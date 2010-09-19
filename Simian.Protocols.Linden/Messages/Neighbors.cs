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
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("Neighbors")]
    public class Neighbors : ISceneModule
    {
        const float BORDER_CROSS_THRESHOLD = 5.0f;
        const int BORDER_CROSS_THROTTLE_MS = 2000;

        private static Vector3d NEIGHBOR_RADIUS = new Vector3d(128.0d, 128.0d, 128.0d);

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IScheduler m_scheduler;
        private IHttpServer m_httpServer;
        private LLUDP m_udp;
        private ISceneFactory m_sceneFactory;
        private IUserClient m_userClient;
        private IGridClient m_gridClient;
        private AABB m_borderCrossAABB;
        private ThrottledQueue<uint, IScenePresence> m_childUpdates;
        private Dictionary<uint, Vector3> m_lastCameraPositions;
        private Dictionary<UUID, int> m_borderCrossThrottles;

        public void Start(IScene scene)
        {
            m_scene = scene;
            m_lastCameraPositions = new Dictionary<uint, Vector3>();
            m_borderCrossThrottles = new Dictionary<UUID, int>();

            // Create an AABB for this scene that extends beyond the borders by BORDER_CROSS_THRESHOLD
            // that is used to check for border crossings
            m_borderCrossAABB = new AABB(Vector3.Zero, new Vector3(scene.MaxPosition - scene.MinPosition));
            m_borderCrossAABB.Min -= new Vector3(BORDER_CROSS_THRESHOLD, BORDER_CROSS_THRESHOLD, BORDER_CROSS_THRESHOLD);
            m_borderCrossAABB.Max += new Vector3(BORDER_CROSS_THRESHOLD, BORDER_CROSS_THRESHOLD, BORDER_CROSS_THRESHOLD);

            m_scheduler = m_scene.Simian.GetAppModule<IScheduler>();
            if (m_scheduler == null)
            {
                m_log.Warn("Neighbors requires an IScheduler");
                return;
            }

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("Neighbors requires an IHttpServer");
                return;
            }

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp == null)
            {
                m_log.Warn("Neighbors requires an LLUDP");
                return;
            }

            m_userClient = m_scene.Simian.GetAppModule<IUserClient>();
            if (m_userClient == null)
            {
                m_log.Warn("Neighbors requires an IUserClient");
                return;
            }

            m_gridClient = scene.Simian.GetAppModule<IGridClient>();

            // Add neighbor messaging handlers
            string urlFriendlySceneName = WebUtil.UrlEncode(scene.Name);
            string regionPath = "/regions/" + urlFriendlySceneName;
            m_httpServer.AddHandler("POST", null, regionPath + "/region/online", true, true, RegionOnlineHandler);
            m_httpServer.AddHandler("POST", null, regionPath + "/region/offline", true, true, RegionOfflineHandler);
            m_httpServer.AddHandler("POST", null, regionPath + "/child_avatar/update", true, true, ChildAvatarUpdateHandler);

            m_scene.AddPublicCapability("region/online", m_httpServer.HttpAddress.Combine(regionPath + "/region/online"));
            m_scene.AddPublicCapability("region/offline", m_httpServer.HttpAddress.Combine(regionPath + "/region/offline"));
            m_scene.AddPublicCapability("child_avatar/update", m_httpServer.HttpAddress.Combine(regionPath + "/child_avatar/update"));

            // Track local scenes going up and down
            m_sceneFactory = scene.Simian.GetAppModule<ISceneFactory>();
            if (m_sceneFactory != null)
            {
                m_sceneFactory.OnSceneStart += SceneStartHandler;
                m_sceneFactory.OnSceneStop += SceneStopHandler;
            }

            m_scene.OnPresenceAdd += PresenceAddHandler;
            m_scene.OnEntityAddOrUpdate += EntityAddOrUpdateHandler;

            m_childUpdates = new ThrottledQueue<uint, IScenePresence>(5.0f, 200, true, SendChildUpdate);
            m_childUpdates.Start();
        }

        public void Stop()
        {
            m_childUpdates.Stop(false);

            #region region/offline

            // Notify all the neighbors we know about that we're going offline
            if (m_gridClient != null)
            {
                SceneInfo[] neighbors = m_scene.GetNeighbors();
                for (int i = 0; i < neighbors.Length; i++)
                {
                    SendRegionOffline(neighbors[i]);
                }
            }
            else
            {
                m_log.Warn("No IGridClient found, skipping neighbor offline notifications");
            }

            #endregion region/offline

            if (m_httpServer != null)
            {
                // Remove capability handlers
                m_scene.RemovePublicCapability("region/online");
                m_scene.RemovePublicCapability("region/offline");
                m_scene.RemovePublicCapability("child_avatar/update");

                m_httpServer.RemoveHandlers(RegionOnlineHandler);
                m_httpServer.RemoveHandlers(RegionOfflineHandler);
                m_httpServer.RemoveHandlers(ChildAvatarUpdateHandler);
            }

            if (m_sceneFactory != null)
            {
                m_sceneFactory.OnSceneStart -= SceneStartHandler;
                m_sceneFactory.OnSceneStop -= SceneStopHandler;
            }

            m_scene.OnPresenceAdd -= PresenceAddHandler;
            m_scene.OnEntityAddOrUpdate -= EntityAddOrUpdateHandler;

            m_lastCameraPositions.Clear();
        }

        #region Local Scene Start/Stop

        private void SceneStartHandler(IScene scene)
        {
            if (scene.ID == m_scene.ID)
            {
                #region region/online

                // Fetch nearby regions and notify them we're online
                if (m_gridClient != null)
                {
                    IList<SceneInfo> scenes;
                    if (m_gridClient.TryGetRegionRange(scene.MinPosition - NEIGHBOR_RADIUS, scene.MaxPosition + NEIGHBOR_RADIUS, out scenes))
                    {
                        for (int i = 0; i < scenes.Count; i++)
                        {
                            SceneInfo curScene = scenes[i];

                            // Skip our own scene
                            if (curScene.ID != scene.ID)
                                SendRegionOnline(curScene);
                        }
                    }
                    else
                    {
                        m_log.Warn("Failed to fetch neighbor regions for " + scene.Name);
                    }
                }
                else
                {
                    m_log.Warn("No IGridClient found, skipping neighbor online notifications");
                }

                #endregion region/online
            }
            else
            {
                // Another local scene came online, track it
                m_scene.AddNeighbor(SceneInfo.FromScene(scene));
            }
        }

        private void SceneStopHandler(IScene scene)
        {
            if (scene.ID != m_scene.ID)
            {
                // Another local scene went offline, stop tracking it
                m_scene.RemoveNeighbor(scene.ID);
            }
        }

        #endregion Local Scene Start/Stop

        #region HTTP Handlers

        private void RegionOnlineHandler(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            OSDMap requestMap = null;
            try { requestMap = OSDParser.Deserialize(request.Body) as OSDMap; }
            catch { }

            if (requestMap != null)
            {
                Vector3d minPosition = new Vector3d(requestMap["region_x"].AsReal(), requestMap["region_y"].AsReal(), 0.0d);
                Vector3d maxPosition = new Vector3d(minPosition.X + 256.0d, minPosition.Y + 256.0d, 4096.0d);

                SceneInfo scene = new SceneInfo
                {
                    ID = requestMap["region_id"].AsUUID(),
                    Name = requestMap["region_name"].AsString(),
                    MinPosition = minPosition,
                    MaxPosition = maxPosition,
                    PublicSeedCapability = requestMap["public_region_seed_capability"].AsUri()
                };

                //m_log.Debug(m_scene.Name + " adding neighbor " + scene.Name);
                m_scene.AddNeighbor(scene);
            }
            else
            {
                m_log.Warn("Failed to parse region/online request");
            }
        }

        private void RegionOfflineHandler(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            OSDMap requestMap = null;
            try { requestMap = OSDParser.Deserialize(request.Body) as OSDMap; }
            catch { }

            if (requestMap != null)
            {
                UUID regionID = requestMap["region_id"].AsUUID();

                m_log.Debug(m_scene.Name + " removing neighbor " + regionID);
                m_scene.RemoveNeighbor(regionID);
            }
        }

        private void ChildAvatarUpdateHandler(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            OSDMap requestMap = null;
            try { requestMap = OSDParser.Deserialize(request.Body) as OSDMap; }
            catch { }

            if (requestMap != null)
            {
                IScenePresence child;
                if (m_scene.TryGetPresence(requestMap["agent_id"].AsUUID(), out child) && child.IsChildPresence)
                {
                    child.RelativePosition = requestMap["position"].AsVector3();
                    child.RelativeRotation = requestMap["rotation"].AsQuaternion();

                    if (child is LLAgent)
                    {
                        LLAgent childAgent = (LLAgent)child;

                        childAgent.CameraPosition = requestMap["camera_center"].AsVector3();
                        childAgent.CameraAtAxis = requestMap["camera_at"].AsVector3();
                        childAgent.CameraLeftAxis = requestMap["camera_left"].AsVector3();
                        childAgent.CameraUpAxis = requestMap["camera_up"].AsVector3();
                        childAgent.DrawDistance = (float)requestMap["draw_distance"].AsReal();
                    }
                }
            }
        }

        #endregion HTTP Handlers

        private bool CheckForBorderCrossing(ISceneEntity entity, out SceneInfo neighbor)
        {
            Vector3 pos = entity.ScenePosition;

            if (!m_borderCrossAABB.Intersects(pos))
            {
                // We are outside the scene AABB plus threshold, test if we entered into a neighbor
                // scene
                SceneInfo[] closestNeighbors = m_scene.GetNeighborsNear(m_scene.MinPosition + new Vector3d(pos), 0.1);
                if (closestNeighbors.Length > 0 && closestNeighbors[0].ID != m_scene.ID)
                {
                    neighbor = closestNeighbors[0];
                    return true;
                }
            }

            neighbor = null;
            return false;
        }

        private bool CheckForCameraMovement(ISceneEntity entity)
        {
            const float CAMERA_MOVE_THRESHOLD = 10f;

            if (entity is LLAgent)
            {
                LLAgent agent = (LLAgent)entity;

                // Calculate the center of the far frustum plane
                Vector3 camPosition = agent.CameraPosition + agent.CameraAtAxis * agent.DrawDistance;

                lock (m_lastCameraPositions)
                {
                    Vector3 lastCamPos;
                    if (!m_lastCameraPositions.TryGetValue(agent.LocalID, out lastCamPos))
                        lastCamPos = Vector3.Zero;

                    if (Vector3.DistanceSquared(camPosition, lastCamPos) > CAMERA_MOVE_THRESHOLD * CAMERA_MOVE_THRESHOLD)
                    {
                        m_lastCameraPositions[entity.LocalID] = camPosition;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool BorderCrossLLAgent(LLAgent agent, SceneInfo neighbor)
        {
            IPAddress simHost;
            int simPort;
            Uri seedCapability;

            // Send a rez_avatar/request message to create a root agent or upgrade a child agent
            if (SendRezAvatarRequest(agent, neighbor, false, out simHost, out simPort, out seedCapability))
            {
                // Demote this agent to a child agent
                agent.IsChildPresence = true;

                // Calculate our position relative to the new region
                Vector3 relativePosition = agent.ScenePosition - new Vector3(neighbor.MinPosition - m_scene.MinPosition);

                // Send the CrossedRegion message over the event queue to the client
                CrossedRegionMessage crossed = new CrossedRegionMessage();
                crossed.AgentID = agent.ID;
                crossed.SessionID = agent.SessionID;
                crossed.RegionHandle = Util.PositionToRegionHandle(neighbor.MinPosition);
                crossed.IP = simHost;
                crossed.Port = simPort;
                crossed.Position = relativePosition;
                crossed.LookAt = agent.CameraAtAxis; // TODO: Get LookAt from the agent's rotation
                crossed.SeedCapability = seedCapability;

                m_log.Info("Sending CrossedRegion to " + agent.Name + " from " + m_scene.Name + " to " + neighbor.Name +
                    ", pos=" + relativePosition + ", vel=" + agent.Velocity);
                agent.EventQueue.QueueEvent("CrossedRegion", crossed.Serialize());

                return true;
            }
            else
            {
                m_log.Warn("Border crossing " + agent.Name + " from " + m_scene.Name + " to " + neighbor.Name +
                    " failed, rez_avatar/request did not succeed");

                return false;
            }
        }

        private bool BorderCrossEntity(ISceneEntity entity, SceneInfo neighbor)
        {
            return false;
        }

        private void PresenceAddHandler(object sender, PresenceArgs e)
        {
            m_scheduler.FireAndForget(
                delegate(object o)
                {
                    // The default search distance (in every direction) for child agent connections
                    const float NEIGHBOR_SEARCH_MARGIN = 256.0f;

                    // HACK: Attempted fix for the viewer hanging indefinitely at login. It seems like
                    // a client race condition when establishing neighbor connections too quickly
                    System.Threading.Thread.Sleep(1000 * 5);

                    if (e.Presence is LLAgent && !e.Presence.IsChildPresence)
                    {
                        LLAgent agent = (LLAgent)e.Presence;

                        // Fetch nearby neighbors for the new presence
                        // TODO: We should be doing this later, based off draw distance
                        Vector3d globalPosition = e.Presence.Scene.MinPosition + new Vector3d(e.Presence.ScenePosition);
                        SceneInfo[] nearNeighbors = m_scene.GetNeighborsNear(globalPosition, e.Presence.InterestRadius + NEIGHBOR_SEARCH_MARGIN);

                        // Iterate over all of the given neighbors and send each a rez_avatar/request to create a child agent
                        for (int i = 0; i < nearNeighbors.Length; i++)
                        {
                            SendRezAvatarRequest(agent, nearNeighbors[i], true);
                        }
                    }
                },
                null
            );
        }

        private void EntityAddOrUpdateHandler(object sender, EntityAddOrUpdateArgs e)
        {
            if (e.UpdateFlags.HasFlag(UpdateFlags.Position))
            {
                // Ignore child entities
                ISceneEntity entity = e.Entity;
                if (entity is ILinkable && ((ILinkable)entity).Parent != null)
                    return;

                // Ignore child agents
                if (entity is IScenePresence && ((IScenePresence)entity).IsChildPresence)
                    return;

                // Check if we are currently throttling border crossing attempts for this entity
                bool borderCrossThrottled = false;
                lock (m_borderCrossThrottles)
                {
                    int tickCount;
                    if (m_borderCrossThrottles.TryGetValue(entity.ID, out tickCount))
                    {
                        int now = Util.TickCount();
                        if (tickCount > now)
                            borderCrossThrottled = true;
                        else
                            m_borderCrossThrottles.Remove(entity.ID);
                    }
                }

                SceneInfo neighbor;
                if (!borderCrossThrottled && CheckForBorderCrossing(entity, out neighbor))
                {
                    bool success;

                    if (entity is LLAgent)
                        success = BorderCrossLLAgent((LLAgent)entity, neighbor);
                    else
                        success = BorderCrossEntity(entity, neighbor);

                    if (success)
                    {
                        m_log.Debug(entity.Name + " border crossed to " + neighbor.Name + " @ " + entity.ScenePosition);
                    }
                    else
                    {
                        // Add a throttle for border crossing this entity
                        lock (m_borderCrossThrottles)
                            m_borderCrossThrottles[entity.ID] = Util.TickCount() + BORDER_CROSS_THROTTLE_MS;
                    }
                }
                else if (entity is IScenePresence && CheckForCameraMovement(entity))
                {
                    m_childUpdates.Add(entity.LocalID, (IScenePresence)entity);
                }
            }
        }

        #region Message Senders

        private void SendRegionOnline(SceneInfo neighbor)
        {
            // Build the region/online message
            uint regionX, regionY;
            GetRegionXY(m_scene.MinPosition, out regionX, out regionY);

            OSDMap regionOnline = new OSDMap
            {
                { "region_id", OSD.FromUUID(m_scene.ID) },
                { "region_name", OSD.FromString(m_scene.Name) },
                { "region_x", OSD.FromInteger(regionX) },
                { "region_y", OSD.FromInteger(regionY) }
            };

            // Put our public region seed capability into the message
            Uri publicSeedCap;
            if (m_scene.TryGetPublicCapability("public_region_seed_capability", out publicSeedCap))
                regionOnline["public_region_seed_capability"] = OSD.FromUri(publicSeedCap);
            else
                m_log.Warn("Registering scene " + m_scene.Name + " with neighbor " + neighbor.Name + " without a public seed capability");

            // Send the hello notification
            Uri regionOnlineCap;
            if (neighbor.TryGetCapability("region/online", out regionOnlineCap))
            {
                try
                {
                    UntrustedHttpWebRequest.PostToUntrustedUrl(regionOnlineCap, OSDParser.SerializeJsonString(regionOnline));
                    //m_log.Debug(scene.Name + " sent region/online to " + curScene.Name);
                }
                catch (Exception ex)
                {
                    m_log.Warn(m_scene.Name + " failed to send region/online to " + neighbor.Name + ": " +
                        ex.Message);
                }
            }
            else
            {
                m_log.Warn("No region/online capability found for " + neighbor.Name + ", " +
                    m_scene.Name + " is skipping it");
            }
        }

        private void SendRegionOffline(SceneInfo neighbor)
        {
            // Build the region/offline message
            OSDMap regionOffline = new OSDMap
            {
                { "region_id", OSD.FromUUID(m_scene.ID) }
            };

            Uri regionOfflineCap;
            if (neighbor.TryGetCapability("region/offline", out regionOfflineCap))
            {
                // Send the message
                try
                {
                    UntrustedHttpWebRequest.PostToUntrustedUrl(regionOfflineCap, OSDParser.SerializeJsonString(regionOffline));
                    m_log.Debug(m_scene.Name + " sent region/offline to " + neighbor.Name);
                }
                catch (Exception ex)
                {
                    m_log.Warn(m_scene.Name + " failed to send region/offline to " + neighbor.Name + ": " +
                        ex.Message);
                }
            }
            else
            {
                m_log.Warn("No region/offline capability found for " + neighbor.Name + ", " +
                    m_scene.Name + " is skipping it");
            }
        }

        private void SendChildUpdate(IScenePresence presence)
        {
            const float DEFAULT_DRAW_DISTANCE = 128.0f;

            // Build the template child_avatar/update message
            OSDMap childUpdate = new OSDMap();
            childUpdate["agent_id"] = OSD.FromUUID(presence.ID);
            childUpdate["rotation"] = OSD.FromQuaternion(presence.SceneRotation);

            float drawDistance = DEFAULT_DRAW_DISTANCE;

            if (presence is LLAgent)
            {
                LLAgent agent = (LLAgent)presence;
                drawDistance = agent.DrawDistance;

                childUpdate["camera_center"] = OSD.FromVector3(agent.CameraPosition);
                childUpdate["camera_at"] = OSD.FromVector3(agent.CameraAtAxis);
                childUpdate["camera_left"] = OSD.FromVector3(agent.CameraLeftAxis);
                childUpdate["camera_up"] = OSD.FromVector3(agent.CameraUpAxis);
            }

            childUpdate["draw_distance"] = OSD.FromReal(drawDistance);

            // Get a list of neighbors to send this update to based on the draw distance
            SceneInfo[] neighbors = m_scene.GetNeighborsNear(m_scene.MinPosition + new Vector3d(presence.ScenePosition), drawDistance);
            for (int i = 0; i < neighbors.Length; i++)
            {
                SceneInfo neighbor = neighbors[i];

                // Find the presence position relative to this neighbor
                Vector3 relativePosition = presence.ScenePosition - new Vector3(neighbor.MinPosition - presence.Scene.MinPosition);
                childUpdate["position"] = OSD.FromVector3(relativePosition);

                Uri childUpdateCap;
                if (neighbor.TryGetCapability("child_avatar/update", out childUpdateCap))
                {
                    try
                    {
                        // Send the message
                        //m_log.Debug("Sending child agent update for " + presence.Name);
                        string message = OSDParser.SerializeJsonString(childUpdate);
                        UntrustedHttpWebRequest.PostToUntrustedUrl(childUpdateCap, message);
                    }
                    catch (Exception ex)
                    {
                        m_log.Warn("child_avatar/update from " + m_scene.Name + " to " + neighbor.Name + " for agent " +
                            presence.Name + " failed: " + ex.Message);
                    }
                }
                else
                {
                    // This shouldn't happen since we check for the child_avatar/update capability 
                    // before adding this agent/neighbor pair to the queue
                    throw new InvalidOperationException("child_avatar/update capability not found in SendChildUpdate handler");
                }
            }
        }

        private bool SendRezAvatarRequest(LLAgent agent, SceneInfo neighbor, bool isChild)
        {
            IPAddress simHost;
            int simPort;
            Uri seedCapability;

            return SendRezAvatarRequest(agent, neighbor, isChild, out simHost, out simPort, out seedCapability);
        }

        private bool SendRezAvatarRequest(LLAgent agent, SceneInfo neighbor, bool isChild, out IPAddress simHost, out int simPort, out Uri seedCapability)
        {
            simHost = null;
            simPort = 0;
            seedCapability = null;

            Uri rezAvatarRequestCap;
            if (neighbor.TryGetCapability("rez_avatar/request", out rezAvatarRequestCap))
            {
                string firstName, lastName;
                Util.GetFirstLastName(agent.Name, out firstName, out lastName);

                // Find the presence position relative to this neighbor
                Vector3 relativePosition = agent.ScenePosition - new Vector3(neighbor.MinPosition - m_scene.MinPosition);
                // Calculate the direction this agent is currently facing
                Vector3 lookAt = Vector3.UnitY * agent.RelativeRotation;

                // Create the template rez_avatar/request message
                OSDMap rezAvatarRequest = new OSDMap
                {
                    { "agent_id", OSD.FromUUID(agent.ID) },
                    { "session_id", OSD.FromUUID(agent.SessionID) },
                    { "position", OSD.FromVector3(relativePosition) },
                    { "look_at", OSD.FromVector3(lookAt) },
                    { "velocity", OSD.FromVector3(agent.Velocity) },
                    { "child", OSD.FromBoolean(isChild) }
                };

                OSDMap rezAvatarResponse = null;
                try
                {
                    // Send the message and get a response
                    rezAvatarResponse = OSDParser.Deserialize(UntrustedHttpWebRequest.PostToUntrustedUrl(
                        rezAvatarRequestCap, OSDParser.SerializeJsonString(rezAvatarRequest))) as OSDMap;
                }
                catch { }

                if (rezAvatarResponse != null)
                {
                    return RezChildAgentReplyHandler(agent, rezAvatarResponse, out simHost, out simPort, out seedCapability);
                }
                else
                {
                    m_log.Warn(m_scene.Name + " failed to create a child agent on " + neighbor.Name +
                        ", rez_avatar/request failed");
                }
            }
            else
            {
                m_log.Warn(neighbor.Name + " does not have a rez_avatar/request capability");
            }

            return false;
        }

        #endregion Message Senders

        private bool RezChildAgentReplyHandler(LLAgent agent, OSDMap map, out IPAddress simHost, out int simPort, out Uri seedCapability)
        {
            simHost = null;
            simPort = 0;
            seedCapability = null;

            if (map["connect"].AsBoolean())
            {
                #region Response Parsing

                string simHostStr = map["sim_host"].AsString();
                if (!IPAddress.TryParse(simHostStr, out simHost))
                {
                    m_log.Warn("rez_avatar/response had an invalid sim_host: " + simHostStr);
                    return false;
                }

                simPort = map["sim_port"].AsInteger();
                UUID regionID = map["region_id"].AsUUID();

                uint regionX = map["region_x"].AsUInteger();
                uint regionY = map["region_y"].AsUInteger();
                ulong regionHandle = Utils.UIntsToLong(regionX, regionY);

                seedCapability = map["region_seed_capability"].AsUri();
                if (seedCapability == null)
                {
                    m_log.Warn("rez_avatar/response had an invalid region_seed_capability: " + map["region_seed_capability"].AsString());
                    return false;
                }

                #endregion Response Parsing

                #region EnableSimulator

                EnableSimulatorMessage.SimulatorInfoBlock block = new EnableSimulatorMessage.SimulatorInfoBlock();
                block.IP = simHost;
                block.Port = simPort;
                block.RegionHandle = regionHandle;

                EnableSimulatorMessage enable = new EnableSimulatorMessage();
                enable.Simulators = new EnableSimulatorMessage.SimulatorInfoBlock[1];
                enable.Simulators[0] = block;

                m_log.Debug("Sending EnableSimulator message for scene " + regionID + " to " + agent.Name);
                agent.EventQueue.QueueEvent("EnableSimulator", enable.Serialize());

                #endregion EnableSimulator

                #region EstablishAgentCommunication

                // Send an EstablishAgentCommunication event down to the client to get the neighbor event queue established
                EstablishAgentCommunicationMessage eacMessage = new EstablishAgentCommunicationMessage();
                eacMessage.AgentID = regionID;
                eacMessage.Address = simHost;
                eacMessage.Port = simPort;
                eacMessage.SeedCapability = seedCapability;

                m_log.Debug("Sending EstablishAgentCommunication message for seedcap " + seedCapability + " to " + agent.Name);
                agent.EventQueue.QueueEvent("EstablishAgentCommunication", eacMessage.Serialize());

                #endregion EstablishAgentCommunication

                return true;
            }
            else
            {
                m_log.Warn("rez_avatar/request from " + m_scene.Name + " for child agent " + agent.Name +
                    " failed: " + map["message"].AsString());
                return false;
            }
        }

        private static void GetRegionXY(Vector3d position, out uint regionX, out uint regionY)
        {
            regionX = (uint)position.X;
            regionY = (uint)position.Y;
        }
    }
}
