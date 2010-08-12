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
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;

namespace Simian.Protocols.Linden
{
    [SceneModule("Voice")]
    public class Voice : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("Voice requires an IHttpServer");
                return;
            }

            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "ParcelVoiceInfoRequest", ParcelVoiceInfoRequestHandler);
            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "ProvisionVoiceAccountRequest", ProvisionVoiceAccountRequestHandler);
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "ParcelVoiceInfoRequest");
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "ProvisionVoiceAccountRequest");
            }
        }

        private void ParcelVoiceInfoRequestHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            ParcelVoiceInfoRequestMessage message;
            if (LLUtil.TryGetMessage<ParcelVoiceInfoRequestMessage>(request.Body, out message))
            {
                m_log.DebugFormat("ParcelVoiceInfoRequest: RegionName={0}, ParcelID={1}, SipChannelUri={2}",
                    message.RegionName, message.ParcelID, message.SipChannelUri);
            }
            else
            {
                m_log.Warn("Received invalid request data for ParcelVoiceInfoRequest");
                response.Status = System.Net.HttpStatusCode.BadRequest;
            }
        }

        private void ProvisionVoiceAccountRequestHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            ProvisionVoiceAccountRequestMessage reply = new ProvisionVoiceAccountRequestMessage();
            reply.Username = String.Empty;
            reply.Password = String.Empty;

            // TODO: Implement this once we have Freeswitch support
            //LLUtil.SendLLSDXMLResponse(response, reply.Serialize());
        }
    }
}
