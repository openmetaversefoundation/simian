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
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("ParcelManager")]
    public class ParcelManager : ISceneModule, IParcels
    {
        const float DEFAULT_PRIMS_PER_SQM = 0.2288818359375f;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IDataStore m_dataStore;
        private DoubleDictionarySlim<UUID, int, SceneParcel> m_parcels;
        private int[] m_parcelOverlay;
        private int m_currentParcelID;
        private float m_primsPerSquareMeter;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_dataStore = m_scene.Simian.GetAppModule<IDataStore>();

            // Set the prims per square meter value
            m_primsPerSquareMeter = DEFAULT_PRIMS_PER_SQM;
            IConfig config = scene.Config.Configs["LindenRegion"];
            if (config != null)
            {
                // Parse the floating point value as a string and convert manually to avoid 
                // localization issues. This hack should be removed when we fix our build of Nini 
                // to always parse with EnUsCulture
                string primsPerSquareMeterStr = config.GetString("PrimsPerSquareMeter", DEFAULT_PRIMS_PER_SQM.ToString());
                if (!Single.TryParse(primsPerSquareMeterStr, System.Globalization.NumberStyles.Float, Utils.EnUsCulture.NumberFormat, out m_primsPerSquareMeter))
                    m_primsPerSquareMeter = DEFAULT_PRIMS_PER_SQM;
            }

            m_parcels = new DoubleDictionarySlim<UUID, int, SceneParcel>();
            m_parcelOverlay = new int[64 * 64];

            // Load serialized parcel information if we have any
            Deserialize();

            if (m_currentParcelID == 0)
            {
                // Create a default parcel if nothing was serialized
                CreateDefaultParcel();
            }

            // Put all of the initial scene entities in parcels
            m_scene.ForEachEntity(AddEntityToParcel);

            m_scene.OnEntitySignificantMovement += EntitySignificantMovementHandler;
        }

        public void Stop()
        {
            m_scene.OnEntitySignificantMovement -= EntitySignificantMovementHandler;

            Serialize();
        }

        public void AddOrUpdateParcel(SceneParcel parcel)
        {
            if (parcel.ID == UUID.Zero)
                parcel.ID = UUID.Random();
            if (parcel.LocalID <= 0)
                parcel.LocalID = System.Threading.Interlocked.Increment(ref m_currentParcelID);

            UpdateParcelOverlay(parcel);

            m_parcels.Add(parcel.ID, parcel.LocalID, parcel);

            Serialize();
        }

        public int GetParcelID(int x, int y)
        {
            return m_parcelOverlay[y * 64 + x];
        }

        public bool TryGetParcel(UUID parcelID, out SceneParcel parcel)
        {
            return m_parcels.TryGetValue(parcelID, out parcel);
        }

        public bool TryGetParcel(int parcelID, out SceneParcel parcel)
        {
            lock (m_parcels)
                return m_parcels.TryGetValue(parcelID, out parcel);
        }

        public bool TryGetParcel(Vector3 position, out SceneParcel parcel)
        {
            // Clamp position to inside region boundaries
            int x = (int)Utils.Clamp(position.X, 0f, 255f) / 4;
            int y = (int)Utils.Clamp(position.Y, 0f, 255f) / 4;

            int parcelID = m_parcelOverlay[y * 64 + x];

            return TryGetParcel(parcelID, out parcel);
        }

        public void ForEachParcel(Action<SceneParcel> action)
        {
            m_parcels.ForEach(action);
        }

        public void SplitParcel(SceneParcel parcel, int startX, int endX, int startY, int endY)
        {
            SceneParcel newParcel = new SceneParcel(parcel);
            newParcel.ID = UUID.Random();
            newParcel.LocalID = System.Threading.Interlocked.Increment(ref m_currentParcelID);
            newParcel.ClaimDate = DateTime.UtcNow;
            newParcel.Dwell = 0f;

            m_parcels.Add(newParcel.ID, newParcel.LocalID, newParcel);

            // Update parcel bitmaps
            BitPack origParcelBitmap = new BitPack(parcel.Bitmap, 0);
            BitPack parcelBitmap = new BitPack(new byte[512], 0);
            BitPack newParcelBitmap = new BitPack(newParcel.Bitmap, 0);

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool origParcelBit = (origParcelBitmap.UnpackBits(1) != 0);

                    if (x >= startX && x <= endX && y >= startY && y <= endY)
                    {
                        // Inside the new parcel
                        parcelBitmap.PackBit(false);
                        newParcelBitmap.PackBit(true);
                        m_parcelOverlay[y * 64 + x] = newParcel.LocalID;
                    }
                    else
                    {
                        // Not inside the new parcel
                        parcelBitmap.PackBit(origParcelBit);
                        newParcelBitmap.PackBit(false);
                    }
                }
            }

            // Update parcel landing info
            SceneParcel landingParcel;
            if (TryGetParcel(newParcel.LandingLocation, out landingParcel) && landingParcel == parcel)
            {
                newParcel.Landing = LandingType.None;
                newParcel.LandingLocation = Vector3.Zero;
            }
            else
            {
                parcel.Landing = LandingType.None;
                parcel.LandingLocation = Vector3.Zero;
            }

            // Update max prim counts
            Vector3 aabbMin, aabbMax;
            int area = GetParcelArea(parcel, out aabbMin, out aabbMax);
            parcel.MaxPrims = (int)Math.Round((float)area * m_primsPerSquareMeter);
            area = GetParcelArea(newParcel, out aabbMin, out aabbMax);
            newParcel.MaxPrims = (int)Math.Round((float)area * m_primsPerSquareMeter);

            Serialize();
        }

        public void JoinParcels(IList<SceneParcel> parcels)
        {
            if (parcels.Count < 2)
                return;

            SceneParcel masterParcel = parcels[0];
            
            for (int i = 1; i < parcels.Count; i++)
            {
                SceneParcel parcel = parcels[i];

                // Remove the child parcels
                m_parcels.Remove(parcel.ID, parcel.LocalID);

                // Merge the child parcel bitmaps into the master parcel bitmap
                for (int j = 0; j < masterParcel.Bitmap.Length; j++)
                    masterParcel.Bitmap[j] |= parcel.Bitmap[j];
            }

            // Update parcel bitmaps
            UpdateParcelOverlay(masterParcel);

            // Update max prim count
            Vector3 aabbMin, aabbMax;
            int area = GetParcelArea(masterParcel, out aabbMin, out aabbMax);
            masterParcel.MaxPrims = (int)Math.Round((float)area * m_primsPerSquareMeter);

            Serialize();
        }

        private void CreateDefaultParcel()
        {
            SceneParcel parcel = new SceneParcel();
            parcel.ID = UUID.Random();
            parcel.LocalID = System.Threading.Interlocked.Increment(ref m_currentParcelID);
            parcel.Desc = String.Empty;
            parcel.Flags = ParcelFlags.AllowAPrimitiveEntry | ParcelFlags.AllowFly | ParcelFlags.AllowGroupScripts |
                ParcelFlags.AllowLandmark | ParcelFlags.AllowOtherScripts | ParcelFlags.AllowTerraform |
                ParcelFlags.AllowVoiceChat | ParcelFlags.CreateGroupObjects | ParcelFlags.CreateObjects |
                ParcelFlags.UseBanList;
            parcel.Landing = LandingType.Direct;
            parcel.MaxPrims = 250000;
            parcel.Name = m_scene.Name;
            parcel.Status = ParcelStatus.Leased;
            parcel.LandingLocation = new Vector3(128f, 128f, 0f);
            parcel.MaxPrims = (int)Math.Round(m_primsPerSquareMeter * 256f * 256f);

            parcel.Bitmap = new byte[512];
            for (int i = 0; i < 512; i++)
                parcel.Bitmap[i] = Byte.MaxValue;

            for (int i = 0; i < m_parcelOverlay.Length; i++)
                m_parcelOverlay[i] = parcel.LocalID;

            // Add the default parcel to the list
            AddOrUpdateParcel(parcel);
        }

        private void UpdateParcelOverlay(SceneParcel parcel)
        {
            BitPack bitmap = new BitPack(parcel.Bitmap, 0);

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (bitmap.UnpackBits(1) != 0)
                        m_parcelOverlay[y * 64 + x] = parcel.LocalID;
                }
            }
        }

        private void EntitySignificantMovementHandler(object sender, EntitySignificantMovementArgs e)
        {
            AddEntityToParcel(e.Entity);
        }

        private void AddEntityToParcel(ISceneEntity entity)
        {
            // Ignore avatars
            if (entity is IScenePresence)
                return;

            // Only track root entities
            if (entity is ILinkable && ((ILinkable)entity).Parent != null)
                return;

            // NOTE: We can safely use RelativePosition instead of ScenePosition here since we only
            // deal with root entities
            SceneParcel parcel;
            if (TryGetParcel(entity.RelativePosition, out parcel))
            {
                bool removeOld = false;

                lock (parcel.ParcelEntities)
                {
                    if (!parcel.ParcelEntities.ContainsKey(entity.ID))
                    {
                        // Add this entity to the new parcel
                        parcel.ParcelEntities.Add(entity.ID, entity);
                        // Remove this entity from the previous parcel if a last significant
                        // position is set
                        removeOld = (entity.LastSignificantPosition != Vector3.Zero);
                    }
                }

                if (removeOld)
                {
                    bool removed = false;

                    SceneParcel oldParcel;
                    if (TryGetParcel(entity.LastSignificantPosition, out oldParcel))
                    {
                        lock (oldParcel.ParcelEntities)
                            removed = oldParcel.ParcelEntities.Remove(entity.ID);
                    }

                    #region Plan B

                    if (!removed)
                    {
                        m_log.Debug("Doing a deep search for the previous parcel of entity " + entity.ID);

                        m_parcels.ForEach(
                            delegate(SceneParcel p)
                            {
                                lock (p.ParcelEntities)
                                {
                                    if (p.ParcelEntities.Remove(entity.ID))
                                        removed = true;
                                }
                            }
                        );

                        if (!removed)
                            m_log.Warn("Deep search for previous parcel of entity " + entity.ID + " failed");
                    }

                    #endregion Plan B
                }
            }
        }

        #region Serialization/Deserialization

        private void Serialize()
        {
            if (m_dataStore != null)
            {
                OSDArray parcelsArray = new OSDArray();
                m_parcels.ForEach(delegate(SceneParcel parcel) { parcelsArray.Add(parcel.GetOSD()); });

                m_dataStore.BeginSerialize(new SerializedData
                {
                    StoreID = m_scene.ID,
                    Section = "parcels",
                    Name = "parcels",
                    Data = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(parcelsArray)),
                    ContentType = "application/llsd+json",
                    Version = 1,
                });
            }
        }

        private void Deserialize()
        {
            if (m_dataStore != null)
            {
                IList<SerializedData> items = m_dataStore.Deserialize(m_scene.ID, "parcels");
                foreach (SerializedData data in items)
                {
                    OSDArray parcelData = OSDParser.DeserializeJson(Encoding.UTF8.GetString(data.Data)) as OSDArray;
                    if (parcelData != null)
                    {
                        for (int i = 0; i < parcelData.Count; i++)
                        {
                            SceneParcel parcel = SceneParcel.FromOSD(parcelData[i] as OSDMap);
                            UpdateParcelOverlay(parcel);

                            m_parcels.Add(parcel.ID, parcel.LocalID, parcel);

                            if (parcel.LocalID > m_currentParcelID)
                                m_currentParcelID = parcel.LocalID;

                            UpdateParcelOverlay(parcel);
                        }
                    }
                }

                #region Prune orphaned parcels

                List<SceneParcel> orphanList = new List<SceneParcel>(0);
                m_parcels.ForEach(
                    delegate(SceneParcel parcel)
                    {
                        bool found = false;

                        for (int i = 0; i < m_parcelOverlay.Length; i++)
                        {
                            if (m_parcelOverlay[i] == parcel.LocalID)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                            orphanList.Add(parcel);
                    }
                );

                for (int i = 0; i < orphanList.Count; i++)
                {
                    SceneParcel orphan = orphanList[i];
                    m_log.WarnFormat("Pruning orphaned parcel {0} (ID: {1}, LocalID: {2})",
                        orphan.Name, orphan.ID, orphan.LocalID);
                    m_parcels.Remove(orphan.ID, orphan.LocalID);
                }

                #endregion Prune orphaned parcels
            }
        }

        #endregion Serialization/Deserialization

        public static int GetParcelArea(SceneParcel parcel, out Vector3 aabbMin, out Vector3 aabbMax)
        {
            int minX = 64;
            int minY = 64;
            int maxX = 0;
            int maxY = 0;
            int area = 0;

            System.Diagnostics.Debug.Assert(parcel.Bitmap != null);
            System.Diagnostics.Debug.Assert(parcel.Bitmap.Length == 512);

            BitPack bitmap = new BitPack(parcel.Bitmap, 0);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (bitmap.UnpackBits(1) != 0)
                    {
                        int x4 = x * 4;
                        int y4 = y * 4;

                        if (minX > x4) minX = x4;
                        if (minY > y4) minY = y4;
                        if (maxX < x4) maxX = x4;
                        if (maxX < y4) maxY = y4;
                        area += 16;
                    }
                }
            }

            aabbMin = new Vector3(minX, minY, 0f);
            aabbMax = new Vector3(maxX, maxY, 0f);
            return area;
        }
    }
}
