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
    [SceneModule("Animations")]
    public class Animations : ISceneModule
    {
        const string AVATAR_ANIMATION = "AvatarAnimation";
        const string VIEWER_EFFECT = "ViewerEffect";
        /// <summary>Magic UUID for combining with an agent ID to create an event ID for animations</summary>
        static readonly UUID ANIMATION_EVENT_ID = new UUID("ba180582-35b8-436e-a62a-39c07f7e4b26");
        /// <summary>Magic UUID for combining with a ViewerEffect ID to create an event ID for viewer effects</summary>
        static readonly UUID EFFECT_EVENT_ID = new UUID("bbaa774c-2cac-446d-b24b-c897582f4f67");

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_scene.AddInterestListHandler(AVATAR_ANIMATION, new InterestListEventHandler { PriorityCallback = AnimationPrioritizer, SendCallback = SendAvatarAnimationPackets });
                m_scene.AddInterestListHandler(VIEWER_EFFECT, new InterestListEventHandler { PriorityCallback = ViewerEffectPrioritizer, SendCallback = SendViewerEffectPackets });

                m_udp.AddPacketHandler(PacketType.AgentAnimation, AgentAnimationHandler);
                m_udp.AddPacketHandler(PacketType.ViewerEffect, ViewerEffectHandler);

                m_scene.OnPresenceAdd += PresenceAddHandler;
                m_scene.OnSendPresenceAnimations += SendPresenceAnimationsHandler;
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.ViewerEffect, ViewerEffectHandler);

                m_scene.OnPresenceAdd -= PresenceAddHandler;
                m_scene.OnSendPresenceAnimations -= SendPresenceAnimationsHandler;
            }
        }

        private void AgentAnimationHandler(Packet packet, LLAgent agent)
        {
            AgentAnimationPacket animPacket = (AgentAnimationPacket)packet;
            bool changed = false;

            for (int i = 0; i < animPacket.AnimationList.Length; i++)
            {
                AgentAnimationPacket.AnimationListBlock block = animPacket.AnimationList[i];

                if (block.StartAnim)
                {
                    if (agent.Animations.Add(block.AnimID, ref agent.CurrentAnimSequenceNum))
                        changed = true;
                }
                else
                {
                    if (agent.Animations.Remove(block.AnimID))
                        changed = true;
                }
            }

            if (changed)
                m_scene.SendPresenceAnimations(this, agent);
        }

        private void ViewerEffectHandler(Packet packet, LLAgent agent)
        {
            ViewerEffectPacket effect = (ViewerEffectPacket)packet;

            // Broadcast this viewer effect to everyone
            for (int i = 0; i < effect.Effect.Length; i++)
            {
                ViewerEffectPacket.EffectBlock block = effect.Effect[i];

                if (block.AgentID == agent.ID)
                    m_scene.CreateInterestListEvent(new InterestListEvent(UUID.Combine(block.ID, EFFECT_EVENT_ID), VIEWER_EFFECT, agent.ScenePosition, Vector3.One, block));
                else
                    m_log.Warn("Skipping ViewerEffect block for " + block.AgentID + " from " + agent.ID + " (" + agent.Name + ")");
            }
        }

        private void PresenceAddHandler(object sender, PresenceArgs e)
        {
            // When an LLUDP agent logs in, send them current animation data for every presence in the sim
            if (e.Presence is LLAgent)
            {
                m_scene.ForEachPresence(
                    delegate(IScenePresence presence)
                    {
                        m_scene.CreateInterestListEventFor(e.Presence, new InterestListEvent(UUID.Combine(presence.ID, ANIMATION_EVENT_ID), AVATAR_ANIMATION,
                            presence.ScenePosition, presence.Scale, presence));
                    }
                );
            }
        }

        private void SendPresenceAnimationsHandler(object sender, PhysicalPresenceArgs e)
        {
            IPhysicalPresence presence = e.Presence;
            presence.Scene.CreateInterestListEvent(new InterestListEvent(UUID.Combine(presence.ID, ANIMATION_EVENT_ID),
                AVATAR_ANIMATION, presence.ScenePosition, presence.Scale, presence));
        }

        private void SendAvatarAnimationPackets(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            for (int i = 0; i < eventDatas.Length; i++)
            {
                LLAgent animAgent = (LLAgent)eventDatas[i].Event.State;

                AvatarAnimationPacket packet = new AvatarAnimationPacket();
                packet.Sender.ID = animAgent.ID;

                Animation[] animations = animAgent.Animations.GetAnimations();

                packet.AnimationList = new AvatarAnimationPacket.AnimationListBlock[animations.Length];
                for (int j = 0; j < animations.Length; j++)
                {
                    Animation animation = animations[j];
                    packet.AnimationList[j] = new AvatarAnimationPacket.AnimationListBlock { AnimID = animation.ID, AnimSequenceID = animation.SequenceNum };
                }

                packet.AnimationSourceList = new AvatarAnimationPacket.AnimationSourceListBlock[1];
                packet.AnimationSourceList[0] = new AvatarAnimationPacket.AnimationSourceListBlock { ObjectID = animAgent.ID };

                packet.PhysicalAvatarEventList = new AvatarAnimationPacket.PhysicalAvatarEventListBlock[0];

                m_udp.SendPacket(agent, packet, ThrottleCategory.Task, false);
            }
        }

        private void SendViewerEffectPackets(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            ViewerEffectPacket packet = new ViewerEffectPacket();
            packet.Header.Reliable = false;
            packet.AgentData.AgentID = presence.ID;
            packet.Effect = new ViewerEffectPacket.EffectBlock[eventDatas.Length];

            for (int i = 0; i < eventDatas.Length; i++)
                packet.Effect[i] = (ViewerEffectPacket.EffectBlock)eventDatas[i].Event.State;

            m_udp.SendPacket(agent, packet, ThrottleCategory.Task, true);
        }

        private double? AnimationPrioritizer(InterestListEvent eventData, IScenePresence presence)
        {
            // Add one so the ObjectUpdate for this avatar has a higher priority
            return InterestListEventHandler.DefaultPrioritizer(eventData, presence).Value + 1.0;
        }

        private double? ViewerEffectPrioritizer(InterestListEvent eventData, IScenePresence presence)
        {
            // Don't bother sending ViewerEffect packets further away than this
            const float VIEWER_EFFECT_CUTOFF = 64.0f * 64.0f;

            float distanceSq = Vector3.DistanceSquared(presence.ScenePosition, eventData.ScenePosition);

            if (distanceSq <= VIEWER_EFFECT_CUTOFF)
                return InterestListEventHandler.DefaultPrioritizer(eventData, presence);
            else
                return null;
        }
    }
}
