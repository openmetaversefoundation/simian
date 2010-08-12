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
        public string ProductName;
        public string ProductSKU;
        public RegionFlags RegionFlags;
        public SimAccess SimAccess;
        public UUID TerrainBase0;
        public UUID TerrainBase1;
        public UUID TerrainBase2;
        public UUID TerrainBase3;
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
            ProductName = "Simian";
            ProductSKU = String.Empty;

            RegionFlags = RegionFlags.SkipCollisions | RegionFlags.SkipScripts;
            SimAccess = SimAccess.Min;

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
        }

        private void Serialize()
        {
            if (m_dataStore != null)
            {
                OSDMap map = new OSDMap();

                map["owner_id"] = OSD.FromUUID(OwnerID);
                map["product_name"] = OSD.FromString(ProductName);
                map["product_sku"] = OSD.FromString(ProductSKU);
                map["region_flags"] = OSD.FromUInteger((uint)RegionFlags);
                map["sim_access"] = OSD.FromInteger((int)SimAccess);
                map["terrain_base_0"] = OSD.FromUUID(TerrainBase0);
                map["terrain_base_1"] = OSD.FromUUID(TerrainBase1);
                map["terrain_base_2"] = OSD.FromUUID(TerrainBase2);
                map["terrain_base_3"] = OSD.FromUUID(TerrainBase3);
                map["terrain_detail_0"] = OSD.FromUUID(TerrainDetail0);
                map["terrain_detail_1"] = OSD.FromUUID(TerrainDetail1);
                map["terrain_detail_2"] = OSD.FromUUID(TerrainDetail2);
                map["terrain_detail_3"] = OSD.FromUUID(TerrainDetail3);
                map["terrain_height_range_00"] = OSD.FromReal(TerrainHeightRange00);
                map["terrain_height_range_01"] = OSD.FromReal(TerrainHeightRange01);
                map["terrain_height_range_10"] = OSD.FromReal(TerrainHeightRange10);
                map["terrain_height_range_11"] = OSD.FromReal(TerrainHeightRange11);
                map["terrain_start_height_00"] = OSD.FromReal(TerrainStartHeight00);
                map["terrain_start_height_01"] = OSD.FromReal(TerrainStartHeight01);
                map["terrain_start_height_10"] = OSD.FromReal(TerrainStartHeight10);
                map["terrain_start_height_11"] = OSD.FromReal(TerrainStartHeight11);
                map["water_height"] = OSD.FromReal(WaterHeight);

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
                        OwnerID = map["owner_id"].AsUUID();
                        ProductName = map["product_name"].AsString();
                        ProductSKU = map["product_sku"].AsString();
                        RegionFlags = (RegionFlags)map["region_flags"].AsUInteger();
                        SimAccess = (SimAccess)map["sim_access"].AsInteger();
                        TerrainBase0 = map["terrain_base_0"].AsUUID();
                        TerrainBase1 = map["terrain_base_1"].AsUUID();
                        TerrainBase2 = map["terrain_base_2"].AsUUID();
                        TerrainBase3 = map["terrain_base_3"].AsUUID();
                        TerrainDetail0 = map["terrain_detail_0"].AsUUID();
                        TerrainDetail1 = map["terrain_detail_1"].AsUUID();
                        TerrainDetail2 = map["terrain_detail_2"].AsUUID();
                        TerrainDetail3 = map["terrain_detail_3"].AsUUID();
                        TerrainHeightRange00 = (float)map["terrain_height_range_00"].AsReal();
                        TerrainHeightRange01 = (float)map["terrain_height_range_01"].AsReal();
                        TerrainHeightRange10 = (float)map["terrain_height_range_10"].AsReal();
                        TerrainHeightRange11 = (float)map["terrain_height_range_11"].AsReal();
                        TerrainStartHeight00 = (float)map["terrain_start_height_00"].AsReal();
                        TerrainStartHeight01 = (float)map["terrain_start_height_01"].AsReal();
                        TerrainStartHeight10 = (float)map["terrain_start_height_10"].AsReal();
                        TerrainStartHeight11 = (float)map["terrain_start_height_11"].AsReal();
                        WaterHeight = (float)map["water_height"].AsReal();
                    }
                }
            }
        }
    }
}
