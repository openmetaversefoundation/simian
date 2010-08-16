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
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using log4net;
using Nini.Config;
using OpenMetaverse;

namespace Simian.Scenes.LLScene
{
    [ApplicationModule("LLSceneFactory")]
    public class LLSceneFactory : ISceneFactory, IApplicationModule
    {
        const double REGION_SIZE = 256.0d;
        const string SOURCE_PATH = "./Config/LLRegions/";

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        public event SceneStartCallback OnSceneStart;
        public event SceneStopCallback OnSceneStop;

        private Dictionary<UUID, IScene> m_scenes = new Dictionary<UUID, IScene>();
        private IScene[] m_scenesArray;

        public bool Start(Simian simian)
        {
            m_scenes = new Dictionary<UUID, IScene>();

            string[] sceneFiles = null;

            try { sceneFiles = Directory.GetFiles(SOURCE_PATH, "*.ini", SearchOption.AllDirectories); }
            catch (DirectoryNotFoundException)
            {
                m_log.Warn(Path.GetFullPath(SOURCE_PATH) + " not found, cannot load scene definitions");
                return false;
            }

            for (int i = 0; i < sceneFiles.Length; i++)
            {
                // Create the config source for this region by merging the app config and the region config
                IConfigSource configSource = simian.GetConfigCopy();
                IniConfigSource regionConfigSource = new IniConfigSource(sceneFiles[i]);
                configSource.Merge(regionConfigSource);

                IConfig config = configSource.Configs["LindenRegion"];

                if (config != null)
                {
                    UUID id;
                    UUID.TryParse(config.GetString("ID"), out id);

                    string name = config.GetString("Name");

                    uint locationX = 0, locationY = 0;
                    string[] locationParts = config.GetString("Location").Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (locationParts.Length != 2 || !UInt32.TryParse(locationParts[0], out locationX) || !UInt32.TryParse(locationParts[1], out locationY))
                    {
                        m_log.Warn("Missing or invalid Location for " + name + " region");
                    }
                    Vector3d regionPosition = new Vector3d(locationX * (uint)REGION_SIZE, locationY * (uint)REGION_SIZE, 0.0d);

                    Scene scene = new Scene(id, name, regionPosition, new Vector3d(256.0, 256.0, 4096.0), simian, configSource);
                    m_log.Info("Starting scene " + scene.Name + " (" + scene.ID + ")");
                    scene.Start();

                    m_scenes[scene.ID] = scene;

                    //CreateMapTile(scene);
                }
                else
                {
                    m_log.Warn("No [LindenRegion] config section found in " + sceneFiles[i] + ", skipping");
                }
            }

            // Create the array
            m_scenesArray = new IScene[m_scenes.Count];
            int j = 0;
            foreach (IScene scene in m_scenes.Values)
                m_scenesArray[j++] = scene;

            // Fire the OnSceneStart callback for each scene we started
            SceneStartCallback callback = OnSceneStart;
            if (callback != null)
            {
                for (int i = 0; i < m_scenesArray.Length; i++)
                {
                    callback(m_scenesArray[i]);
                }
            }

            return true;
        }

        public void Stop()
        {
            foreach (IScene scene in m_scenes.Values)
            {
                m_log.Info("Shutting down scene " + scene.Name + " (" + scene.ID + ")");
                scene.Stop();

                SceneStopCallback callback = OnSceneStop;
                if (callback != null)
                    callback(scene);
            }
        }

        public IScene[] GetScenes()
        {
            return m_scenesArray;
        }

        public bool TryGetScene(UUID sceneID, out IScene scene)
        {
            return m_scenes.TryGetValue(sceneID, out scene);
        }

        private void CreateMapTile(IScene scene)
        {
            ISceneRenderer renderer = scene.Simian.GetAppModule<ISceneRenderer>();
            IGridClient gridClient = scene.Simian.GetAppModule<IGridClient>();

            if (renderer != null && gridClient != null)
            {
                Viewport viewport = new Viewport(new Vector3(127.5f, 127.5f, 221.7025033688163f), -Vector3.UnitZ, 60f, 1024f, 0.1f, 256, 256);
                Image image = renderer.Render(scene, viewport);

                if (image != null)
                {
                    byte[] assetData;
                    using (MemoryStream stream = new MemoryStream())
                    {
                        image.Save(stream, ImageFormat.Png);
                        assetData = stream.ToArray();
                    }

                    // FIXME: Need IGridClient.SaveMapTile
                    //gridClient.
                }
                else
                {
                    m_log.Warn("Failed to render map tile for " + scene.Name);
                }
            }
        }

        private Image RenderScene(IScene scene, bool withObjects)
        {
            Bitmap bitmap = new Bitmap(256, 256, PixelFormat.Format24bppRgb);
            ITerrain m_terrain = scene.GetSceneModule<ITerrain>();
            float[] heightmap = (m_terrain != null) ? m_terrain.GetHeightmap() : null;
            float waterHeight = (m_terrain != null) ? m_terrain.WaterHeight : 20f;

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    if (heightmap != null && heightmap[y * 256 + x] > waterHeight)
                        bitmap.SetPixel(x, y, Color.Green);
                    else
                        bitmap.SetPixel(x, y, Color.Blue);
                }
            }

            return (System.Drawing.Image)bitmap;
        }
    }
}
