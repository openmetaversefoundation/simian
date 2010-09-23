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
using System.Drawing;
using System.IO;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Connectors.Standalone
{
    [ApplicationModule("StandaloneGridClient")]
    public class StandaloneGridClient : IGridClient, IApplicationModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private ISceneFactory m_sceneFactory;
        private IAssetClient m_assetClient;
        private IHttpServer m_httpServer;

        public bool Start(Simian simian)
        {
            m_simian = simian;

            m_httpServer = simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Error("StandaloneGridClient requires an IHttpServer");
                return false;
            }

            m_sceneFactory = simian.GetAppModule<ISceneFactory>();
            if (m_sceneFactory == null)
            {
                m_log.Error("StandaloneGridClient requires an ISceneFactory");
                return false;
            }

            m_assetClient = simian.GetAppModule<IAssetClient>();

            return true;
        }

        public void Stop()
        {
        }

        #region IGridClient Members

        public bool TryGetScene(UUID sceneID, out SceneInfo sceneInfo)
        {
            IScene scene;
            if (m_sceneFactory != null && m_sceneFactory.TryGetScene(sceneID, out scene))
            {
                sceneInfo = SceneInfo.FromScene(scene);
                return true;
            }

            sceneInfo = null;
            return false;
        }

        public bool TryGetSceneAt(Vector3d position, bool onlyEnabled, out SceneInfo sceneInfo)
        {
            IScene[] scenes = null;
            if (m_sceneFactory != null)
                scenes = m_sceneFactory.GetScenes();

            if (scenes != null)
            {
                for (int i = 0; i < scenes.Length; i++)
                {
                    IScene scene = scenes[i];
                    Vector3d minPosition = scene.MinPosition;
                    Vector3d maxPosition = scene.MaxPosition;

                    // Point-AABB test
                    if (minPosition.X <= position.X && position.X <= maxPosition.X &&
                        minPosition.Y <= position.Y && position.Y <= maxPosition.Y &&
                        minPosition.Z <= position.Z && position.Z <= maxPosition.Z)
                    {
                        sceneInfo = SceneInfo.FromScene(scene);
                        return true;
                    }
                }
            }

            sceneInfo = null;
            return false;
        }

        public bool TryGetSceneNear(Vector3d position, bool onlyEnabled, out SceneInfo sceneInfo)
        {
            IScene[] scenes = null;
            if (m_sceneFactory != null)
                scenes = m_sceneFactory.GetScenes();

            IScene closestScene = null;
            double minDist = Double.MaxValue;

            if (scenes != null)
            {
                for (int i = 0; i < scenes.Length; i++)
                {
                    IScene scene = scenes[i];

                    // Get the midpoint of this scene
                    Vector3d midpoint = (scene.MaxPosition + scene.MinPosition) * 0.5d;
                    // Compare this distance against the current minimum distance (squared, to avoid an unncessary sqrt)
                    double distance = Vector3d.DistanceSquared(position, midpoint);
                    if (distance < minDist)
                    {
                        minDist = distance;
                        closestScene = scene;
                    }
                }
            }

            if (closestScene != null)
            {
                sceneInfo = SceneInfo.FromScene(closestScene);
                return true;
            }

            sceneInfo = null;
            return false;
        }

        public bool TryGetRegionRange(Vector3d minPosition, Vector3d maxPosition, out IList<SceneInfo> scenes)
        {
            scenes = new List<SceneInfo>();

            IScene[] sceneArray = null;
            if (m_sceneFactory != null)
                sceneArray = m_sceneFactory.GetScenes();

            for (int i = 0; i < sceneArray.Length; i++)
            {
                IScene scene = sceneArray[i];
                Vector3d sceneMin = scene.MinPosition;
                Vector3d sceneMax = scene.MaxPosition;

                // AABB overlap test
                if (sceneMin.X <= maxPosition.X &&
                    sceneMin.Y <= maxPosition.Y &&
                    sceneMin.Z <= maxPosition.Z &&
                    sceneMax.X >= minPosition.X &&
                    sceneMax.Y >= minPosition.Y &&
                    sceneMax.Z >= minPosition.Z)
                {
                    scenes.Add(SceneInfo.FromScene(scene));
                }
            }

            return true;
        }

        public SceneInfo[] SearchScenes(string query, int maxNumber, bool onlyEnabled)
        {
            IScene[] scenes = null;
            if (m_sceneFactory != null)
                scenes = m_sceneFactory.GetScenes();

            SortedList<int, SceneInfo> foundScenes = new SortedList<int, SceneInfo>();

            if (scenes != null)
            {
                for (int i = 0; i < scenes.Length; i++)
                {
                    IScene scene = scenes[i];

                    if (scene.Name.ToLowerInvariant().Contains(query.ToLowerInvariant()))
                    {
                        foundScenes.Add(scene.Name.Length, SceneInfo.FromScene(scene));
                        
                        if (foundScenes.Count == maxNumber)
                            break;
                    }
                }
            }

            // Copy the (sorted) found scenes to an array
            SceneInfo[] sceneInfos = new SceneInfo[foundScenes.Count];
            for (int i = 0; i < foundScenes.Count; i++)
                sceneInfos[i] = foundScenes[i];

            return sceneInfos;
        }

        public bool AddOrUpdateMapTile(SceneInfo sceneInfo, Image mapTile)
        {
            if (m_assetClient == null)
                return false;

            int zoomLevel = 1;
            uint x = (uint)sceneInfo.MinPosition.X / 256u;
            uint y = (uint)sceneInfo.MinPosition.Y / 256u;

            byte[] pngData;
            using (MemoryStream stream = new MemoryStream())
            {
                mapTile.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                pngData = stream.ToArray();
            }

            Asset asset = new Asset
            {
                ContentType = "image/png",
                CreationDate = DateTime.UtcNow,
                CreatorID = sceneInfo.ID,
                Data = pngData,
                ID = TileNameToUUID(zoomLevel, x, y)
            };

            // TODO: Create and store the other zoom levels
            return m_assetClient.StoreAsset(asset);
        }

        #endregion IGridClient Members

        #region Map Tile Handling

        private static UUID TileNameToUUID(int zoomLevel, uint x, uint y)
        {
            byte[] data = new byte[16];

            Utils.IntToBytes(zoomLevel, data, 4);
            Utils.UIntToBytes(x, data, 8);
            Utils.UIntToBytes(y, data, 12);

            return new UUID(data, 0);
        }

        #endregion Map Tile Handling
    }
}
