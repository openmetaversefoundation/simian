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
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("ClientStats")]
    public class ClientStats : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private IStatsTracker m_statsTracker;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("GetTexture requires an IHttpServer");
                return;
            }

            m_statsTracker = m_scene.Simian.GetAppModule<IStatsTracker>();

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "ViewerStats", ViewerStatsHandler);
            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "TextureStats", TextureStatsHandler);
        }

        public void Stop()
        {
            m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "ViewerStats");
            m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "TextureStats");
        }

        private void ViewerStatsHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            ViewerStatsMessage message;
            if (LLUtil.TryGetMessage<ViewerStatsMessage>(request.Body, out message))
            {
                DateTime timestamp = DateTime.UtcNow;

                if (m_statsTracker != null)
                {
                    // Log timestamped data points
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "AgentFPS", message.AgentFPS);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "AgentMemoryUsed", message.AgentMemoryUsed);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "AgentPing", message.AgentPing);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "AgentsInView", message.AgentsInView);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "FailuresInvalid", message.FailuresInvalid);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "FailuresOffCircuit", message.FailuresOffCircuit);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "FailuresResent", message.FailuresResent);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "FailuresSendPacket", message.FailuresSendPacket);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "InCompressedPackets", message.InCompressedPackets);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "InKbytes", message.InKbytes);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "InPackets", message.InPackets);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "InSavings", message.InSavings);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "MetersTraveled", message.MetersTraveled);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "MiscInt1", message.MiscInt1);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "MiscInt2", message.MiscInt2);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "MiscString1", message.MiscString1);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "MiscVersion", message.MiscVersion);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "ObjectKbytes", message.object_kbytes);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "OutCompressedPackets", message.OutCompressedPackets);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "OutKbytes", message.OutKbytes);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "OutPackets", message.OutPackets);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "OutSavings", message.OutSavings);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "RegionsVisisted", message.RegionsVisited);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "SimulatorFPS", message.SimulatorFPS);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "StatsDropped", message.StatsDropped);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "StatsFailedResends", message.StatsFailedResends);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "TextureKbytes", message.texture_kbytes);
                    m_statsTracker.LogEntry(timestamp, message.SessionID, "WorldKbytes", message.world_kbytes);

                    // Update constant values for this session
                    m_statsTracker.SetValue(message.SessionID, "AgentLanguage", message.AgentLanguage);
                    m_statsTracker.SetValue(message.SessionID, "AgentRuntime", message.AgentRuntime);
                    m_statsTracker.SetValue(message.SessionID, "AgentStartTime", message.AgentStartTime);
                    m_statsTracker.SetValue(message.SessionID, "AgentVersion", message.AgentVersion);
                    m_statsTracker.SetValue(message.SessionID, "SystemCPU", message.SystemCPU);
                    m_statsTracker.SetValue(message.SessionID, "SystemGPU", message.SystemGPU);
                    m_statsTracker.SetValue(message.SessionID, "SystemGPUClass", message.SystemGPUClass);
                    m_statsTracker.SetValue(message.SessionID, "SystemGPUVendor", message.SystemGPUVendor);
                    m_statsTracker.SetValue(message.SessionID, "SystemGPUVersion", message.SystemGPUVersion);
                    m_statsTracker.SetValue(message.SessionID, "SystemInstalledRam", message.SystemInstalledRam);
                    m_statsTracker.SetValue(message.SessionID, "SystemOS", message.SystemOS);
                    m_statsTracker.SetValue(message.SessionID, "VertexBuffersEnabled", message.VertexBuffersEnabled);
                }
            }
            else
            {
                m_log.Warn("Received invalid data for ViewerStats");
                response.Status = System.Net.HttpStatusCode.BadRequest;
            }
        }

        private void TextureStatsHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            try
            {
                OSD osd = OSDParser.Deserialize(request.Body);
                m_log.Info("Received a TextureStats message: " + osd.ToString());
            }
            catch (Exception)
            {
                m_log.Warn("Failed to decode TextureStats message");
            }
        }
    }
}
