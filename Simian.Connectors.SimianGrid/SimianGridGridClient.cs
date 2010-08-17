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
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Connectors.Remote
{
    [ApplicationModule("SimianGridGridClient")]
    public class SimianGridGridClient : IGridClient, IApplicationModule
    {
        private const double CACHE_TIMEOUT = 60.0d * 1.0d;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private ISceneFactory m_sceneFactory;
        private IHttpServer m_httpServer;
        private IAssetClient m_assetClient;
        private string m_serverUrl;
        private ExpiringCache<UUID, SceneInfo> m_sceneCache = new ExpiringCache<UUID, SceneInfo>();

        public bool Start(Simian simian)
        {
            m_simian = simian;
            m_httpServer = simian.GetAppModule<IHttpServer>();
            m_assetClient = simian.GetAppModule<IAssetClient>();

            m_sceneFactory = simian.GetAppModule<ISceneFactory>();
            if (m_sceneFactory != null)
            {
                m_sceneFactory.OnSceneStart += SceneStartHandler;
                m_sceneFactory.OnSceneStop += SceneStopHandler;
            }

            IConfigSource source = simian.Config;

            IConfig config = source.Configs["SimianGrid"];
            if (config != null)
                m_serverUrl = config.GetString("GridService", null);

            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SimianGrid] config section is missing the GridService URL");
                return false;
            }

            return true;
        }

        public void Stop()
        {
            if (m_sceneFactory != null)
            {
                m_sceneFactory.OnSceneStart -= SceneStartHandler;
                m_sceneFactory.OnSceneStop -= SceneStopHandler;
            }
        }

        #region IGridClient Members

        public bool TryGetScene(UUID sceneID, out SceneInfo sceneInfo)
        {
            // Cache check
            if (m_sceneCache.TryGetValue(sceneID, out sceneInfo))
                return true;

            // Remote fetch
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "SceneID", sceneID.ToString() },
                { "Enabled", "1" }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                sceneInfo = ResponseToSceneInfo(response);
                m_sceneCache.AddOrUpdate(sceneID, sceneInfo, CACHE_TIMEOUT);
                return true;
            }
            else
            {
                m_log.Warn("Grid service did not find a match for region " + sceneID);
                sceneInfo = null;
                return false;
            }
        }

        public bool TryGetSceneAt(Vector3d position, bool onlyEnabled, out SceneInfo sceneInfo)
        {
            if (TryGetSceneNear(position, onlyEnabled, out sceneInfo))
            {
                Vector3d minPosition = sceneInfo.MinPosition;
                Vector3d maxPosition = sceneInfo.MaxPosition;

                // Point-AABB test
                if (minPosition.X <= position.X && position.X <= maxPosition.X &&
                    minPosition.Y <= position.Y && position.Y <= maxPosition.Y &&
                    minPosition.Z <= position.Z && position.Z <= maxPosition.Z)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetSceneNear(Vector3d position, bool onlyEnabled, out SceneInfo sceneInfo)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "Position", position.ToString() },
                { "FindClosest", "1" }
            };
            if (onlyEnabled)
                requestArgs["Enabled"] = "1";

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                sceneInfo = ResponseToSceneInfo(response);
                m_sceneCache.AddOrUpdate(sceneInfo.ID, sceneInfo, CACHE_TIMEOUT);
                return true;
            }
            else
            {
                m_log.Warn("Grid service did not find a match for region at " + position);
                sceneInfo = null;
                return false;
            }
        }

        public bool TryGetRegionRange(Vector3d minPosition, Vector3d maxPosition, out IList<SceneInfo> scenes)
        {
            scenes = new List<SceneInfo>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScenes" },
                { "MinPosition", minPosition.ToString() },
                { "MaxPosition", maxPosition.ToString() },
                { "Enabled", "1" }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                OSDArray array = response["Scenes"] as OSDArray;
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        SceneInfo scene = ResponseToSceneInfo(array[i] as OSDMap);
                        if (scene != null)
                        {
                            m_sceneCache.AddOrUpdate(scene.ID, scene, CACHE_TIMEOUT);
                            scenes.Add(scene);
                        }
                    }
                }

                return true;
            }

            return false;
        }

        public SceneInfo[] SearchScenes(string query, int maxNumber, bool onlyEnabled)
        {
            List<SceneInfo> foundScenes = new List<SceneInfo>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScenes" },
                { "NameQuery", query },
                { "Enabled", "1" }
            };
            if (maxNumber > 0)
                requestArgs["MaxNumber"] = maxNumber.ToString();
            if (onlyEnabled)
                requestArgs["Enabled"] = "1";

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
            {
                OSDArray array = response["Scenes"] as OSDArray;
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        SceneInfo scene = ResponseToSceneInfo(array[i] as OSDMap);
                        // Skip caching for name-based searches
                        if (scene != null)
                            foundScenes.Add(scene);
                    }
                }
            }

            return foundScenes.ToArray();
        }

        #endregion IGridClient Members

        private void SceneStartHandler(IScene scene)
        {
            #region Scene Registration

            Uri publicSeedCap;
            if (scene.TryGetPublicCapability("public_region_seed_capability", out publicSeedCap))
                scene.ExtraData["PublicSeedCapability"] = OSD.FromUri(publicSeedCap);
            else
                m_log.Warn("Registering scene " + scene.Name + " with the grid service without a public seed capability");

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddScene" },
                { "SceneID", scene.ID.ToString() },
                { "Name", scene.Name },
                { "MinPosition", scene.MinPosition.ToString() },
                { "MaxPosition", scene.MaxPosition.ToString() },
                { "Address", m_httpServer.HttpAddress.AbsoluteUri },
                { "Enabled", "1" },
                { "ExtraData", OSDParser.SerializeJsonString(scene.ExtraData) }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (!response["Success"].AsBoolean())
                m_log.Warn("Region registration for " + scene.Name + " failed: " + response["Message"].AsString());

            #endregion Scene Registration
        }

        private void SceneStopHandler(IScene scene)
        {
            #region Scene Deregistration

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddScene" },
                { "SceneID", scene.ID.ToString() },
                { "Enabled", "0" }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (!response["Success"].AsBoolean())
                m_log.Warn("Region deregistration for " + scene.Name + " failed: " + response["Message"].AsString());

            #endregion Scene Deregistration
        }

        private SceneInfo ResponseToSceneInfo(OSDMap response)
        {
            if (response == null)
                return null;

            OSDMap extraData = response["ExtraData"] as OSDMap;
            Uri publicSeedCapability = (extraData != null) ? extraData["PublicSeedCapability"].AsUri() : null;

            SceneInfo scene = new SceneInfo
            {
                ID = response["SceneID"].AsUUID(),
                MinPosition = response["MinPosition"].AsVector3d(),
                MaxPosition = response["MaxPosition"].AsVector3d(),
                Name = response["Name"].AsString(),
                PublicSeedCapability = publicSeedCapability
            };

            return scene;
        }
    }
}
