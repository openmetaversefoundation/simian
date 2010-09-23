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
using System.Diagnostics;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("SimStats")]
    public class SimStats : ISceneModule
    {
        public enum SimStatType : uint
        {
            TimeDilation = 0,
            SimFPS = 1,
            PhysicsFPS = 2,
            AgentUpdates = 3,
            FrameMS = 4,
            NetMS = 5,
            OtherMS = 6,
            PhysicsMS = 7,
            AgentMS = 8,
            ImageMS = 9,
            ScriptMS = 10,
            TotalPrim = 11,
            ActivePrim = 12,
            Agents = 13,
            ChildAgents = 14,
            ActiveScripts = 15,
            ScriptInstructionsPerSecond = 16,
            InPacketsPerSecond = 17,
            OutPacketsPerSecond = 18,
            PendingDownloads = 19,
            PendingUploads = 20,
            VirtualSizeKB = 21,
            ResidentSizeKB = 22,
            PendingLocalUploads = 23,
            UnAckedBytes = 24,
            PhysicsPinnedTasks = 25,
            PhysicsLODTasks = 26,
            PhysicsStepMS = 27,
            PhysicsShapeMS = 28,
            PhysicsOtherMS = 29,
            PhysicsMemory = 30,
        }

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;
        private RegionInfo m_regionInfo;
        private int m_pid;
        private SimStatsPacket m_stats;
        private IPhysicsEngine m_physics;
        private int m_lastPacketsSent;
        private int m_lastPacketsReceived;
        private int m_simStatsSeconds;

        public void Start(IScene scene)
        {
            m_scene = scene;

            // Get our process ID, used in the SimStats packet
            m_pid = Process.GetCurrentProcess().Id;

            m_regionInfo = m_scene.GetSceneModule<RegionInfo>();
            m_physics = m_scene.GetSceneModule<IPhysicsEngine>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                InitializeStatsPacket();
                m_scene.Simian.AddHeartbeatHandler(SendSimStats);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_scene.Simian.RemoveHeartbeatHandler(SendSimStats);
            }
        }

        public void SetStat(SimStatType type, float value)
        {
            m_stats.Stat[(int)type].StatValue = value;
        }

        private void SendSimStats(object sender, System.Timers.ElapsedEventArgs e)
        {
            const int INTERVAL_SECONDS = 3;

            #region Packet Statistics

            int packetsSent = m_udp.PacketsSent;
            int packetsReceived = m_udp.PacketsReceived;

            int ppsSent = packetsSent - m_lastPacketsSent;
            int ppsReceived = packetsReceived - m_lastPacketsReceived;

            m_lastPacketsSent = packetsSent;
            m_lastPacketsReceived = packetsReceived;

            #endregion Packet Statistics

            if (++m_simStatsSeconds >= INTERVAL_SECONDS)
            {
                m_simStatsSeconds = 0;
                IScenePresence[] presences = m_scene.GetPresences();

                // Don't bother if noone is connected
                if (presences.Length == 0)
                    return;

                #region Region Info

                if (m_regionInfo != null)
                {
                    m_stats.Region.ObjectCapacity = m_regionInfo.ObjectCapacity;
                    m_stats.Region.RegionFlags = (uint)m_regionInfo.RegionFlags;
                }

                #endregion Region Info

                #region Prim/Agent Stats

                // Count root and child agents
                int rootAgents = 0;
                int childAgents = 0;
                int unackedBytes = 0;

                for (int i = 0; i < presences.Length; i++)
                {
                    IScenePresence presence = presences[i];

                    if (presence != null)
                    {
                        if (presence.IsChildPresence)
                            childAgents++;
                        else
                            rootAgents++;

                        if (presence is LLAgent)
                            unackedBytes += ((LLAgent)presence).UnackedBytes;
                    }
                    else
                        m_log.Warn("Unable to send sim stats to null presence");
                }

                SetStat(SimStatType.Agents, rootAgents);
                SetStat(SimStatType.ChildAgents, childAgents);
                SetStat(SimStatType.TotalPrim, m_scene.EntityCount() - presences.Length);
                SetStat(SimStatType.UnAckedBytes, unackedBytes);

                #endregion Prim/Agent Stats

                #region Memory Usage

                Process process = Process.GetCurrentProcess();

                float vmem = (float)process.VirtualMemorySize64 / (1024f * 1024f);
                float mem1 = (float)process.PrivateMemorySize64 / (1024f * 1024f);
                float mem2 = (float)System.GC.GetTotalMemory(false) / (1024f * 1024f);

                SetStat(SimStatType.VirtualSizeKB, vmem);
                // HACK: Average between private memory size (overestimation) and what the GC 
                // thinks it allocated (underestimation, especially when running unmanaged physics)
                SetStat(SimStatType.ResidentSizeKB, (mem1 + mem2) * 0.5f);
                // Report the difference between private memory size and GC memory as physics 
                // memory. Another hack, but this allows us to see the difference between the two
                SetStat(SimStatType.PhysicsMemory, Math.Max(mem1 - mem2, 0f));

                #endregion Memory Usage

                #region Physics

                if (m_physics != null)
                {
                    float frameTimeMS = m_physics.FrameTimeMS;
                    float fps = m_physics.FPS;

                    SetStat(SimStatType.TimeDilation, m_physics.TimeDilation);
                    SetStat(SimStatType.PhysicsFPS, fps);
                    SetStat(SimStatType.SimFPS, fps);
                    SetStat(SimStatType.PhysicsMS, frameTimeMS);
                    SetStat(SimStatType.PhysicsStepMS, frameTimeMS);
                    SetStat(SimStatType.FrameMS, frameTimeMS);
                }

                #endregion Physics

                #region Networking

                SetStat(SimStatType.InPacketsPerSecond, ppsReceived);
                SetStat(SimStatType.OutPacketsPerSecond, ppsSent);

                #endregion Networking

                // TODO: Scripts
                //SimStatType.ActiveScripts
                //SimStatType.ScriptInstructionsPerSecond

                // TODO: Other
                //SimStatType.ActivePrim
                //SimStatType.AgentUpdates

                // Serialize the packet
                byte[] packetData = m_stats.ToBytes();

                // Send the serialized packet to each root agent
                m_scene.ForEachPresence(
                    delegate(IScenePresence presence)
                    {
                        if (presence is LLAgent && !presence.IsChildPresence)
                            m_udp.SendPacketData((LLAgent)presence, packetData, PacketType.SimStats, ThrottleCategory.Task);
                    }
                );
            }
        }

        private void InitializeStatsPacket()
        {
            m_stats = new SimStatsPacket();
            
            m_stats.PidStat.PID = m_pid;

            m_stats.Region.RegionX = (uint)(m_scene.MinPosition.X / 256d);
            m_stats.Region.RegionY = (uint)(m_scene.MinPosition.Y / 256d);

            Array statTypes = Enum.GetValues(typeof(SimStatType));

            m_stats.Stat = new SimStatsPacket.StatBlock[statTypes.Length];
            foreach (uint statIndex in statTypes)
                m_stats.Stat[statIndex] = new SimStatsPacket.StatBlock { StatID = statIndex };
        }
    }
}
