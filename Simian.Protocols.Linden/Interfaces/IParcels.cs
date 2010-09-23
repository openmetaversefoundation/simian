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
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    public struct ParcelAccessEntry
    {
        public UUID AgentID;
        public DateTime Created;

        public OSDMap GetOSD()
        {
            return new OSDMap
            {
                { "agent_id", OSD.FromUUID(AgentID) },
                { "created", OSD.FromDate(Created) }
            };
        }

        public static ParcelAccessEntry FromOSD(OSDMap map)
        {
            return new ParcelAccessEntry
            {
                AgentID = map["agent_id"].AsUUID(),
                Created = map["created"].AsDate()
            };
        }
    }

    /// <summary>
    /// Parcel of land, a portion of virtual real estate in a scene
    /// </summary>
    public class SceneParcel
    {
        /// <summary>Tracks non-avatar entities currently residing on this parcel</summary>
        public Dictionary<UUID, ISceneEntity> ParcelEntities = new Dictionary<UUID, ISceneEntity>();

        /// <summary>Grid-wide identifier for this parcel</summary>
        public UUID ID;
        /// <summary>Scene-local identifier for this parcel</summary>
        public int LocalID;
        /// <summary>List of users blacklisted from this parcel</summary>
        public List<ParcelAccessEntry> AccessBlackList;
        /// <summary>List of users whitelisted for access to this parcel</summary>
        public List<ParcelAccessEntry> AccessWhiteList;
        /// <summary>ID of the user authorized to purchase this land</summary>
        public UUID AuthBuyerID;
        /// <summary>Packed binary map of the 4x4m regions this parcel occupies
        /// in the scene. 512 bytes, 4096 yes/no bits for a 64x64 grid</summary>
        public byte[] Bitmap;
        /// <summary>Category this parcel is listed under in search</summary>
        public ParcelCategory Category;
        /// <summary>Time this parcel was claimed</summary>
        public DateTime ClaimDate;
        /// <summary>Description</summary>
        public string Desc;
        /// <summary>Cumulative minutes avatars spend occupying this parcel in 
        /// a 24-hour period</summary>
        public float Dwell;
        /// <summary>Parcel settings</summary>
        public ParcelFlags Flags;
        /// <summary>ID of the group this parcel is associated with</summary>
        public UUID GroupID;
        /// <summary>True if this parcel is owned by the group specified in
        /// GroupID</summary>
        public bool IsGroupOwned;
        /// <summary>Landing point for incoming teleports</summary>
        public LandingType Landing;
        /// <summary>Maximum number of prims this parcel supports</summary>
        public int MaxPrims;
        /// <summary>Parcel media settings</summary>
        public ParcelMedia Media;
        /// <summary>Streaming music URL for this parcel</summary>
        public string MusicURL;
        /// <summary>Parcel name</summary>
        public string Name;
        /// <summary>True to signal for clients to mask the media URL</summary>
        public bool ObscureMedia;
        /// <summary>True to signal for clients to mask the music URL</summary>
        public bool ObscureMusic;
        /// <summary>Number of minutes before foreign objects are returned</summary>
        public int AutoReturnTime;
        /// <summary>ID of the user that owns this parcel</summary>
        public UUID OwnerID;
        /// <summary>The number of hours a pass to this parcel is good for, if 
        /// a pass is required for access</summary>
        public float PassHours;
        /// <summary>Cost of an access pass to this parcel</summary>
        public int PassPrice;
        /// <summary>If true, users that are not marked as age verified cannot
        /// enter this parcel</summary>
        public bool DenyAgeUnverified;
        /// <summary>If true, anonymous or guest users cannot enter this parcel</summary>
        public bool DenyAnonymous;
        /// <summary>If true, scripted entites cannot push users in this parcel</summary>
        public bool PushOverride;
        /// <summary>Sale price of this parcel</summary>
        public int SalePrice;
        /// <summary>TextureID of an image for this parcel</summary>
        public UUID SnapshotID;
        /// <summary>Parcel ownership status</summary>
        public ParcelStatus Status;
        /// <summary>Teleport landing location</summary>
        public Vector3 LandingLocation;
        /// <summary>Teleport landing looking direction</summary>
        public Vector3 LandingLookAt;

        /// <summary>
        /// Constructor
        /// </summary>
        public SceneParcel()
        {
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public SceneParcel(SceneParcel parcel)
        {
            if (parcel.AccessBlackList != null)
                AccessBlackList = new List<ParcelAccessEntry>(parcel.AccessBlackList);
            if (parcel.AccessWhiteList != null)
                AccessWhiteList = new List<ParcelAccessEntry>(parcel.AccessWhiteList);
            AuthBuyerID = parcel.AuthBuyerID;
            AutoReturnTime = parcel.AutoReturnTime;
            Bitmap = new byte[512];
            Buffer.BlockCopy(parcel.Bitmap, 0, Bitmap, 0, 512);
            Category = parcel.Category;
            ClaimDate = parcel.ClaimDate;
            Desc = parcel.Desc;
            Dwell = parcel.Dwell;
            Flags = parcel.Flags;
            GroupID = parcel.GroupID;
            IsGroupOwned = parcel.IsGroupOwned;
            Landing = parcel.Landing;
            LocalID = parcel.LocalID;
            MaxPrims = parcel.MaxPrims;
            ParcelMedia oldMedia = parcel.Media;
            Media = new ParcelMedia
            {
                MediaAutoScale = oldMedia.MediaAutoScale,
                MediaDesc = oldMedia.MediaDesc,
                MediaHeight = oldMedia.MediaHeight,
                MediaID = oldMedia.MediaID,
                MediaLoop = oldMedia.MediaLoop,
                MediaType = oldMedia.MediaType,
                MediaURL = oldMedia.MediaURL,
                MediaWidth = oldMedia.MediaWidth
            };
            MusicURL = parcel.MusicURL;
            Name = parcel.Name;
            ObscureMedia = parcel.ObscureMedia;
            ObscureMusic = parcel.ObscureMusic;
            OwnerID = parcel.OwnerID;
            PassHours = parcel.PassHours;
            PassPrice = parcel.PassPrice;
            DenyAgeUnverified = parcel.DenyAgeUnverified;
            DenyAnonymous = parcel.DenyAnonymous;
            PushOverride = parcel.PushOverride;
            SalePrice = parcel.SalePrice;
            SnapshotID = parcel.SnapshotID;
            Status = parcel.Status;
            LandingLocation = parcel.LandingLocation;
            LandingLookAt = parcel.LandingLookAt;
        }

        public OSDMap GetOSD()
        {
            OSDMap map = new OSDMap();

            map["id"] = OSD.FromUUID(ID);
            map["local_id"] = OSD.FromInteger(LocalID);

            OSDArray blacklist = new OSDArray();
            if (AccessBlackList != null)
            {
                for (int i = 0; i < AccessBlackList.Count; i++)
                    blacklist.Add(AccessBlackList[i].GetOSD());
            }
            map["access_black_list"] = blacklist;

            OSDArray whitelist = new OSDArray();
            if (AccessWhiteList != null)
            {
                for (int i = 0; i < AccessWhiteList.Count; i++)
                    whitelist.Add(AccessWhiteList[i].GetOSD());
            }
            map["access_white_list"] = whitelist;

            map["auth_buyer_id"] = OSD.FromUUID(AuthBuyerID);
            map["auto_return_time"] = OSD.FromInteger(AutoReturnTime);
            map["bitmap"] = OSD.FromBinary(Bitmap);
            map["category"] = OSD.FromInteger((int)Category);
            map["claim_date"] = OSD.FromDate(ClaimDate);
            map["desc"] = OSD.FromString(Desc);
            map["dwell"] = OSD.FromReal(Dwell);
            map["flags"] = OSD.FromInteger((uint)Flags);
            map["group_id"] = OSD.FromUUID(GroupID);
            map["is_group_owned"] = OSD.FromBoolean(IsGroupOwned);
            map["landing"] = OSD.FromInteger((int)Landing);
            map["max_prims"] = OSD.FromInteger(MaxPrims);

            OSDMap media = new OSDMap
            {
                { "auto_scale", OSD.FromBoolean(Media.MediaAutoScale) },
                { "desc", OSD.FromString(Media.MediaDesc) },
                { "height", OSD.FromInteger(Media.MediaHeight) },
                { "id", OSD.FromUUID(Media.MediaID) },
                { "loop", OSD.FromBoolean(Media.MediaLoop) },
                { "type", OSD.FromString(Media.MediaType) },
                { "url", OSD.FromString(Media.MediaURL) },
                { "width", OSD.FromInteger(Media.MediaWidth) }
            };
            map["parcel_media"] = media;

            map["music_url"] = OSD.FromString(MusicURL);
            map["name"] = OSD.FromString(Name);
            map["obscure_media"] = OSD.FromBoolean(ObscureMedia);
            map["obscure_music"] = OSD.FromBoolean(ObscureMusic);
            map["owner_id"] = OSD.FromUUID(OwnerID);
            map["pass_hours"] = OSD.FromReal(PassHours);
            map["pass_price"] = OSD.FromInteger(PassPrice);
            map["deny_age_unverified"] = OSD.FromBoolean(DenyAgeUnverified);
            map["deny_anonymous"] = OSD.FromBoolean(DenyAnonymous);
            map["push_override"] = OSD.FromBoolean(PushOverride);
            map["sale_price"] = OSD.FromInteger(SalePrice);
            map["snapshot_id"] = OSD.FromUUID(SnapshotID);
            map["status"] = OSD.FromInteger((int)Status);
            map["landing_location"] = OSD.FromVector3(LandingLocation);
            map["landing_look_at"] = OSD.FromVector3(LandingLookAt);

            return map;
        }

        public static SceneParcel FromOSD(OSDMap map)
        {
            SceneParcel parcel = new SceneParcel();

            parcel.ID = map["id"].AsUUID();
            parcel.LocalID = map["local_id"].AsInteger();

            OSDArray blacklist = map["access_black_list"] as OSDArray;
            if (blacklist != null)
            {
                parcel.AccessBlackList = new List<ParcelAccessEntry>(blacklist.Count);
                for (int i = 0; i < blacklist.Count; i++)
                {
                    if (blacklist[i] is OSDMap)
                        parcel.AccessBlackList[i] = ParcelAccessEntry.FromOSD((OSDMap)blacklist[i]);
                }
            }

            OSDArray whitelist = map["access_white_list"] as OSDArray;
            if (whitelist != null)
            {
                parcel.AccessWhiteList = new List<ParcelAccessEntry>(whitelist.Count);
                for (int i = 0; i < whitelist.Count; i++)
                {
                    if (whitelist[i] is OSDMap)
                        parcel.AccessWhiteList[i] = ParcelAccessEntry.FromOSD((OSDMap)whitelist[i]);
                }
            }

            parcel.AuthBuyerID = map["auth_buyer_id"].AsUUID();
            parcel.AutoReturnTime = map["auto_return_time"].AsInteger();
            parcel.Bitmap = map["bitmap"].AsBinary();
            parcel.Category = (ParcelCategory)map["category"].AsInteger();
            parcel.ClaimDate = map["claim_date"].AsDate();
            parcel.Desc = map["desc"].AsString();
            parcel.Dwell = (float)map["dwell"].AsReal();
            parcel.Flags = (ParcelFlags)map["flags"].AsInteger();
            parcel.GroupID = map["group_id"].AsUUID();
            parcel.IsGroupOwned = map["is_group_owned"].AsBoolean();
            parcel.Landing = (LandingType)map["landing"].AsInteger();
            parcel.MaxPrims = map["max_prims"].AsInteger();

            OSDMap media = map["parcel_media"] as OSDMap;
            if (media != null)
            {
                parcel.Media.MediaAutoScale = media["auto_scale"].AsBoolean();
                parcel.Media.MediaDesc = media["desc"].AsString();
                parcel.Media.MediaHeight = media["height"].AsInteger();
                parcel.Media.MediaID = media["id"].AsUUID();
                parcel.Media.MediaLoop = media["loop"].AsBoolean();
                parcel.Media.MediaType = media["type"].AsString();
                parcel.Media.MediaURL = media["url"].AsString();
                parcel.Media.MediaWidth = media["width"].AsInteger();
            }

            parcel.MusicURL = map["music_url"].AsString();
            parcel.Name = map["name"].AsString();
            parcel.ObscureMedia = map["obscure_media"].AsBoolean();
            parcel.ObscureMusic = map["obscure_music"].AsBoolean();
            parcel.OwnerID = map["owner_id"].AsUUID();
            parcel.PassHours = (float)map["pass_hours"].AsReal();
            parcel.PassPrice = map["pass_price"].AsInteger();
            parcel.DenyAgeUnverified = map["deny_age_unverified"].AsBoolean();
            parcel.DenyAnonymous = map["deny_anonymous"].AsBoolean();
            parcel.PushOverride = map["push_override"].AsBoolean();
            parcel.SalePrice = map["sale_price"].AsInteger();
            parcel.SnapshotID = map["snapshot_id"].AsUUID();
            parcel.Status = (ParcelStatus)map["status"].AsInteger();
            parcel.LandingLocation = map["landing_location"].AsVector3();
            parcel.LandingLookAt = map["landing_look_at"].AsVector3();

            return parcel;
        }
    }

    public interface IParcels
    {
        void AddOrUpdateParcel(SceneParcel parcel);
        int GetParcelID(int x, int y);
        bool TryGetParcel(UUID parcelID, out SceneParcel parcel);
        bool TryGetParcel(int parcelID, out SceneParcel parcel);
        bool TryGetParcel(Vector3 position, out SceneParcel parcel);
        void ForEachParcel(Action<SceneParcel> action);
        void SplitParcel(SceneParcel parcel, int startX, int endX, int startY, int endY);
        void JoinParcels(IList<SceneParcel> parcels);
    }
}
