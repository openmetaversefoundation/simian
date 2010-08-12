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
            }
        }

        private void UseCircuitCodeHandler(Packet packet, LLAgent agent)
        {
            RegionHandshakePacket handshake = new RegionHandshakePacket();

            handshake.RegionInfo.CacheID = m_scene.ID;
            handshake.RegionInfo.SimName = Utils.StringToBytes(m_scene.Name);
            handshake.RegionInfo2.RegionID = m_scene.ID;
            handshake.RegionInfo.IsEstateManager = (m_permissions != null) ? m_permissions.IsEstateManager(agent) : true;

            if (m_regionInfo != null)
            {
                handshake.RegionInfo3.ColoName = Utils.EmptyBytes;
                handshake.RegionInfo3.ProductName = Utils.StringToBytes(m_regionInfo.ProductName);
                handshake.RegionInfo3.ProductSKU = Utils.StringToBytes(m_regionInfo.ProductSKU);
                handshake.RegionInfo.RegionFlags = (uint)m_regionInfo.RegionFlags;
                handshake.RegionInfo.SimAccess = (byte)m_regionInfo.SimAccess;
                handshake.RegionInfo.SimOwner = m_regionInfo.OwnerID;
                handshake.RegionInfo.TerrainBase0 = m_regionInfo.TerrainBase0;
                handshake.RegionInfo.TerrainBase1 = m_regionInfo.TerrainBase1;
                handshake.RegionInfo.TerrainBase2 = m_regionInfo.TerrainBase2;
                handshake.RegionInfo.TerrainBase3 = m_regionInfo.TerrainBase3;
                handshake.RegionInfo.TerrainDetail0 = m_regionInfo.TerrainDetail0;
                handshake.RegionInfo.TerrainDetail1 = m_regionInfo.TerrainDetail1;
                handshake.RegionInfo.TerrainDetail2 = m_regionInfo.TerrainDetail2;
                handshake.RegionInfo.TerrainDetail3 = m_regionInfo.TerrainDetail3;
                handshake.RegionInfo.TerrainHeightRange00 = m_regionInfo.TerrainHeightRange00;
                handshake.RegionInfo.TerrainHeightRange01 = m_regionInfo.TerrainHeightRange01;
                handshake.RegionInfo.TerrainHeightRange10 = m_regionInfo.TerrainHeightRange10;
                handshake.RegionInfo.TerrainHeightRange11 = m_regionInfo.TerrainHeightRange11;
                handshake.RegionInfo.TerrainStartHeight00 = m_regionInfo.TerrainStartHeight00;
                handshake.RegionInfo.TerrainStartHeight01 = m_regionInfo.TerrainStartHeight01;
                handshake.RegionInfo.TerrainStartHeight10 = m_regionInfo.TerrainStartHeight10;
                handshake.RegionInfo.TerrainStartHeight11 = m_regionInfo.TerrainStartHeight11;
                handshake.RegionInfo.WaterHeight = m_regionInfo.WaterHeight;
            }
            else
            {
                handshake.RegionInfo3.ColoName = Utils.EmptyBytes;
                handshake.RegionInfo3.ProductName = Utils.StringToBytes("Simian");
                handshake.RegionInfo3.ProductSKU = Utils.EmptyBytes;
                handshake.RegionInfo.SimAccess = (byte)SimAccess.Min;
            }

            // Add the agent to the scene
            m_scene.EntityAddOrUpdate(this, agent, UpdateFlags.FullUpdate, 0);

            m_udp.SendPacket(agent, handshake, ThrottleCategory.Task, false);
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
    }
}
