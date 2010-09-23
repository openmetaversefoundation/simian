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
using System.Threading;
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
   [SceneModule("EventQueueGet")]
    public class EventQueueGet : ISceneModule
    {
        /// <summary>The number of milliseconds to wait before the connection times out
        /// and an empty response is sent to the client. This value should be higher
        /// than BATCH_WAIT_INTERVAL for the timeout to function properly</summary>
        const int CONNECTION_TIMEOUT = 1000 * 50;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private IScheduler m_scheduler;
        private LLUDP m_udp;
        private bool m_running;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_scheduler = scene.Simian.GetAppModule<IScheduler>();
            if (m_scheduler == null)
            {
                m_log.Warn("EventQueueManager requires an IScheduler");
                return;
            }

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("Upload requires an IHttpServer");
                return;
            }

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "EventQueueGet", EventQueueHandler);

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_running = true;
                m_scheduler.StartThread(EventQueueManagerThread, "EventQueue Manager (" + scene.Name + ")", ThreadPriority.Normal, false);
            }
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "EventQueueGet");
            }

            if (m_udp != null)
            {
                m_running = false;
            }
        }

        private void EventQueueHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            // Decode the request
            OSD osdRequest = null;

            try { osdRequest = OSDParser.Deserialize(request.Body); }
            catch (Exception) { }

            if (request != null && osdRequest.Type == OSDType.Map)
            {
                OSDMap requestMap = (OSDMap)osdRequest;
                int ack = requestMap["ack"].AsInteger();
                bool done = requestMap["done"].AsBoolean();

                LLAgent agent = null;

                // Fetch an agent reference from either the scene or the LLUDP stack (since the 
                // presence might not exist in the scene yet)
                IScenePresence presence;
                if (m_scene.TryGetPresence(cap.OwnerID, out presence) && presence is LLAgent)
                    agent = (LLAgent)presence;
                else
                    m_udp.TryGetAgent(cap.OwnerID, out agent);

                if (agent != null)
                {
                    if (agent.EventQueue.ConnectionOpen)
                    {
                        m_log.Debug("New connection opened to the event queue for " + agent.Name + " while a previous connection is open. Closing old connection");

                        // If the old connection is still open, queue a signal to close it. Otherwise, just wait for the closed
                        // connection to be detected by the handler thread
                        if (agent.EventQueue.Response != null)
                            agent.EventQueue.EventQueue.Enqueue(null);

                        while (agent.EventQueue.ConnectionOpen)
                            Thread.Sleep(50);

                        m_log.Debug("Old event queue connection closed for " + agent.Name);
                    }

                    if (!done)
                    {
                        //m_log.Debug("Opening event queue connection for " + agent.Name);

                        agent.EventQueue.Context = context;
                        agent.EventQueue.Request = request;
                        agent.EventQueue.Response = response;
                        agent.EventQueue.StartTime = Util.TickCount();
                        agent.EventQueue.ConnectionOpen = true;

                        // ACK sanity checking
                        if (ack != agent.EventQueue.CurrentID - 1 && ack != 0)
                            m_log.WarnFormat("Received an ack for id {0}, last id sent was {1}", ack, agent.EventQueue.CurrentID - 1);
                    }
                    else
                    {
                        m_log.DebugFormat("Shutting down the event queue {0} for {1} at the client's request", request.Uri, agent.Name);
                        agent.EventQueue.SendEvents(50);
                    }
                }
                else
                {
                    m_log.Warn("Received an event queue connection from client " + cap.OwnerID + " that does not have a presence in scene " + m_scene.Name);
                }
            }
            else
            {
                m_log.Warn("Received a request with invalid or missing data at " + request.Uri + ", closing the connection");

                response.Connection = request.Connection;
                response.Status = System.Net.HttpStatusCode.BadRequest;
                response.Send();
            }
        }

        private void EventQueueManagerThread()
        {
            while (m_running)
            {
                bool doSleep = true;

                m_scene.ForEachPresence(
                    delegate(IScenePresence presence)
                    {
                        if (!(presence is LLAgent) || presence.InterestList == null)
                            return;
                        LLAgent agent = (LLAgent)presence;

                        if (agent.EventQueue.ConnectionOpen)
                        {
                            if (agent.EventQueue.EventQueue.Count > 0)
                            {
                                // Set ConnectionOpen to false early so we don't try to send
                                // events on this EQ again before the first call finishes
                                agent.EventQueue.ConnectionOpen = false;
                                m_scheduler.FireAndForget(agent.EventQueue.SendEvents, null);
                                doSleep = false;
                            }
                            else
                            {
                                // Check for a timeout
                                int elapsed = Util.TickCount() - agent.EventQueue.StartTime;
                                if (elapsed >= CONNECTION_TIMEOUT)
                                {
                                    //m_log.DebugFormat("{0}ms passed without an event, timing out the event queue", elapsed);

                                    agent.EventQueue.SendResponse(null);
                                    doSleep = false;
                                }
                            }
                        }
                    }
                );

                if (doSleep)
                    Thread.Sleep(Simian.LONG_SLEEP_INTERVAL);

                m_scheduler.ThreadKeepAlive();
            }

            m_scheduler.RemoveThread();
        }
    }
}
