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
using System.Collections.Generic;
using System.Net;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.WebSocket
{
    [SceneModule("WebSockets")]
    public class WebSockets : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        /// <summary>Reference to the scene that owns this server</summary>
        internal IScene Scene;
        /// <summary>Reference to a thread scheduler</summary>
        internal IScheduler Scheduler;
        /// <summary>Server that handles the actual socket connections and 
        /// sending/receiving of data</summary>
        internal WebSocketServer Server;
        /// <summary>Collection of message handling callbacks</summary>
        internal MessageEventDictionary MessageEvents;
        
        public void Start(IScene scene)
        {
            Scene = scene;

            Scheduler = scene.Simian.GetAppModule<IScheduler>();
            if (Scheduler == null)
            {
                m_log.Error("Cannot start WebSockets without an IScheduler");
                throw new InvalidOperationException();
            }

            MessageEvents = new MessageEventDictionary(Scheduler);

            try
            {
                Server = new WebSocketServer(this);
                Server.Connected += ConnectedHandler;
                Server.Disconnected += DisconnectedHandler;
                Server.DataReceived += DataReceivedHandler;

                Server.Start(12000, "http://localhost:12000", "ws://localhost:12000/");
            }
            catch (Exception ex)
            {
                m_log.Error("WebSocket server failed to start: " + ex.Message);
            }
        }

        public void Stop()
        {
            if (Server != null)
            {
                Server.Stop();
                Server = null;
            }
        }

        public void AddMessageHandler(string messageType, MessageCallback eventHandler)
        {
            MessageEvents.RegisterEvent(messageType, eventHandler);
        }

        public void RemoveMessageHandler(string messageType, MessageCallback eventHandler)
        {
            MessageEvents.UnregisterEvent(messageType, eventHandler);
        }

        public void SendMessage(WSAgent agent, OSDMap message, ThrottleCategory category)
        {
            Server.SendMessage(agent, message, category);
        }

        public void BroadcastMessage(OSDMap message, ThrottleCategory category)
        {
            Server.BroadcastMessage(message, category);
        }

        #region Web Socket Handlers

        private void ConnectedHandler(WSAgent agent)
        {
            m_log.Debug("Agent connected from " + agent.Socket.RemoteEndPoint);
        }

        private void DisconnectedHandler(WSAgent agent)
        {
            m_log.Debug("Agent disconnected from " + agent.Socket.RemoteEndPoint);
        }

        private void DataReceivedHandler(WSAgent agent, string data)
        {
            m_log.Debug("Agent from " + agent.Socket.RemoteEndPoint + " sent message \"" + data + "\"");

            OSDMap map = null;
            try { map = OSDParser.DeserializeJson(data) as OSDMap; }
            catch (Exception ex)
            {
                m_log.Error("Failed to deserialize message: " + ex.Message);
            }

            if (map != null)
            {
                string messageType = map["message"].AsString();
                // TODO: Stuff incoming messages in a blocking queue instead of
                // directly firing the handler. Otherwise, our semaphore will 
                // start blocking IOCP threads
                MessageEvents.BeginRaiseEvent(map, agent);
            }
        }

        #endregion Web Socket Handler
    }
}
