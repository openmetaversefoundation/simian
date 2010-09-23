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
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden.Packets
{
   [SceneModule("AgentData")]
    public class AgentData : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IUserClient m_userClient;
        private LLUDP m_udp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_userClient = m_scene.Simian.GetAppModule<IUserClient>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.AgentDataUpdateRequest, AgentDataUpdateRequestHandler);
                m_udp.AddPacketHandler(PacketType.UUIDNameRequest, UUIDNameRequestHandler);
                m_udp.AddPacketHandler(PacketType.UUIDGroupNameRequest, UUIDGroupNameRequestHandler);
                m_udp.AddPacketHandler(PacketType.AvatarPropertiesRequest, AvatarPropertiesRequestHandler);
                m_udp.AddPacketHandler(PacketType.AvatarPropertiesUpdate, AvatarPropertiesUpdateHandler);
                m_udp.AddPacketHandler(PacketType.AvatarInterestsUpdate, AvatarInterestsUpdateHandler);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.AgentDataUpdateRequest, AgentDataUpdateRequestHandler);
                m_udp.RemovePacketHandler(PacketType.UUIDNameRequest, UUIDNameRequestHandler);
                m_udp.RemovePacketHandler(PacketType.UUIDGroupNameRequest, UUIDGroupNameRequestHandler);
                m_udp.RemovePacketHandler(PacketType.AvatarPropertiesRequest, AvatarPropertiesRequestHandler);
                m_udp.RemovePacketHandler(PacketType.AvatarPropertiesUpdate, AvatarPropertiesUpdateHandler);
                m_udp.RemovePacketHandler(PacketType.AvatarInterestsUpdate, AvatarInterestsUpdateHandler);
            }
        }

        private void AgentDataUpdateRequestHandler(Packet packet, LLAgent agent)
        {
            string firstName, lastName;
            Util.GetFirstLastName(agent.Name, out firstName, out lastName);

            AgentDataUpdatePacket response = new AgentDataUpdatePacket();
            response.AgentData.AgentID = agent.ID;
            response.AgentData.FirstName = Utils.StringToBytes(firstName);
            response.AgentData.LastName = Utils.StringToBytes(lastName);

            bool groupFetchSuccess = false;
            User user;
            if (m_userClient != null && m_userClient.TryGetUser(agent.ID, out user))
            {
                OSDMap groupMap = user.GetField("active_group") as OSDMap;

                if (groupMap != null)
                {
                    response.AgentData.ActiveGroupID = groupMap["id"].AsUUID();
                    response.AgentData.GroupName = Utils.StringToBytes(groupMap["name"].AsString());
                    response.AgentData.GroupPowers = groupMap["powers"].AsULong();
                    response.AgentData.GroupTitle = Utils.StringToBytes(groupMap["title"].AsString());

                    groupFetchSuccess = true;
                }
            }
            
            if (!groupFetchSuccess)
            {
                response.AgentData.GroupName = Utils.EmptyBytes;
                response.AgentData.GroupTitle = Utils.EmptyBytes;
            }

            m_udp.SendPacket(agent, response, ThrottleCategory.Task, false);
        }

        private void UUIDNameRequestHandler(Packet packet, LLAgent agent)
        {
            UUIDNameRequestPacket request = (UUIDNameRequestPacket)packet;

            List<UUIDNameReplyPacket.UUIDNameBlockBlock> responses = new List<UUIDNameReplyPacket.UUIDNameBlockBlock>();

            for (int i = 0; i < request.UUIDNameBlock.Length; i++)
            {
                UUID requestID = request.UUIDNameBlock[i].ID;
                string firstName = null, lastName = null;

                // See if we can fetch a presence in the local scene with the requested UUID first
                IScenePresence localPresence;
                if (m_scene.TryGetPresence(requestID, out localPresence))
                {
                    Util.GetFirstLastName(localPresence.Name, out firstName, out lastName);
                }
                else if (m_userClient != null)
                {
                    // TODO: We might want to switch to a batch user service command in the future
                    User user;
                    if (m_userClient.TryGetUser(requestID, out user))
                        Util.GetFirstLastName(user.Name, out firstName, out lastName);
                }

                if (firstName != null && lastName != null)
                {
                    UUIDNameReplyPacket.UUIDNameBlockBlock block = new UUIDNameReplyPacket.UUIDNameBlockBlock();
                    block.ID = requestID;
                    block.FirstName = Utils.StringToBytes(firstName);
                    block.LastName = Utils.StringToBytes(lastName);

                    responses.Add(block);
                }
            }

            // Build the response packet
            UUIDNameReplyPacket response = new UUIDNameReplyPacket();
            response.UUIDNameBlock = responses.ToArray();

            m_udp.SendPacket(agent, response, ThrottleCategory.Task, true);
        }

        private void UUIDGroupNameRequestHandler(Packet packet, LLAgent agent)
        {
            // TODO:
            m_log.Warn("Implement UUIDGroupNameRequest handling");
        }

        private void AvatarPropertiesRequestHandler(Packet packet, LLAgent agent)
        {
            AvatarPropertiesRequestPacket request = (AvatarPropertiesRequestPacket)packet;

            User user;
            if (m_userClient != null && m_userClient.TryGetUser(request.AgentData.AvatarID, out user))
            {
                SendAvatarProperties(agent, request.AgentData.AvatarID, user);
                SendAvatarInterests(agent, request.AgentData.AvatarID, user);
            }
            else
            {
                m_log.Warn("Could not find user " + request.AgentData.AvatarID + ", returning empty profile to " + agent.Name);
                SendAvatarProperties(agent, request.AgentData.AvatarID, null);
                SendAvatarInterests(agent, request.AgentData.AvatarID, null);
            }
        }

        private void AvatarPropertiesUpdateHandler(Packet packet, LLAgent agent)
        {
            AvatarPropertiesUpdatePacket update = (AvatarPropertiesUpdatePacket)packet;

            User user;
            if (m_userClient != null && m_userClient.TryGetUser(agent.ID, out user))
            {
                OSDMap updates = new OSDMap
                {
                    { "About", OSD.FromString(Utils.BytesToString(update.PropertiesData.AboutText)) },
                    { "AllowPublish", OSD.FromBoolean(update.PropertiesData.AllowPublish) },
                    { "FLAbout", OSD.FromString(Utils.BytesToString(update.PropertiesData.FLAboutText)) },
                    { "FLImage", OSD.FromUUID(update.PropertiesData.FLImageID) },
                    { "Image", OSD.FromUUID(update.PropertiesData.ImageID) },
                    { "AllowMaturePublish", OSD.FromBoolean(update.PropertiesData.MaturePublish) },
                    { "URL", OSD.FromString(Utils.BytesToString(update.PropertiesData.ProfileURL)) }
                };

                m_userClient.UpdateUserFields(agent.ID, updates);
                SendAvatarProperties(agent, agent.ID, user);
            }
            else
            {
                m_log.Warn("Could not find user " + agent.ID + ", not updating profile for " + agent.Name);
                SendAvatarProperties(agent, agent.ID, null);
            }
        }

        private void AvatarInterestsUpdateHandler(Packet packet, LLAgent agent)
        {
            AvatarInterestsUpdatePacket update = (AvatarInterestsUpdatePacket)packet;

            User user;
            if (m_userClient != null && m_userClient.TryGetUser(agent.ID, out user))
            {
                OSDMap map = new OSDMap
                {
                    { "WantMask", OSD.FromInteger((int)update.PropertiesData.WantToMask) },
                    { "WantText", OSD.FromString(Utils.BytesToString(update.PropertiesData.WantToText)) },
                    { "SkillsMask", OSD.FromInteger((int)update.PropertiesData.SkillsMask) },
                    { "SkillsText", OSD.FromString(Utils.BytesToString(update.PropertiesData.SkillsText)) },
                    { "Languages", OSD.FromString(Utils.BytesToString(update.PropertiesData.LanguagesText)) }
                };

                m_userClient.UpdateUserFields(agent.ID, new OSDMap { { "LLInterests", map } });
            }
            else
            {
                m_log.Warn("Could not find user " + agent.ID + ", not updating profile interests for " + agent.Name);
                SendAvatarInterests(agent, agent.ID, null);
            }
        }

        private void SendAvatarProperties(LLAgent agent, UUID avatarID, User user)
        {
            AvatarPropertiesReplyPacket reply = new AvatarPropertiesReplyPacket();
            reply.AgentData.AgentID = agent.ID;
            reply.AgentData.AvatarID = avatarID;

            // TODO: Check for ProfileFlags.Online (including permission check)
            ProfileFlags profileFlags = 0;

            if (user != null)
            {
                if (user.AccessLevel > 0)
                    profileFlags |= ProfileFlags.Identified;

                reply.PropertiesData.AboutText = Utils.StringToBytes(user.GetField("About").AsString());
                reply.PropertiesData.BornOn = Utils.StringToBytes(user.GetField("CreationDate").AsDate().ToString("M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture));
                reply.PropertiesData.CharterMember = (user.AccessLevel >= 200) ? Utils.StringToBytes("Operator") : Utils.EmptyBytes;
                reply.PropertiesData.FLAboutText = Utils.StringToBytes(user.GetField("FLAbout").AsString());
                reply.PropertiesData.Flags = (uint)profileFlags;
                reply.PropertiesData.FLImageID = user.GetField("FLImage").AsUUID();
                reply.PropertiesData.ImageID = user.GetField("Image").AsUUID();
                reply.PropertiesData.PartnerID = user.GetField("Partner").AsUUID();
                reply.PropertiesData.ProfileURL = Utils.StringToBytes(user.GetField("URL").AsString());
            }
            else
            {
                reply.PropertiesData.AboutText = Utils.EmptyBytes;
                reply.PropertiesData.BornOn = Utils.EmptyBytes;
                reply.PropertiesData.CharterMember = Utils.EmptyBytes;
                reply.PropertiesData.FLAboutText = Utils.EmptyBytes;
                reply.PropertiesData.ProfileURL = Utils.EmptyBytes;
            }

            m_udp.SendPacket(agent, reply, ThrottleCategory.Task, false);
        }

        private void SendAvatarInterests(LLAgent agent, UUID avatarID, User user)
        {
            AvatarInterestsReplyPacket reply = new AvatarInterestsReplyPacket();
            reply.AgentData.AgentID = agent.ID;
            reply.AgentData.AvatarID = avatarID;
            OSDMap interests;

            if (user != null && (interests = user.GetField("LLInterests") as OSDMap) != null)
            {
                reply.PropertiesData.LanguagesText = Utils.StringToBytes(interests["Languages"].AsString());
                reply.PropertiesData.SkillsMask = interests["SkillsMask"].AsUInteger();
                reply.PropertiesData.SkillsText = Utils.StringToBytes(interests["SkillsText"].AsString());
                reply.PropertiesData.WantToMask = interests["WantMask"].AsUInteger();
                reply.PropertiesData.WantToText = Utils.StringToBytes(interests["WantText"].AsString());
            }
            else
            {
                reply.PropertiesData.LanguagesText = Utils.EmptyBytes;
                reply.PropertiesData.SkillsText = Utils.EmptyBytes;
                reply.PropertiesData.WantToText = Utils.EmptyBytes;
            }

            m_udp.SendPacket(agent, reply, ThrottleCategory.Task, false);
        }
    }
}
