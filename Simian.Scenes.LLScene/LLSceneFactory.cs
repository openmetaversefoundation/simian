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

        private IScheduler m_scheduler;
        private ISceneRenderer m_renderer;
        private IGridClient m_gridClient;
        private Dictionary<UUID, IScene> m_scenes = new Dictionary<UUID, IScene>();
        private IScene[] m_scenesArray;

        public bool Start(Simian simian)
        {
            m_scheduler = simian.GetAppModule<IScheduler>();
            if (m_scheduler == null)
            {
                m_log.Error("LLSceneFactory requires an IScheduler");
                return false;
            }

            m_scenes = new Dictionary<UUID, IScene>();

            m_renderer = simian.GetAppModule<ISceneRenderer>();
            m_gridClient = simian.GetAppModule<IGridClient>();

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

                    // Create a map tile for this scene
                    m_scheduler.FireAndForget(o => CreateMapTile((IScene)o), scene);
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

        public void RestartScene(Scene scene)
        {
            scene.Stop();
            scene.Start();
        }

        private void CreateMapTile(IScene scene)
        {
            if (m_renderer != null && m_gridClient != null)
            {
                Vector3 camPos = new Vector3(127.5f, 127.5f, 221.7025033688163f);
                Viewport viewport = new Viewport(camPos, -Vector3.UnitZ, 1024f, 0.1f, 256, 256, 256f, 256f);
                Image image = m_renderer.Render(scene, viewport);

                if (image != null)
                    m_gridClient.AddOrUpdateMapTile(SceneInfo.FromScene(scene), image);
                else
                    m_log.Warn("Failed to render map tile for " + scene.Name);
            }
        }
    }
}
