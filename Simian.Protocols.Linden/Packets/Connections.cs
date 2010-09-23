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
using System.ComponentModel.Composition;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    /// <summary>
    /// Handles logins, logouts, teleports, region crossings, bandwidth settings and more for agents
    /// </summary>
    [SceneModule("Connections")]
    public class Connections : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private RegionInfo m_regionInfo;
        private LLPermissions m_permissions;
        private LLUDP m_udp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_regionInfo = m_scene.GetSceneModule<RegionInfo>();
            m_permissions = m_scene.GetSceneModule<LLPermissions>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.UseCircuitCode, UseCircuitCodeHandler);
                m_udp.AddPacketHandler(PacketType.CompleteAgentMovement, CompleteAgentMovementHandler);
                m_udp.AddPacketHandler(PacketType.LogoutRequest, LogoutRequestHandler);
                m_udp.AddPacketHandler(PacketType.AgentThrottle, AgentThrottleHandler);
                m_udp.AddPacketHandler(PacketType.RegionHandshakeReply, RegionHandshakeReplyHandler);
                m_udp.AddPacketHandler(PacketType.AgentFOV, AgentFOVHandler);
                m_udp.AddPacketHandler(PacketType.AgentHeightWidth, AgentHeightWidthHandler);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.UseCircuitCode, UseCircuitCodeHandler);
                m_udp.RemovePacketHandler(PacketType.CompleteAgentMovement, CompleteAgentMovementHandler);
                m_udp.RemovePacketHandler(PacketType.LogoutRequest, LogoutRequestHandler);
                m_udp.RemovePacketHandler(PacketType.AgentThrottle, AgentThrottleHandler);
                m_udp.RemovePacketHandler(PacketType.RegionHandshakeReply, RegionHandshakeReplyHandler);
                m_udp.RemovePacketHandler(PacketType.AgentFOV, AgentFOVHandler);
                m_udp.RemovePacketHandler(PacketType.AgentHeightWidth, AgentHeightWidthHandler);
            }
        }

        private void UseCircuitCodeHandler(Packet packet, LLAgent agent)
        {
            // Add the agent to the scene
            m_scene.EntityAddOrUpdate(this, agent, UpdateFlags.FullUpdate, 0);

            Estates.SendRegionHandshake(agent, m_udp, m_scene, m_regionInfo, m_permissions);
        }

        private void CompleteAgentMovementHandler(Packet packet, LLAgent agent)
        {
            //CompleteAgentMovementPacket cam = (CompleteAgentMovementPacket)packet;

            AgentMovementCompletePacket amc = new AgentMovementCompletePacket();
            amc.AgentData.AgentID = agent.ID;
            amc.AgentData.SessionID = agent.SessionID;
            amc.Data.LookAt = Vector3.UnitX;
            amc.Data.Position = new Vector3(128f, 128f, 25f);
            amc.Data.RegionHandle = Util.PositionToRegionHandle(m_scene.MinPosition);
            amc.Data.Timestamp = Utils.DateTimeToUnixTime(DateTime.UtcNow);
            amc.SimData.ChannelVersion = Utils.StringToBytes("Simian");

            m_log.Debug("Sending AgentMovementComplete to " + agent.Name);

            m_udp.SendPacket(agent, amc, ThrottleCategory.Task, false);
        }

        private void LogoutRequestHandler(Packet packet, LLAgent agent)
        {
            LogoutReplyPacket reply = new LogoutReplyPacket();
            reply.AgentData.AgentID = agent.ID;
            reply.AgentData.SessionID = agent.SessionID;
            reply.InventoryData = new LogoutReplyPacket.InventoryDataBlock[1];
            reply.InventoryData[0] = new LogoutReplyPacket.InventoryDataBlock();
            reply.InventoryData[0].ItemID = UUID.Zero;

            m_udp.SendPacket(agent, reply, ThrottleCategory.Task, false);

            agent.Shutdown();
        }

        private void AgentThrottleHandler(Packet packet, LLAgent agent)
        {
            AgentThrottlePacket throttle = (AgentThrottlePacket)packet;
            agent.SetThrottles(throttle.Throttle.Throttles);
        }

        private void RegionHandshakeReplyHandler(Packet packet, LLAgent agent)
        {
            //FIXME: is this really where this belongs?

            EconomyDataPacket economy = new EconomyDataPacket();
            economy.Info.PriceUpload = 0;
            //TODO: populate economy.*

            m_udp.SendPacket(agent, economy, ThrottleCategory.Task, false);
        }

        private void AgentFOVHandler(Packet packet, LLAgent agent)
        {
            AgentFOVPacket fov = (AgentFOVPacket)packet;
            agent.CameraVerticalAngle = fov.FOVBlock.VerticalAngle;
        }

        private void AgentHeightWidthHandler(Packet packet, LLAgent agent)
        {
            AgentHeightWidthPacket hw = (AgentHeightWidthPacket)packet;
            agent.CameraHeight = hw.HeightWidthBlock.Height;
            agent.CameraWidth = hw.HeightWidthBlock.Width;
        }
    }
}
