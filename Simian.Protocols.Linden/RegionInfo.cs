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
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("RegionInfo")]
    public class RegionInfo : ISceneModule
    {
        private IScene m_scene;
        private ITerrain m_terrain;
        private IDataStore m_dataStore;

        public UUID OwnerID;
        public RegionFlags RegionFlags;
        public SimAccess SimAccess;
        public UUID TerrainDetail0;
        public UUID TerrainDetail1;
        public UUID TerrainDetail2;
        public UUID TerrainDetail3;
        public float TerrainHeightRange00;
        public float TerrainHeightRange01;
        public float TerrainHeightRange10;
        public float TerrainHeightRange11;
        public float TerrainStartHeight00;
        public float TerrainStartHeight01;
        public float TerrainStartHeight10;
        public float TerrainStartHeight11;
        public uint ObjectCapacity;
        public uint MaxAgents;
        public bool UseFixedSun;
        public bool UseEstateSun;

        public float WaterHeight
        {
            get
            {
                if (m_terrain != null)
                    return m_terrain.WaterHeight;
                return 20.0f;
            }
            set
            {
                if (m_terrain != null)
                    m_terrain.WaterHeight = value;
            }
        }

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_dataStore = m_scene.Simian.GetAppModule<IDataStore>();
            m_terrain = m_scene.GetSceneModule<ITerrain>();

            // Initialize default region information in case there is no serialized data
            InitializeDefaults();

            // Load serialized region information if we have any
            Deserialize();
        }

        public void Stop()
        {
            Serialize();
        }

        private void InitializeDefaults()
        {
            RegionFlags =
                RegionFlags.AllowLandmark |
                RegionFlags.AllowSetHome |
                RegionFlags.AllowDirectTeleport |
                RegionFlags.AllowParcelChanges |
                RegionFlags.ExternallyVisible |
                RegionFlags.MainlandVisible |
                RegionFlags.PublicAllowed;

            SimAccess = SimAccess.PG;

            TerrainHeightRange00 = 0f;
            TerrainHeightRange01 = 20f;
            TerrainHeightRange10 = 0f;
            TerrainHeightRange11 = 20f;

            TerrainStartHeight00 = 0f;
            TerrainStartHeight01 = 40f;
            TerrainStartHeight10 = 0f;
            TerrainStartHeight11 = 40f;

            WaterHeight = 20f;

            ObjectCapacity = UInt32.MaxValue;
            MaxAgents = Byte.MaxValue;
        }

        private void Serialize()
        {
            if (m_dataStore != null)
            {
                OSDMap map = new OSDMap();

                map["owner_id"] = OwnerID;
                map["region_flags"] = (int)(uint)RegionFlags;
                map["sim_access"] = (int)SimAccess;
                map["terrain_detail_0"] = TerrainDetail0;
                map["terrain_detail_1"] = TerrainDetail1;
                map["terrain_detail_2"] = TerrainDetail2;
                map["terrain_detail_3"] = TerrainDetail3;
                map["terrain_height_range_00"] = TerrainHeightRange00;
                map["terrain_height_range_01"] = TerrainHeightRange01;
                map["terrain_height_range_10"] = TerrainHeightRange10;
                map["terrain_height_range_11"] = TerrainHeightRange11;
                map["terrain_start_height_00"] = TerrainStartHeight00;
                map["terrain_start_height_01"] = TerrainStartHeight01;
                map["terrain_start_height_10"] = TerrainStartHeight10;
                map["terrain_start_height_11"] = TerrainStartHeight11;
                map["water_height"] = WaterHeight;
                map["object_capacity"] = (int)ObjectCapacity;
                map["avatar_capacity"] = (int)MaxAgents;
                map["use_fixed_sun"] = UseFixedSun;
                map["use_estate_sun"] = UseEstateSun;

                m_dataStore.BeginSerialize(new SerializedData
                {
                    StoreID = m_scene.ID,
                    Section = "regioninfo",
                    Name = "regioninfo",
                    Data = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(map)),
                    ContentType = "application/llsd+json",
                    Version = 1,
                });
            }
        }

        private void Deserialize()
        {
            if (m_dataStore != null)
            {
                SerializedData regionInfo = m_dataStore.DeserializeOne(m_scene.ID, "regioninfo");

                if (regionInfo != null)
                {
                    OSDMap map = OSDParser.DeserializeJson(Encoding.UTF8.GetString(regionInfo.Data)) as OSDMap;

                    if (map != null)
                    {
                        OwnerID = map["owner_id"];
                        RegionFlags = (RegionFlags)(uint)map["region_flags"];
                        SimAccess = (SimAccess)(int)map["sim_access"];
                        TerrainDetail0 = map["terrain_detail_0"];
                        TerrainDetail1 = map["terrain_detail_1"];
                        TerrainDetail2 = map["terrain_detail_2"];
                        TerrainDetail3 = map["terrain_detail_3"];
                        TerrainHeightRange00 = map["terrain_height_range_00"];
                        TerrainHeightRange01 = map["terrain_height_range_01"];
                        TerrainHeightRange10 = map["terrain_height_range_10"];
                        TerrainHeightRange11 = map["terrain_height_range_11"];
                        TerrainStartHeight00 = map["terrain_start_height_00"];
                        TerrainStartHeight01 = map["terrain_start_height_01"];
                        TerrainStartHeight10 = map["terrain_start_height_10"];
                        TerrainStartHeight11 = map["terrain_start_height_11"];
                        WaterHeight = map["water_height"];
                        ObjectCapacity = map["object_capacity"];
                        MaxAgents = map["avatar_capacity"];
                        UseFixedSun = map["use_fixed_sun"];
                        UseEstateSun = map["use_estate_sun"];
                    }

                    // Make sure the simulator access level is set
                    if (SimAccess == SimAccess.Unknown)
                        SimAccess = SimAccess.PG;
                }
            }
        }
    }
}
