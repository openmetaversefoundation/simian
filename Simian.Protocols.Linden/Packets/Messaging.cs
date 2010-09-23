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
    [SceneModule("Messaging")]
    public class Messaging : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.ImprovedInstantMessage, ImprovedInstantMessageHandler);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.ImprovedInstantMessage, ImprovedInstantMessageHandler);
            }
        }

        private void ImprovedInstantMessageHandler(Packet packet, LLAgent agent)
        {
            ImprovedInstantMessagePacket im = (ImprovedInstantMessagePacket)packet;

            // The following fields are unused since we already have this information, plus the 
            // client could forge it:
            // - im.MessageBlock.FromAgentName
            // - im.MessageBlock.FromGroup;
            // - im.MessageBlock.RegionID
            // - im.MessageBlock.Position
            // - im.MessageBlock.Timestamp
            // - im.MessageBlock.ParentEstateID

            InstantMessageDialog type = (InstantMessageDialog)im.MessageBlock.Dialog;
            string message = Utils.BytesToString(im.MessageBlock.Message);
            bool allowOffline = (im.MessageBlock.Offline != 0);

            switch (type)
            {
                case InstantMessageDialog.MessageFromAgent:
                case InstantMessageDialog.StartTyping:
                case InstantMessageDialog.StopTyping:
                    SendInstantMessage(im.MessageBlock.ID, im.MessageBlock.ToAgentID, agent.Name,
                        agent.ScenePosition, agent.Scene.ID, false, type, message, allowOffline, 
                        DateTime.UtcNow, im.MessageBlock.BinaryBucket);
                    break;

                case InstantMessageDialog.RequestTeleport:
                case InstantMessageDialog.GodLikeRequestTeleport:
                case InstantMessageDialog.Lure911:
                case InstantMessageDialog.AcceptTeleport:
                case InstantMessageDialog.DenyTeleport:
                case InstantMessageDialog.BusyAutoResponse:
                    break;

                case InstantMessageDialog.FriendshipOffered:
                case InstantMessageDialog.FriendshipAccepted:
                case InstantMessageDialog.FriendshipDeclined:
                    break;

                case InstantMessageDialog.GroupInvitation:
                case InstantMessageDialog.GroupInvitationAccept:
                case InstantMessageDialog.GroupInvitationDecline:
                    break;

                case InstantMessageDialog.GroupNotice:
                case InstantMessageDialog.GroupNoticeRequested:
                case InstantMessageDialog.GroupNoticeInventoryAccepted:
                case InstantMessageDialog.GroupNoticeInventoryDeclined:
                case InstantMessageDialog.GroupVote:
                    break;

                case InstantMessageDialog.InventoryOffered:
                case InstantMessageDialog.InventoryAccepted:
                case InstantMessageDialog.InventoryDeclined:
                    break;

                case InstantMessageDialog.TaskInventoryOffered:
                case InstantMessageDialog.TaskInventoryAccepted:
                case InstantMessageDialog.TaskInventoryDeclined:
                    break;

                case InstantMessageDialog.SessionAdd:
                case InstantMessageDialog.SessionOfflineAdd:
                case InstantMessageDialog.SessionCardlessStart:
                case InstantMessageDialog.Session911Start:
                case InstantMessageDialog.SessionDrop:
                case InstantMessageDialog.SessionGroupStart:
                case InstantMessageDialog.SessionSend:
                    break;

                //case InstantMessageDialog.MessageFromObject:
                //case InstantMessageDialog.FromTaskAsAlert:
                //case InstantMessageDialog.MessageBox:
                //case InstantMessageDialog.GotoUrl:
                //case InstantMessageDialog.ConsoleAndChatHistory:
                //case InstantMessageDialog.NewUserDefault:
                default:
                    m_log.Warn("Received an IM with unhandled type " + type + " from " + agent.Name);
                    return;
            }
        }

        public bool SendInstantMessage(UUID messageID, UUID toAgentID, string fromName, Vector3 fromPosition, 
            UUID fromRegionID, bool fromGroup, InstantMessageDialog type, string message, bool allowOffline, 
            DateTime timestamp, byte[] binaryBucket)
        {
            // Cap the message length at 1023 + null terminator
            if (!String.IsNullOrEmpty(message) && message.Length > 1023)
                message = message.Substring(0, 1023);

            // FIXME: Support IMing to remote agents
            IScenePresence presence;
            if (m_scene.TryGetPresence(toAgentID, out presence) && presence is LLAgent)
            {
                LLAgent agent = (LLAgent)presence;

                ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket();
                im.AgentData.AgentID = agent.ID;
                im.MessageBlock.Dialog = (byte)type;
                im.MessageBlock.FromAgentName = Utils.StringToBytes(fromName);
                im.MessageBlock.FromGroup = fromGroup;
                im.MessageBlock.ID = messageID;
                im.MessageBlock.Message = Utils.StringToBytes(message);
                im.MessageBlock.Offline = (byte)((allowOffline) ? 1 : 0);
                im.MessageBlock.ParentEstateID = 0;
                im.MessageBlock.Position = fromPosition;
                im.MessageBlock.RegionID = fromRegionID;
                im.MessageBlock.Timestamp = Utils.DateTimeToUnixTime(timestamp);
                im.MessageBlock.ToAgentID = agent.ID;
                im.MessageBlock.BinaryBucket = binaryBucket ?? Utils.EmptyBytes;

                m_udp.SendPacket(agent, im, ThrottleCategory.Task, false);
                return true;
            }
            else
            {
                m_log.Warn("Dropping instant message from " + fromName + " to " + toAgentID + " that does not exist in the local scene");
                return false;
            }
        }
    }
}
