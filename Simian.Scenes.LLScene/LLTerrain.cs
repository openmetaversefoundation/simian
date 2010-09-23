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
using System.IO;
using log4net;
using OpenMetaverse;
using Simian.Protocols.Linden;

namespace Simian.Scenes.LLScene
{
    [SceneModule("LLTerrain")]
    public class LLTerrain : ITerrain, ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        public event HeightmapChangedCallback OnHeightmapChanged;
        public event HeightmapAreaChangedCallback OnHeightmapAreaChanged;

        private IScene m_scene;
        private IDataStore m_dataStore;
        private float[] m_heightmap = new float[256 * 256];
        private float[] m_originalHeightmap = new float[256 * 256];
        private float m_waterHeight;

        public float WaterHeight
        {
            get { return m_waterHeight; }
            set { m_waterHeight = value; }
        }

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_dataStore = scene.Simian.GetAppModule<IDataStore>();

            Deserialize();
        }

        public void Stop()
        {
            Serialize();
        }

        public float[] GetHeightmap()
        {
            return m_heightmap;
        }

        public float[] GetOriginalHeightmap()
        {
            return m_originalHeightmap;
        }

        public void SetHeightmap(float[] heightmap)
        {
            if (heightmap.Length != 256 * 256)
                throw new ArgumentOutOfRangeException("heightmap must be 256x256");

            Buffer.BlockCopy(heightmap, 0, m_heightmap, 0, 256 * 256 * sizeof(float));
            Buffer.BlockCopy(heightmap, 0, m_originalHeightmap, 0, 256 * 256 * sizeof(float));

            HeightmapChangedCallback callback = OnHeightmapChanged;
            if (callback != null)
                callback(m_heightmap);
        }

        public void Set16x16Patch(int x, int y, float[] patch)
        {
            if (x < 0 || x > 16)
                throw new IndexOutOfRangeException(x + " is an invalid x value");
            if (y < 0 || y > 16)
                throw new IndexOutOfRangeException(y + " is an invalid y value");

            for (int yi = 0; yi < 16; yi++)
            {
                for (int xi = 0; xi < 16; xi++)
                {
                    int yOffset = (yi + y * 16) * 256;
                    int xOffset = (xi + x * 16);

                    m_heightmap[yOffset + xOffset] = patch[yi * 16 + xi];
                }
            }

            HeightmapAreaChangedCallback callback = OnHeightmapAreaChanged;
            if (callback != null)
                callback(m_heightmap, x, y);
        }

        public float GetTerrainHeightAt(float fx, float fy)
        {
            int x = (int)fx;
            int y = (int)fy;

            if (x > 255) x = 255;
            else if (x < 0) x = 0;
            if (y > 255) y = 255;
            else if (y < 0) y = 0;

            float center = m_heightmap[y * 256 + x];

            float distX = fx - (float)x;
            float distY = fy - (float)y;

            float nearestX;
            float nearestY;

            if (distX > 0f)
            {
                int i = x < 255 ? 1 : 0;
                nearestX = m_heightmap[y * 256 + (x + i)];
            }
            else
            {
                int i = x > 0 ? 1 : 0;
                nearestX = m_heightmap[y * 256 + (x - i)];
            }

            if (distY > 0f)
            {
                int i = y < 255 ? 1 : 0;
                nearestY = m_heightmap[(y + i) * 256 + x];
            }
            else
            {
                int i = y > 0 ? 1 : 0;
                nearestY = m_heightmap[(y - i) * 256 + x];
            }

            float lerpX = Utils.Lerp(center, nearestX, Math.Abs(distX));
            float lerpY = Utils.Lerp(center, nearestY, Math.Abs(distY));

            return ((lerpX + lerpY) * 0.5f);
        }

        private void Serialize()
        {
            if (m_dataStore != null)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    LLRAW llraw = new LLRAW();
                    llraw.Heightmap = m_heightmap;
                    llraw.WaterHeight = m_waterHeight;
                    llraw.ToStream(stream);

                    m_dataStore.BeginSerialize(
                        new SerializedData
                        {
                            StoreID = m_scene.ID,
                            Section = "terrain",
                            Name = "terrain",
                            ContentType = "application/x-llraw",
                            Data = stream.ToArray(),
                            Version = 1,
                        }
                    );
                }
            }
        }

        private void Deserialize()
        {
            if (m_dataStore != null)
            {
                SerializedData terrain = m_dataStore.DeserializeOne(m_scene.ID, "terrain");

                if (terrain != null)
                {
                    try
                    {
                        using (MemoryStream stream = new MemoryStream(terrain.Data))
                        {
                            LLRAW llraw = LLRAW.FromStream(stream);
                            m_waterHeight = llraw.WaterHeight;
                            SetHeightmap(llraw.Heightmap);
                        }
                    }
                    catch (Exception ex)
                    {
                        m_log.Warn("Failed to deserialize terrain: " + ex.Message);
                    }
                }
            }
        }
    }
}
