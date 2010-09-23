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
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("LayerData")]
    public class LayerData : ISceneModule
    {
        public enum TerrainAction : byte
        {
            Flatten = 0,
            Raise = 1,
            Lower = 2,
            Smooth = 3,
            Noise = 4,
            Revert = 5,
        }

        const string TERRAIN = "Terrain";
        /// <summary>Magic UUID for combining with terrain position to create an event ID for terrain</summary>
        private static readonly UUID TERRAIN_EVENT_ID = new UUID("9c442e80-2e11-11df-8a39-0800200c9a66");
        /// <summary>The size of a single block of terrain</summary>
        private static readonly Vector3 TERRAIN_SCALE = new Vector3(16f, 16f, 1f);

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;
        private ITerrain m_terrain;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_terrain = m_scene.GetSceneModule<ITerrain>();
            if (m_terrain == null)
            {
                m_log.Error("LayerData requires an ITerrain module");
                return;
            }

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.ModifyLand, ModifyLandHandler);

                m_scene.AddInterestListHandler(TERRAIN, new InterestListEventHandler { SendCallback = SendTerrainPacket });

                m_scene.OnPresenceAdd += PresenceAddHandler;
                m_terrain.OnHeightmapChanged += HeightmapChangedHandler;
                m_terrain.OnHeightmapAreaChanged += HeightmapAreaChangedHandler;
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.ModifyLand, ModifyLandHandler);

                m_scene.OnPresenceAdd -= PresenceAddHandler;
                m_terrain.OnHeightmapChanged -= HeightmapChangedHandler;
                m_terrain.OnHeightmapAreaChanged -= HeightmapAreaChangedHandler;
            }
        }

        private void ModifyLandHandler(Packet packet, LLAgent agent)
        {
            ModifyLandPacket modify = (ModifyLandPacket)packet;

            TerrainAction action = (TerrainAction)modify.ModifyBlock.Action;
            float height = modify.ModifyBlock.Height;
            float seconds = modify.ModifyBlock.Seconds;

            // TODO: Build a permission mask based on this agent's permission to edit the affected parcels
            bool[] allowMask = new bool[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    allowMask[y * 64 + x] = true;
                }
            }

            // Build an edit mask that tracks all of the terrain blocks modified by this request
            bool[] editMask = new bool[16 * 16];

            for (int i = 0; i < modify.ParcelData.Length; i++)
            {
                ModifyLandPacket.ParcelDataBlock block = modify.ParcelData[i];

                int localID = block.LocalID;
                float north = block.North;
                float east = block.East;
                float south = block.South;
                float west = block.West;
                float size = (modify.ModifyBlockExtended.Length > i) ? modify.ModifyBlockExtended[i].BrushSize : modify.ModifyBlock.BrushSize;

                if (north == south && east == west)
                {
                    // Terrain painting
                    switch (action)
                    {
                        case TerrainAction.Raise:
                            RaiseLowerSphere(allowMask, ref editMask, west, south, height, size, seconds);
                            break;
                        case TerrainAction.Flatten:
                            FlattenSphere(allowMask, ref editMask, west, south, height, size, seconds);
                            break;
                        case TerrainAction.Lower:
                            RaiseLowerSphere(allowMask, ref editMask, west, south, height, size, seconds * -1.0f);
                            break;
                        case TerrainAction.Noise:
                            NoiseSphere(allowMask, ref editMask, west, south, height, size, seconds);
                            break;
                        case TerrainAction.Revert:
                            RevertSphere(allowMask, ref editMask, west, south, height, size, seconds);
                            break;
                        case TerrainAction.Smooth:
                            SmoothSphere(allowMask, ref editMask, west, south, height, size, seconds);
                            break;
                        default:
                            m_log.Warn("Unhandled ModifyLand paint action " + action);
                            break;
                    }
                }
                else
                {
                    // Terrain flooding
                    switch (action)
                    {
                        case TerrainAction.Raise:
                        case TerrainAction.Flatten:
                        case TerrainAction.Lower:
                        case TerrainAction.Noise:
                        case TerrainAction.Revert:
                        case TerrainAction.Smooth:
                        default:
                            m_log.Warn("Unhandled ModifyLand flood action " + action);
                            break;
                    }
                }
            }

            // Send updates out for any modified terrain blocks
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (editMask[y * 16 + x])
                    {
                        m_scene.CreateInterestListEvent(new InterestListEvent(
                            CreateTerrainEventID(x, y),
                            TERRAIN,
                            new Vector3(x * 16 + 8, y * 16 + 8, 0.0f),
                            TERRAIN_SCALE,
                            new int[] { x, y })
                        );
                    }
                }
            }
        }

        private void PresenceAddHandler(object sender, PresenceArgs e)
        {
            Vector3 terrainScale = new Vector3(16f, 16f, 1f);

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    // TODO: Generate event IDs based on the index of the terrain block to allow event compression
                    m_scene.CreateInterestListEventFor(e.Presence, new InterestListEvent(
                        CreateTerrainEventID(x, y),
                        TERRAIN,
                        new Vector3(x * 16 + 8, y * 16 + 8, 0.0f),
                        terrainScale,
                        new int[] { x, y })
                    );
                }
            }
        }

        private void HeightmapChangedHandler(float[] heightmap)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    m_scene.CreateInterestListEvent(new InterestListEvent(
                        CreateTerrainEventID(x, y),
                        TERRAIN,
                        new Vector3(x * 16 + 8, y * 16 + 8, 0.0f),
                        TERRAIN_SCALE,
                        new int[] { x, y })
                    );
                }
            }
        }

        private void HeightmapAreaChangedHandler(float[] heightmap, int xCell, int yCell)
        {
            m_scene.CreateInterestListEvent(new InterestListEvent(
                CreateTerrainEventID(xCell, yCell),
                TERRAIN,
                new Vector3(xCell * 16 + 8, yCell * 16 + 8, 0.0f),
                TERRAIN_SCALE,
                new int[] { xCell, yCell })
            );
        }

        private UUID CreateTerrainEventID(int x, int y)
        {
            return UUID.Combine(TERRAIN_EVENT_ID, new UUID(Utils.UIntsToLong((uint)x, (uint)y)));
        }

        private void SendTerrainPacket(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            const int PATCHES_PER_PACKET = 3;

            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            List<int> patches = new List<int>(PATCHES_PER_PACKET);

            for (int i = 0; i < eventDatas.Length; i++)
            {
                int[] state = (int[])eventDatas[i].Event.State;
                int x = state[0];
                int y = state[1];

                patches.Add(y * 16 + x);

                if (patches.Count == PATCHES_PER_PACKET || i == eventDatas.Length - 1)
                {
                    LayerDataPacket packet = TerrainCompressor.CreateLandPacket(m_terrain.GetHeightmap(), patches.ToArray());
                    m_udp.SendPacket(agent, packet, ThrottleCategory.Land, false);
                    patches = new List<int>(PATCHES_PER_PACKET);
                }
            }
        }

        #region Terrain Paint Methods

        private void RaiseLowerSphere(bool[] allowMask, ref bool[] editMask, float rx, float ry, float rz, float size, float seconds)
        {
            float[] heightmap = m_terrain.GetHeightmap();

            float s = (float)(int)(Math.Pow(2, size) + 0.5);

            float margin = s + 0.5f;
            int xFrom = (int)(rx - margin);
            int xTo = (int)(rx + margin) + 1;
            int yFrom = (int)(ry - margin);
            int yTo = (int)(ry + margin) + 1;

            if (xFrom < 0) xFrom = 0;
            if (yFrom < 0) yFrom = 0;
            if (xTo > 256) xTo = 256;
            if (yTo > 256) yTo = 256;

            for (int y = yFrom; y < yTo; y++)
            {
                for (int x = xFrom; x < xTo; x++)
                {
                    // Check the allow mask to see if we are allowed to edit this part of the heightmap
                    if (!allowMask[(y / 4) * 64 + (x / 4)])
                        continue;

                    // Calculate a cos-sphere and add it to the heighmap
                    float r = (float)Math.Sqrt(((x - rx) * (x - rx)) + ((y - ry) * (y - ry)));
                    float z = (float)Math.Cos((r * Math.PI) / (s * 2));

                    if (z > 0.0f)
                    {
                        float currentHeight = heightmap[y * 256 + x];
                        float modifier = z * seconds;

                        heightmap[y * 256 + x] = Utils.Clamp(currentHeight + modifier, 0f, 255f);

                        // Update the edit mask
                        editMask[(y / 16) * 16 + (x / 16)] = true;
                    }
                }
            }
        }

        private void FlattenSphere(bool[] allowMask, ref bool[] editMask, float rx, float ry, float rz, float size, float seconds)
        {
            float[] heightmap = m_terrain.GetHeightmap();

            float s = (size + 1.0f) * 1.35f;

            float margin = s + 0.5f;
            int xFrom = (int)(rx - margin);
            int xTo = (int)(rx + margin) + 1;
            int yFrom = (int)(ry - margin);
            int yTo = (int)(ry + margin) + 1;

            if (xFrom < 0) xFrom = 0;
            if (yFrom < 0) yFrom = 0;
            if (xTo > 256) xTo = 256;
            if (yTo > 256) yTo = 256;

            for (int y = yFrom; y < yTo; y++)
            {
                for (int x = xFrom; x < xTo; x++)
                {
                    // Check the allow mask to see if we are allowed to edit this part of the heightmap
                    if (!allowMask[(y / 4) * 64 + (x / 4)])
                        continue;

                    // Calculate a modifier strength
                    float z = (seconds < 4.0f)
                        ? SphericalFactor(x, y, rx, ry, s) * seconds * 0.25f
                        : 1.0f;

                    // Calculate the delta between the target height and current height
                    float delta = rz - heightmap[y * 256 + x];

                    if (Math.Abs(delta) > 0.1f)
                        delta *= Utils.Clamp(z, 0.0f, 1.0f);

                    if (delta != 0.0f)
                    {
                        float currentHeight = heightmap[y * 256 + x];
                        heightmap[y * 256 + x] = Utils.Clamp(currentHeight + delta, 0f, 255f);

                        // Update the edit mask
                        editMask[(y / 16) * 16 + (x / 16)] = true;
                    }
                }
            }
        }

        private void NoiseSphere(bool[] allowMask, ref bool[] editMask, float rx, float ry, float rz, float size, float seconds)
        {
            float[] heightmap = m_terrain.GetHeightmap();

            float s = (size + 1.0f) * 1.35f;

            float margin = s + 0.5f;
            int xFrom = (int)(rx - margin);
            int xTo = (int)(rx + margin) + 1;
            int yFrom = (int)(ry - margin);
            int yTo = (int)(ry + margin) + 1;

            if (xFrom < 0) xFrom = 0;
            if (yFrom < 0) yFrom = 0;
            if (xTo > 256) xTo = 256;
            if (yTo > 256) yTo = 256;

            for (int y = yFrom; y < yTo; y++)
            {
                for (int x = xFrom; x < xTo; x++)
                {
                    // Check the allow mask to see if we are allowed to edit this part of the heightmap
                    if (!allowMask[(y / 4) * 64 + (x / 4)])
                        continue;

                    // Calculate a modifier strength
                    float z = SphericalFactor(x, y, rx, ry, s);

                    if (z > 0f)
                    {
                        float currentHeight = heightmap[y * 256 + x];

                        // FIXME: Math.Abs() isn't right
                        float noise = Math.Abs(PerlinNoise2D(x / 256f, y / 256f, 8, 1.0f));

                        heightmap[y * 256 + x] = Utils.Clamp(currentHeight + noise * z * seconds, 0f, 255f);

                        // Update the edit mask
                        editMask[(y / 16) * 16 + (x / 16)] = true;
                    }
                }
            }
        }

        private void SmoothSphere(bool[] allowMask, ref bool[] editMask, float rx, float ry, float rz, float size, float seconds)
        {
            float[] heightmap = m_terrain.GetHeightmap();

            float s = (size + 1.0f) * 1.35f;

            float step = size / 4.0f;

            float margin = s + 0.5f;
            int xFrom = (int)(rx - margin);
            int xTo = (int)(rx + margin) + 1;
            int yFrom = (int)(ry - margin);
            int yTo = (int)(ry + margin) + 1;

            if (xFrom < 0) xFrom = 0;
            if (yFrom < 0) yFrom = 0;
            if (xTo > 256) xTo = 256;
            if (yTo > 256) yTo = 256;

            Dictionary<int, float> smoothes = new Dictionary<int, float>((yTo - yFrom) * (xTo - xFrom));

            for (int y = yFrom; y < yTo; y++)
            {
                for (int x = xFrom; x < xTo; x++)
                {
                    // Check the allow mask to see if we are allowed to edit this part of the heightmap
                    if (!allowMask[(y / 4) * 64 + (x / 4)])
                        continue;

                    // Calculate a modifier strength
                    float z = SphericalFactor(x, y, rx, ry, s);

                    if (z > 0f)
                    {
                        float average = 0f;
                        int avgsteps = 0;

                        for (float n = 0f - size; n < size; n += step)
                        {
                            for (float l = 0f - size; l < size; l += step)
                            {
                                average += GetBilinearInterpolate((int)(x + n), (int)(y + l), heightmap);
                                ++avgsteps;
                            }
                        }

                        // Collect all the smoothing operations. We can't apply them as
                        // we go or it will throw off the bilinear interpolation
                        smoothes.Add(y * 256 + x, average / (float)avgsteps);
                    }
                }
            }

            foreach (KeyValuePair<int, float> smooth in smoothes)
            {
                // Reverse the x and y values from the dictionary key
                int y = (smooth.Key - (smooth.Key % 256)) / 256;
                int x = smooth.Key - (y * 256);

                // Lookup the current height
                float height = heightmap[smooth.Key];

                // Calculate the modifier strength again
                float z = SphericalFactor(x, y, rx, ry, s);

                float a = (height - smooth.Value) * z;
                float newHeight = Utils.Clamp(height - (a * seconds), 0f, 255f);

                if (newHeight != height)
                {
                    heightmap[smooth.Key] = newHeight;

                    // Update the edit mask
                    editMask[(y / 16) * 16 + (x / 16)] = true;
                }
            }
        }

        private void RevertSphere(bool[] allowMask, ref bool[] editMask, float rx, float ry, float rz, float size, float seconds)
        {
            float[] heightmap = m_terrain.GetHeightmap();
            float[] revertMap = m_terrain.GetOriginalHeightmap();

            float s = (size + 1.0f) * 1.35f;

            float margin = s + 0.5f;
            int xFrom = (int)(rx - margin);
            int xTo = (int)(rx + margin) + 1;
            int yFrom = (int)(ry - margin);
            int yTo = (int)(ry + margin) + 1;

            if (xFrom < 0) xFrom = 0;
            if (yFrom < 0) yFrom = 0;
            if (xTo > 256) xTo = 256;
            if (yTo > 256) yTo = 256;

            for (int y = yFrom; y < yTo; y++)
            {
                for (int x = xFrom; x < xTo; x++)
                {
                    // Check the allow mask to see if we are allowed to edit this part of the heightmap
                    if (!allowMask[(y / 4) * 64 + (x / 4)])
                        continue;

                    // FIXME: Finish this
                }
            }
        }

        #endregion Terrain Paint Methods

        #region Terrain Flood Methods

        #endregion Terrain Flood Methods

        #region Helpers

        private static float SphericalFactor(float x, float y, float rx, float ry, float size)
        {
            return (float)(size * size - ((x - rx) * (x - rx) + (y - ry) * (y - ry)));
        }

        public static float GetBilinearInterpolate(int x, int y, float[] heightmap)
        {
            const int w = 256;
            const int h = 256;
            const int stepSize = 1;

            if (x > w - 2) x = w - 2;
            if (y > h - 2) y = h - 2;
            if (x < 0) x = 0;
            if (y < 0) y = 0;

            float h00 = heightmap[y * 256 + x];
            float h10 = heightmap[y * 256 + x + stepSize];
            float h01 = heightmap[(y + stepSize) * 256 + x];
            float h11 = heightmap[(y + stepSize) * 256 + x + stepSize];

            float h1 = h00;
            float h2 = h10;
            float h3 = h01;
            float h4 = h11;

            float a00 = h1;
            float a10 = h2 - h1;
            float a01 = h3 - h1;
            float a11 = h1 - h2 - h3 + h4;

            float partialx = x - x;
            float partialz = y - y;

            return a00 + (a10 * partialx) + (a01 * partialz) + (a11 * partialx * partialz);
        }

        private static float PerlinNoise2D(float x, float y, int octaves, float persistence)
        {
            float total = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float frequency = (float)Math.Pow(2, i);
                float amplitude = (float)Math.Pow(persistence, i);

                total += InterpolatedNoise((int)(x * frequency), (int)(y * frequency)) * amplitude;
            }
            return total;
        }

        public static float InterpolatedNoise(float x, float y)
        {
            int ix = (int)(x);
            float fx = x - (float)ix;

            int iy = (int)y;
            float fy = y - (float)iy;

            float v1 = SmoothedNoise1(ix, iy);
            float v2 = SmoothedNoise1(ix + 1, iy);
            float v3 = SmoothedNoise1(ix, iy + 1);
            float v4 = SmoothedNoise1(ix + 1, iy + 1);

            float i1 = Interpolate(v1, v2, fx);
            float i2 = Interpolate(v3, v4, fx);

            return Interpolate(i1, i2, fy);
        }

        private static float SmoothedNoise1(int x, int y)
        {
            float corners = (Noise(x - 1, y - 1) + Noise(x + 1, y - 1) + Noise(x - 1, y + 1) + Noise(x + 1, y + 1)) / 16;
            float sides = (Noise(x - 1, y) + Noise(x + 1, y) + Noise(x, y - 1) + Noise(x, y + 1)) / 8;
            float center = Noise(x, y) / 4;
            return corners + sides + center;
        }

        private static float Interpolate(float x, float y, float z)
        {
            return (x * (1.0f - z)) + (y * z);
        }

        /// <summary>
        /// Standard 2D noise function using popular prime numbers for perlin
        /// noise generation
        /// </summary>
        private static float Noise(int x, int y)
        {
            int n = x + y * 57;
            n = (n << 13) ^ n;
            return (1.0f - (float)((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f);
        }

        #endregion Helpers
    }
}
