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
using System.Net;
using System.Web;
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("RezAvatar")]
    public class RezAvatar : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private LLUDP m_lludp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("RezAvatar requires an IHttpServer");
                return;
            }

            m_lludp = scene.GetSceneModule<LLUDP>();
            if (m_lludp == null)
            {
                m_log.Error("Can't create the RegionDomain service without an LLUDP server");
                return;
            }

            string urlFriendlySceneName = WebUtil.UrlEncode(m_scene.Name);
            string regionPath = "/regions/" + urlFriendlySceneName;

            m_httpServer.AddHandler("POST", null, regionPath + "/rez_avatar/request", true, true, RezAvatarRequestHandler);
            m_scene.AddPublicCapability("rez_avatar/request", m_httpServer.HttpAddress.Combine(regionPath + "/rez_avatar/request"));
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                m_scene.RemovePublicCapability("rez_avatar/request");
                m_httpServer.RemoveHandlers(RezAvatarRequestHandler);
            }
        }

        private void RezAvatarRequestHandler(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            OSDMap requestMap = null;
            try { requestMap = OSDParser.Deserialize(request.Body) as OSDMap; }
            catch (Exception ex)
            {
                m_log.Warn("Failed to decode rez_avatar/request message: " + ex.Message);
                response.Status = HttpStatusCode.BadRequest;
                return;
            }

            Vector3 startPosition = requestMap["position"].AsVector3();
            Vector3 velocity = requestMap["velocity"].AsVector3();
            Vector3 lookAt = Vector3.UnitX;
            bool childAgent;

            User user = BuildUserFromRezAvatarRequest(requestMap, out childAgent);
            OSDMap responseMap = new OSDMap();

            if (user != null)
            {
                if (!childAgent)
                {
                    // Set the agent velocity in case this is a child->root upgrade (border cross)
                    IScenePresence presence;
                    if (m_scene.TryGetPresence(user.ID, out presence) && presence is IPhysicalPresence)
                        ((IPhysicalPresence)presence).Velocity = velocity;

                    RezRootAgent(user, startPosition, lookAt, ref responseMap);
                }
                else
                {
                    RezChildAgent(user, startPosition, lookAt, ref responseMap);
                }
            }
            else
            {
                responseMap["message"] = OSD.FromString("Invalid or incomplete request");
            }

            WebUtil.SendJSONResponse(response, responseMap);
        }

        private void RezRootAgent(User user, Vector3 startPosition, Vector3 lookAt, ref OSDMap responseMap)
        {
            Uri seedCapability;

            if (startPosition == Vector3.Zero)
            {
                m_log.Info(m_scene.Name + ": rez_avatar/request did not contain a position, setting to default");
                startPosition = new Vector3((m_scene.MaxPosition - m_scene.MinPosition) * 0.5d);
                startPosition.Z = 0f;
            }

            if (m_scene.CanPresenceEnter(user.ID, ref startPosition, ref lookAt))
            {
                user.LastPosition = m_scene.MinPosition + new Vector3d(startPosition);
                user.LastLookAt = lookAt;

                int circuitCode = user.GetField("circuit_code").AsInteger();

                if (m_lludp.EnableCircuit(circuitCode, user, startPosition, lookAt, false, out seedCapability))
                {
                    m_log.Info(m_scene.Name + ": rez_avatar/request enabled circuit " + circuitCode + " for root agent " + user.Name + " with seed cap " + seedCapability);
                    FillOutRezAgentResponse(user, seedCapability, startPosition, lookAt, ref responseMap);
                }
                else
                {
                    m_log.Debug("Unable to enable root agent circuit for " + user.Name);
                    responseMap["message"] = OSD.FromString("Failed to enable root circuit");
                }
            }
            else
            {
                m_log.Warn("Denied rez_avatar/request, user=" + user);
                responseMap["message"] = OSD.FromString("Access denied");
            }
        }

        private void RezChildAgent(User user, Vector3 startPosition, Vector3 lookAt, ref OSDMap responseMap)
        {
            if (m_scene.CanPresenceSee(user.ID))
            {
                Uri seedCapability;

                user.LastPosition = m_scene.MinPosition + new Vector3d(startPosition);
                user.LastLookAt = lookAt;

                int circuitCode = user.GetField("circuit_code").AsInteger();

                if (m_lludp.EnableCircuit(circuitCode, user, startPosition, lookAt, true, out seedCapability))
                {
                    m_log.Info("rez_avatar/request enabled circuit " + circuitCode + " for child agent " + user.Name + " in " + m_scene.Name);
                    FillOutRezAgentResponse(user, seedCapability, startPosition, lookAt, ref responseMap);
                }
                else
                {
                    m_log.Debug("Unable to enable child agent circuit for " + user.Name);
                    responseMap["message"] = OSD.FromString("Failed to enable child circuit");
                }
            }
            else
            {
                m_log.Warn("Denied rez_avatar/request, user=" + user);
                responseMap["message"] = OSD.FromString("Access denied");
            }
        }

        private void FillOutRezAgentResponse(User user, Uri seedCapability, Vector3 startPosition, Vector3 lookAt, ref OSDMap responseMap)
        {
            IPAddress externalAddress = m_lludp.MasqueradeAddress ?? m_lludp.Address;
            uint regionX, regionY;
            GetRegionXY(m_scene.MinPosition, out regionX, out regionY);

            responseMap["connect"] = OSD.FromBoolean(true);
            responseMap["agent_id"] = OSD.FromUUID(user.ID);
            responseMap["sim_host"] = OSD.FromString(externalAddress.ToString());
            responseMap["sim_port"] = OSD.FromInteger(m_lludp.Port);
            responseMap["region_seed_capability"] = OSD.FromUri(seedCapability);
            responseMap["position"] = OSD.FromVector3(startPosition);
            responseMap["look_at"] = OSD.FromVector3(lookAt);

            // Region information
            responseMap["region_id"] = OSD.FromUUID(m_scene.ID);
            responseMap["region_x"] = OSD.FromInteger(regionX);
            responseMap["region_y"] = OSD.FromInteger(regionY);
        }

        private User BuildUserFromRezAvatarRequest(OSDMap request, out bool childAgent)
        {
            childAgent = request["child"].AsBoolean();

            if (request == null)
                return null;

            User user = new User();
            user.ID = request["agent_id"].AsUUID();
            user.Name = (request["first_name"].AsString() + ' ' + request["last_name"].AsString()).Trim();
            user.SessionID = request["session_id"].AsUUID();
            user.SecureSessionID = request["secure_session_id"].AsUUID();
            user.SetField("circuit_code", request["circuit_code"]);
            user.AccessLevel = (byte)request["access_level"].AsInteger();

            if (user.ID != UUID.Zero && !String.IsNullOrEmpty(user.Name) && user.GetField("circuit_code").AsInteger() != 0)
                return user;
            else
                return null;
        }

        private void GetRegionXY(Vector3d position, out uint regionX, out uint regionY)
        {
            regionX = (uint)position.X;
            regionY = (uint)position.Y;
        }
    }
}
