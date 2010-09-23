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
using System.Net;
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("SeedCapability")]
    public class SeedCapability : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private IUserClient m_userClient;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("SeedCapability requires an IHttpServer");
                return;
            }

            m_userClient = m_scene.Simian.GetAppModule<IUserClient>();
            if (m_userClient == null)
            {
                m_log.Warn("SeedCapability requires an IUserClient");
                return;
            }

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "region_seed_capability", SeedCapabilityHandler);
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "region_seed_capability");
            }
        }

        private void SeedCapabilityHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            OSDArray seedCapRequest = null;

            try { seedCapRequest = OSDParser.Deserialize(request.Body) as OSDArray; }
            catch (Exception ex)
            {
                m_log.Warn("Failed to deserialize incoming seed capability request: " + ex.Message);
                response.Status = HttpStatusCode.BadRequest;
                return;
            }

            if (seedCapRequest != null)
            {
                User user;
                if (m_userClient.TryGetUser(cap.OwnerID, out user))
                {
                    //m_log.Debug("Received a seed capability request from " + user.Name + ": " + OSDParser.SerializeJsonString(seedCapRequest));

                    // Put all of the requested capabilities into a dictionary
                    Dictionary<string, Uri> capabilities = new Dictionary<string, Uri>(seedCapRequest.Count);
                    for (int i = 0; i < seedCapRequest.Count; i++)
                        capabilities[seedCapRequest[i].AsString()] = null;

                    // Special handling for EventQueueGet since we set "sendResponseAfterCallback=false" for it
                    m_scene.Capabilities.TryAssignCapability(user.ID, capabilities, false, m_scene.ID, "EventQueueGet");

                    // Create all of the capabilities we support, if they were requested
                    List<string> capNames = new List<string>(capabilities.Keys);
                    foreach (string capName in capNames)
                        m_scene.Capabilities.TryAssignCapability(user.ID, capabilities, true, m_scene.ID, capName);

                    // Build the response
                    OSDMap responseMap = new OSDMap(capabilities.Count);
                    foreach (KeyValuePair<string, Uri> kvp in capabilities)
                    {
                        if (kvp.Value != null)
                            responseMap[kvp.Key] = OSD.FromUri(kvp.Value);
                    }

                    m_log.Debug("Returning " + responseMap.Count + " capabilities to " + user.Name + " from the seed capability");

                    // Send the response
                    WebUtil.SendXMLResponse(response, responseMap);
                }
                else
                {
                    m_log.Warn("Received a seed capability request from an unknown agent " + cap.OwnerID);
                    response.Status = HttpStatusCode.NotFound;
                }
            }
            else
            {
                m_log.Warn("Failed to deserialize incoming seed capability request");
                response.Status = HttpStatusCode.BadRequest;
            }
        }
    }
}
