/*
 * Copyright (c) Open Metaverse Foundation
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Chat")]
    public class Chat : ISceneModule
    {
        private class TypingData
        {
            public ISceneEntity Source;
            public bool StartTyping;
        }

        /// <summary>The audible distance for whispering</summary>
        public const float WHISPER_DIST = 10f;
        /// <summary>The audible distance for typing and normal chat messages</summary>
        public const float NORMAL_DIST = 20f;
        /// <summary>The audible distance for shouting</summary>
        public const float SHOUT_DIST = 100f;

        /// <summary>Interest list identifier for typing events</summary>
        const string VIEWER_TYPING = "ViewerTyping";
        /// <summary>Interest list identifier for chat events</summary>
        const string VIEWER_CHAT = "ViewerChat";

        /// <summary>Magic UUID for combining with an agent ID to create an event ID for typing</summary>
        static readonly UUID TYPING_EVENT_ID = new UUID("64acad90-d41a-11de-8a39-0800200c9a66");

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                // Register for the LLUDP chat packet
                m_udp.AddPacketHandler(PacketType.ChatFromViewer, ChatFromViewerHandler);

                // Register for the generic scene chat event
                m_scene.OnEntityChat += ChatHandler;

                // Register for the generic presence alert event
                m_scene.OnPresenceAlert += PresenceAlertHandler;

                // Add event callbacks for two interest list events that we define: typing and chat
                m_scene.AddInterestListHandler(VIEWER_TYPING, new InterestListEventHandler { PriorityCallback = TypingPrioritizer, SendCallback = SendTypingPackets });
                m_scene.AddInterestListHandler(VIEWER_CHAT, new InterestListEventHandler { PriorityCallback = ChatPrioritizer, SendCallback = SendChatPackets });
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.ChatFromViewer, ChatFromViewerHandler);

                m_scene.OnEntityChat -= ChatHandler;
            }
        }

        private void ChatFromViewerHandler(Packet packet, LLAgent agent)
        {
            ChatFromViewerPacket chat = (ChatFromViewerPacket)packet;
            ChatType chatType = (ChatType)chat.ChatData.Type;
            string message = Utils.BytesToString(chat.ChatData.Message);
            int channel = chat.ChatData.Channel;

            // Don't allow clients to chat on negative channels
            if (channel < 0)
                channel = 0;

            // Start/stop typing messages are specific to the LLUDP protocol, so we create events
            // directly that will be processed by this same class. Chat messages are a generic
            // event that can be supported by multiple protocols, so we call IScene.EntityChat and
            // hook IScene.OnChat to do the actual processing

            // Event IDs for start/stop typing are generated with UUID.Combine(agent.ID, TYPING_EVENT_ID)
            // to create an ID that is unique to each agent in the context of typing. Newer typing
            // events will overwrite older ones

            switch (chatType)
            {
                case ChatType.StartTyping:
                    m_scene.CreateInterestListEvent(new InterestListEvent(UUID.Combine(agent.ID, TYPING_EVENT_ID),
                        VIEWER_TYPING, agent.ScenePosition, new Vector3(NORMAL_DIST),
                        new TypingData { Source = agent, StartTyping = true }));
                    break;
                case ChatType.StopTyping:
                    m_scene.CreateInterestListEvent(new InterestListEvent(UUID.Combine(agent.ID, TYPING_EVENT_ID),
                        VIEWER_TYPING, agent.ScenePosition, new Vector3(NORMAL_DIST),
                        new TypingData { Source = agent, StartTyping = false }));
                    break;
                case ChatType.Whisper:
                    m_scene.EntityChat(this, agent, WHISPER_DIST, message, channel, EntityChatType.Normal);
                    break;
                case ChatType.Shout:
                    m_scene.EntityChat(this, agent, SHOUT_DIST, message, channel, EntityChatType.Normal);
                    break;
                case ChatType.Normal:
                default:
                    m_scene.EntityChat(this, agent, NORMAL_DIST, message, channel, EntityChatType.Normal);
                    break;
            }
        }

        private void ChatHandler(object sender, ChatArgs e)
        {
            // This scene callback allows us to handle chat that originates from any protocol or
            // module, not just LLUDP chat. A random UUID is generated for each chat message to
            // prevent new chat messages from overwriting old ones
            if (e.Channel == 0)
            {
                Vector3 scenePosition;
                if (e.Source != null)
                    scenePosition = e.Source.ScenePosition;
                else
                    scenePosition = Vector3.Zero;

                if (e.Type == EntityChatType.Owner || e.Type == EntityChatType.Debug)
                {
                    IScenePresence owner;
                    if (m_scene.TryGetPresence(e.Source.OwnerID, out owner))
                        // NOTE: Audible distance is ignored for EntityChatType.[Owner/Debug]
                        m_scene.CreateInterestListEventFor(owner, new InterestListEvent(UUID.Random(), VIEWER_CHAT, scenePosition, Vector3.Zero, e));
                    else
                        m_log.Warn("Can't send direct chat to missing presence " + e.Source.OwnerID);
                }
                else
                {
                    m_scene.CreateInterestListEvent(new InterestListEvent(UUID.Random(), VIEWER_CHAT, scenePosition, new Vector3(e.AudibleDistance), e));
                }
            }
        }

        private void PresenceAlertHandler(object sender, PresenceAlertArgs e)
        {
            if (e.Presence is LLAgent)
            {
                LLAgent agent = (LLAgent)e.Presence;

                AlertMessagePacket alert = new AlertMessagePacket();
                alert.AlertData.Message = Utils.StringToBytes(e.Message);
                alert.AlertInfo = new AlertMessagePacket.AlertInfoBlock[0];

                m_udp.SendPacket(agent, alert, ThrottleCategory.Task, false);
            }
        }

        private double? TypingPrioritizer(InterestListEvent eventData, IScenePresence presence)
        {
            // This prioritizer will return the distance of the typing source to the current scene
            // presence. Lower numbers mean a higher priority, so the closer the source the
            // higher the priority. If the event is out of range for its audible distance,
            // the event will be suppressed entirely

            float distanceSq = Vector3.DistanceSquared(eventData.ScenePosition, presence.ScenePosition);

            if (distanceSq < NORMAL_DIST * NORMAL_DIST)
                return InterestListEventHandler.DefaultPrioritizer(eventData, presence);
            else
                return null;
        }

        private double? ChatPrioritizer(InterestListEvent eventData, IScenePresence presence)
        {
            ChatArgs args = (ChatArgs)eventData.State;
            switch (args.Type)
            {
                case EntityChatType.Owner:
                case EntityChatType.Debug:
                    return 0.0;
                case EntityChatType.Broadcast:
                    return 1.0;
                default:
                    return InterestListEventHandler.DefaultPrioritizer(eventData, presence);
            }
        }

        private void SendTypingPackets(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            // We can't combine chat blocks together, so send a packet for each typing event
            // that is pulled off the event queue
            for (int i = 0; i < eventDatas.Length; i++)
            {
                TypingData data = (TypingData)eventDatas[i].Event.State;
                ChatAudibleLevel audible = GetAudibleLevel(data.Source.ScenePosition, presence.ScenePosition, NORMAL_DIST);

                ChatFromSimulatorPacket packet = new ChatFromSimulatorPacket();
                packet.ChatData.Audible = (byte)audible;
                packet.ChatData.ChatType = (byte)(data.StartTyping ? ChatType.StartTyping : ChatType.StopTyping);
                packet.ChatData.FromName = Utils.StringToBytes(data.Source.Name);
                packet.ChatData.Message = Utils.EmptyBytes;
                packet.ChatData.OwnerID = data.Source.OwnerID;
                packet.ChatData.Position = data.Source.ScenePosition;
                packet.ChatData.SourceID = data.Source.ID;
                packet.ChatData.SourceType = (byte)ChatSourceType.Agent;

                m_udp.SendPacket(agent, packet, ThrottleCategory.Task, false);
            }
        }

        private void SendChatPackets(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            // We can't combine chat blocks together, so send a packet for each chat event
            // that is pulled off the event queue
            for (int i = 0; i < eventDatas.Length; i++)
            {
                ChatArgs data = (ChatArgs)eventDatas[i].Event.State;
                ChatAudibleLevel audible;
                ChatType type;
                string sourceName;
                ChatSourceType sourceType;
                string message;

                ChatFromSimulatorPacket packet = new ChatFromSimulatorPacket();

                if (data.Source == null)
                {
                    // System message
                    audible = ChatAudibleLevel.Fully;
                    type = ChatType.Normal;
                    sourceName = m_scene.Name;
                    message = data.Message;
                    sourceType = ChatSourceType.System;

                    packet.ChatData.FromName = Utils.StringToBytes(m_scene.Name);
                    packet.ChatData.OwnerID = UUID.Zero;
                    packet.ChatData.Position = Vector3.Zero;
                    packet.ChatData.SourceID = m_scene.ID;
                }
                else
                {
                    // Message from an agent or object
                    sourceName = data.Source.Name;

                    switch (data.Type)
                    {
                        case EntityChatType.Debug:
                            type = ChatType.Debug;
                            audible = ChatAudibleLevel.Fully;
                            break;
                        case EntityChatType.Owner:
                            type = ChatType.OwnerSay;
                            audible = ChatAudibleLevel.Fully;
                            break;
                        case EntityChatType.Broadcast:
                            type = ChatType.Normal;
                            audible = ChatAudibleLevel.Fully;
                            break;
                        default:
                            type = GetChatType(data.AudibleDistance);
                            audible = GetAudibleLevel(data.Source.ScenePosition, presence.ScenePosition, data.AudibleDistance);
                            break;
                    }

                    if (audible == ChatAudibleLevel.Fully)
                        message = data.Message;
                    else
                        message = String.Empty;

                    if (data.Source is IScenePresence)
                        sourceType = ChatSourceType.Agent;
                    else
                        sourceType = ChatSourceType.Object;

                    packet.ChatData.FromName = Utils.StringToBytes(data.Source.Name);
                    packet.ChatData.OwnerID = data.Source.OwnerID;
                    packet.ChatData.Position = data.Source.ScenePosition;
                    packet.ChatData.SourceID = data.Source.ID;
                }

                packet.ChatData.Audible = (byte)audible;
                packet.ChatData.ChatType = (byte)type;
                packet.ChatData.Message = Utils.StringToBytes(message);
                packet.ChatData.SourceType = (byte)sourceType;

                m_udp.SendPacket(agent, packet, ThrottleCategory.Task, false);
            }
        }

        private ChatAudibleLevel GetAudibleLevel(Vector3 sourcePosition, Vector3 targetPosition, float audibleDistance)
        {
            float distanceSq = Vector3.DistanceSquared(sourcePosition, targetPosition);

            if (distanceSq <= audibleDistance * audibleDistance)
                return ChatAudibleLevel.Fully;
            else
                return ChatAudibleLevel.Barely;
        }

        /// <summary>
        /// Allows us to convert from generic chat events to the LLUDP
        /// whisper/normal/shout chat types
        /// </summary>
        /// <param name="audibleDistance">Audible distance of the chat message</param>
        /// <returns>Type of LLUDP chat message</returns>
        private ChatType GetChatType(float audibleDistance)
        {
            if (audibleDistance <= WHISPER_DIST)
                return ChatType.Whisper;
            else if (audibleDistance <= NORMAL_DIST)
                return ChatType.Normal;
            else
                return ChatType.Shout;
        }
    }
}
