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
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Appearance")]
    public class Appearance : ISceneModule
    {
        public const string AVATAR_APPEARANCE = "AvatarAppearance";

        /// <summary>Magic UUID for combining with an agent ID to create an event ID for appearances</summary>
        public static readonly UUID APPEARANCE_EVENT_ID = new UUID("7e661f48-4e10-4657-b3ab-6e69073db48b");
        public static readonly Primitive.TextureEntry DEFAULT_TEXTURE_ENTRY = new Primitive.TextureEntry(OpenMetaverse.AppearanceManager.DEFAULT_AVATAR_TEXTURE);
        public static readonly byte[] DEFAULT_VISUAL_PARAMS = new byte[218];
        public static readonly byte[] BAKE_INDICES = new byte[] { 8, 9, 10, 11, 19, 20 };

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IUserClient m_userClient;
        private IInventoryClient m_inventoryClient;
        private LLUDP m_udp;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_userClient = m_scene.Simian.GetAppModule<IUserClient>();
            m_inventoryClient = m_scene.Simian.GetAppModule<IInventoryClient>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.AgentSetAppearance, AgentSetAppearanceHandler);
                m_udp.AddPacketHandler(PacketType.AgentWearablesRequest, AgentWearablesRequestHandler);
                m_udp.AddPacketHandler(PacketType.AgentIsNowWearing, AgentIsNowWearingHandler);
                m_udp.AddPacketHandler(PacketType.AgentCachedTexture, AgentCachedTextureHandler);

                m_scene.AddInterestListHandler(AVATAR_APPEARANCE, new InterestListEventHandler
                    { PriorityCallback = AvatarAppearancePrioritizer, SendCallback = SendAvatarAppearancePackets });

                m_scene.OnPresenceAdd += PresenceAddHandler;
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.AgentSetAppearance, AgentSetAppearanceHandler);
                m_udp.RemovePacketHandler(PacketType.AgentWearablesRequest, AgentWearablesRequestHandler);
                m_udp.RemovePacketHandler(PacketType.AgentIsNowWearing, AgentIsNowWearingHandler);
                m_udp.RemovePacketHandler(PacketType.AgentCachedTexture, AgentCachedTextureHandler);

                m_scene.OnPresenceAdd -= PresenceAddHandler;
            }
        }

        private void AgentSetAppearanceHandler(Packet packet, LLAgent agent)
        {
            AgentSetAppearancePacket set = (AgentSetAppearancePacket)packet;
            UpdateFlags updateFlags = 0;
            LLUpdateFlags llUpdateFlags = 0;

            m_log.Debug("Updating avatar appearance with " + set.VisualParam.Length + " visual params, texture=" +
                (set.ObjectData.TextureEntry.Length > 1 ? "yes" : "no"));

            //TODO: Store this for cached bake responses
            for (int i = 0; i < set.WearableData.Length; i++)
            {
                //AvatarTextureIndex index = (AvatarTextureIndex)set.WearableData[i].TextureIndex;
                //UUID cacheID = set.WearableData[i].CacheID;

                //m_log.DebugFormat("WearableData: {0} is now {1}", index, cacheID);
            }

            // Create a TextureEntry
            if (set.ObjectData.TextureEntry.Length > 1)
            {
                agent.TextureEntry = new Primitive.TextureEntry(set.ObjectData.TextureEntry, 0,
                    set.ObjectData.TextureEntry.Length);

                llUpdateFlags |= LLUpdateFlags.Textures;

                #region Bake Cache Check

                for (int i = 0; i < BAKE_INDICES.Length; i++)
                {
                    int j = BAKE_INDICES[i];
                    Primitive.TextureEntryFace face = agent.TextureEntry.FaceTextures[j];

                    if (face != null && face.TextureID != AppearanceManager.DEFAULT_AVATAR_TEXTURE)
                    {
                        m_log.Debug("Baked texture " + (AvatarTextureIndex)j + " set to " + face.TextureID);
                    }
                }

                #endregion Bake Cache Check
            }

            if (agent.Scale != set.AgentData.Size)
            {
                // This will be modified in UpdateHeight() if VisualParams are also sent
                agent.Scale = set.AgentData.Size;
                updateFlags |= UpdateFlags.Scale;
            }

            // Create a block of VisualParams
            if (set.VisualParam.Length > 1)
            {
                byte[] visualParams = new byte[set.VisualParam.Length];
                for (int i = 0; i < set.VisualParam.Length; i++)
                    visualParams[i] = set.VisualParam[i].ParamValue;

                agent.VisualParams = visualParams;
                agent.UpdateHeight();

                // Create the event that generates an AvatarAppearance packet for this agent
                m_scene.CreateInterestListEvent(new InterestListEvent
                (
                    UUID.Combine(agent.ID, APPEARANCE_EVENT_ID),
                    AVATAR_APPEARANCE,
                    agent.ScenePosition,
                    agent.Scale,
                    agent
                ));
            }

            if (updateFlags != 0 || llUpdateFlags != 0)
                m_scene.EntityAddOrUpdate(this, agent, updateFlags, (uint)llUpdateFlags);
        }

        private void AgentWearablesRequestHandler(Packet packet, LLAgent agent)
        {
            AgentWearablesUpdatePacket update = new AgentWearablesUpdatePacket();
            update.AgentData.AgentID = agent.ID;

            User user;
            if (m_userClient != null && m_userClient.TryGetUser(agent.ID, out user))
            {
                OSDMap appearanceMap = user.GetField("LLAppearance") as OSDMap;

                if (appearanceMap != null)
                {
                    Dictionary<WearableType, UUID> items = new Dictionary<WearableType, UUID>();
                    Dictionary<WearableType, UUID> assets = new Dictionary<WearableType, UUID>();

                    foreach (KeyValuePair<string, OSD> kvp in appearanceMap)
                    {
                        UUID id = kvp.Value.AsUUID();
                        if (id != UUID.Zero)
                        {
                            #region LLAppearance Parsing

                            switch (kvp.Key)
                            {
                                case "ShapeItem":
                                    items[WearableType.Shape] = id;
                                    break;
                                case "SkinItem":
                                    items[WearableType.Skin] = id;
                                    break;
                                case "HairItem":
                                    items[WearableType.Hair] = id;
                                    break;
                                case "EyesItem":
                                    items[WearableType.Eyes] = id;
                                    break;
                                case "ShirtItem":
                                    items[WearableType.Shirt] = id;
                                    break;
                                case "PantsItem":
                                    items[WearableType.Pants] = id;
                                    break;
                                case "ShoesItem":
                                    items[WearableType.Shoes] = id;
                                    break;
                                case "SocksItem":
                                    items[WearableType.Socks] = id;
                                    break;
                                case "JacketItem":
                                    items[WearableType.Jacket] = id;
                                    break;
                                case "GlovesItem":
                                    items[WearableType.Gloves] = id;
                                    break;
                                case "UndershirtItem":
                                    items[WearableType.Undershirt] = id;
                                    break;
                                case "UnderpantsItem":
                                    items[WearableType.Underpants] = id;
                                    break;
                                case "SkirtItem":
                                    items[WearableType.Skirt] = id;
                                    break;
                                case "AlphaItem":
                                    items[WearableType.Alpha] = id;
                                    break;
                                case "TattooItem":
                                    items[WearableType.Tattoo] = id;
                                    break;

                                case "ShapeAsset":
                                    assets[WearableType.Shape] = id;
                                    break;
                                case "SkinAsset":
                                    assets[WearableType.Skin] = id;
                                    break;
                                case "HairAsset":
                                    assets[WearableType.Hair] = id;
                                    break;
                                case "EyesAsset":
                                    assets[WearableType.Eyes] = id;
                                    break;
                                case "ShirtAsset":
                                    assets[WearableType.Shirt] = id;
                                    break;
                                case "PantsAsset":
                                    assets[WearableType.Pants] = id;
                                    break;
                                case "ShoesAsset":
                                    assets[WearableType.Shoes] = id;
                                    break;
                                case "SocksAsset":
                                    assets[WearableType.Socks] = id;
                                    break;
                                case "JacketAsset":
                                    assets[WearableType.Jacket] = id;
                                    break;
                                case "GlovesAsset":
                                    assets[WearableType.Gloves] = id;
                                    break;
                                case "UndershirtAsset":
                                    assets[WearableType.Undershirt] = id;
                                    break;
                                case "UnderpantsAsset":
                                    assets[WearableType.Underpants] = id;
                                    break;
                                case "SkirtAsset":
                                    assets[WearableType.Skirt] = id;
                                    break;
                                case "AlphaAsset":
                                    assets[WearableType.Alpha] = id;
                                    break;
                                case "TattooAsset":
                                    assets[WearableType.Tattoo] = id;
                                    break;
                            }

                            #endregion LLAppearance Parsing
                        }
                    }

                    update.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[items.Count];
                    int i = 0;
                    foreach (KeyValuePair<WearableType, UUID> kvp in items)
                    {
                        update.WearableData[i] = new AgentWearablesUpdatePacket.WearableDataBlock();
                        update.WearableData[i].WearableType = (byte)kvp.Key;
                        update.WearableData[i].ItemID = kvp.Value;
                        assets.TryGetValue(kvp.Key, out update.WearableData[i].AssetID);
                        ++i;
                    }
                }
                else
                {
                    m_log.Warn("User record does not contain an LLAppearance entry, appearance will not be set");
                }
            }

            if (update.WearableData == null)
                update.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[0];

            m_log.DebugFormat("Sending info about {0} wearables to {1}", update.WearableData.Length, agent.Name);

            update.AgentData.SerialNum = (uint)System.Threading.Interlocked.Increment(ref agent.CurrentWearablesSerialNum);
            m_udp.SendPacket(agent, update, ThrottleCategory.Asset, false);
        }

        private void AgentIsNowWearingHandler(Packet packet, LLAgent agent)
        {
            // This odd function takes the incoming map of WearableType -> ItemID, converts it to 
            // the LLAppearance format of string -> ItemID, fetches all of the wearable inventory 
            // items, and converts them into the LLAppearance format of string -> AssetID before 
            // updating the user's LLAppearance data

            AgentIsNowWearingPacket wearing = (AgentIsNowWearingPacket)packet;

            OSDMap appearanceMap = new OSDMap();
            List<UUID> requestIDs = new List<UUID>(wearing.WearableData.Length);
            Dictionary<UUID, string> wornItems = new Dictionary<UUID, string>();
            int count = 0;

            // Put the ItemIDs in appearanceMap and requestIDs
            for (int i = 0; i < wearing.WearableData.Length; i++)
            {
                AgentIsNowWearingPacket.WearableDataBlock block = wearing.WearableData[i];

                #region WearableType Conversion

                switch ((WearableType)block.WearableType)
                {
                    case WearableType.Shape:
                        appearanceMap["ShapeItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "ShapeAsset";
                        break;
                    case WearableType.Skin:
                        appearanceMap["SkinItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "SkinAsset";
                        break;
                    case WearableType.Hair:
                        appearanceMap["HairItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "HairAsset";
                        break;
                    case WearableType.Eyes:
                        appearanceMap["EyesItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "EyesAsset";
                        break;
                    case WearableType.Shirt:
                        appearanceMap["ShirtItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "ShirtAsset";
                        break;
                    case WearableType.Pants:
                        appearanceMap["PantsItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "PantsAsset";
                        break;
                    case WearableType.Shoes:
                        appearanceMap["ShoesItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "ShoesAsset";
                        break;
                    case WearableType.Socks:
                        appearanceMap["SocksItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "SocksAsset";
                        break;
                    case WearableType.Jacket:
                        appearanceMap["JacketItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "JacketAsset";
                        break;
                    case WearableType.Gloves:
                        appearanceMap["GlovesItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "GlovesAsset";
                        break;
                    case WearableType.Undershirt:
                        appearanceMap["UndershirtItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "UndershirtAsset";
                        break;
                    case WearableType.Underpants:
                        appearanceMap["UnderpantsItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "UnderpantsAsset";
                        break;
                    case WearableType.Skirt:
                        appearanceMap["SkirtItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "SkirtAsset";
                        break;
                    case WearableType.Alpha:
                        appearanceMap["AlphaItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "AlphaAsset";
                        break;
                    case WearableType.Tattoo:
                        appearanceMap["TattooItem"] =  OSD.FromUUID(block.ItemID);
                        wornItems[block.ItemID] = "TattooAsset";
                        break;
                }

                #endregion WearableType Conversion

                if (block.ItemID != UUID.Zero)
                {
                    requestIDs.Add(block.ItemID);
                    ++count;
                }
            }

            m_log.Debug("Updating agent wearables for " + agent.Name + ", new count: " + count);

            // Fetch all of the AssetIDs for inventory items listed in requestIDs
            IDictionary<UUID, UUID> itemsToAssetIDs;
            if (m_inventoryClient != null && m_inventoryClient.TryGetAssetIDs(agent.ID, requestIDs.ToArray(), out itemsToAssetIDs))
            {
                foreach (KeyValuePair<UUID, UUID> kvp in itemsToAssetIDs)
                {
                    // Put the AssetIDs in appearanceMap
                    string wearableAssetKey;
                    if (wornItems.TryGetValue(kvp.Key, out wearableAssetKey))
                        appearanceMap[wearableAssetKey] = OSD.FromUUID(kvp.Value);
                }

                m_log.Debug("Did " + itemsToAssetIDs.Count + " ItemID -> AssetID lookups for " + agent.Name);
            }
            else
            {
                m_log.Warn("Failed to resolve ItemIDs to AssetIDs for " + agent.Name);
            }

            if (m_userClient != null)
                m_userClient.UpdateUserFields(agent.ID, new OSDMap { { "LLAppearance", appearanceMap } });
            else
                m_log.Warn("Cannot save agent appearance without an IUserClient");
        }

        private void AgentCachedTextureHandler(Packet packet, LLAgent agent)
        {
            AgentCachedTexturePacket cached = (AgentCachedTexturePacket)packet;

            AgentCachedTextureResponsePacket response = new AgentCachedTextureResponsePacket();
            response.Header.Zerocoded = true;

            response.AgentData.AgentID = agent.ID;
            response.AgentData.SerialNum = cached.AgentData.SerialNum;

            response.WearableData = new AgentCachedTextureResponsePacket.WearableDataBlock[cached.WearableData.Length];

            // TODO: Respond back with actual cache entries if we have them
            for (int i = 0; i < cached.WearableData.Length; i++)
            {
                response.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock();
                response.WearableData[i].TextureIndex = cached.WearableData[i].TextureIndex;
                response.WearableData[i].TextureID = UUID.Zero;
                response.WearableData[i].HostName = Utils.EmptyBytes;
            }

            m_log.DebugFormat("Sending a cached texture response with {0}/{1} cache hits, SerialNum={2}",
                0, cached.WearableData.Length, cached.AgentData.SerialNum);

            m_udp.SendPacket(agent, response, ThrottleCategory.Task, false);
        }

        private void PresenceAddHandler(object sender, PresenceArgs e)
        {
            m_scene.ForEachPresence(
                delegate(IScenePresence presence)
                {
                    InterestListEvent eventData = new InterestListEvent
                    (
                        UUID.Combine(presence.ID, APPEARANCE_EVENT_ID),
                        AVATAR_APPEARANCE,
                        presence.ScenePosition,
                        presence.Scale,
                        presence
                    );

                    m_scene.CreateInterestListEventFor(e.Presence, eventData);
                }
            );
        }

        private double? AvatarAppearancePrioritizer(InterestListEvent eventData, IScenePresence presence)
        {
            if (eventData.State != presence)
                return InterestListEventHandler.DefaultPrioritizer(eventData, presence).Value + 1.0; // Add one so the ObjectUpdate for this avatar has a higher priority
            else
                return null; // Don't send AvatarAppearance packets to the agent controlling the avatar
        }

        private void SendAvatarAppearancePackets(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            for (int i = 0; i < eventDatas.Length; i++)
            {
                IScenePresence curPresence = (IScenePresence)eventDatas[i].Event.State;
                if (curPresence == presence)
                {
                    m_log.Warn("Attempted to send an AvatarAppearance packet to the controlling agent");
                    continue;
                }

                AvatarAppearancePacket appearance = new AvatarAppearancePacket();
                appearance.Sender.ID = curPresence.ID;
                appearance.Sender.IsTrial = false;

                Primitive.TextureEntry textureEntry = null;
                byte[] visualParams = null;

                if (curPresence is LLAgent)
                {
                    LLAgent curAgent = (LLAgent)curPresence;

                    // If this agent has not set VisualParams yet, skip it
                    if (curAgent.VisualParams == null)
                        continue;

                    textureEntry = curAgent.TextureEntry;
                    visualParams = curAgent.VisualParams;
                }

                if (textureEntry == null)
                {
                    // Use a default texture entry for this avatar
                    textureEntry = DEFAULT_TEXTURE_ENTRY;
                }

                if (visualParams == null)
                {
                    // Use default visual params for this avatar
                    visualParams = DEFAULT_VISUAL_PARAMS;
                }

                appearance.ObjectData.TextureEntry = textureEntry.GetBytes();
                appearance.VisualParam = new AvatarAppearancePacket.VisualParamBlock[visualParams.Length];
                for (int j = 0; j < visualParams.Length; j++)
                {
                    appearance.VisualParam[j] = new AvatarAppearancePacket.VisualParamBlock();
                    appearance.VisualParam[j].ParamValue = visualParams[j];
                }

                m_udp.SendPacket(agent, appearance, ThrottleCategory.Task, false);
            }
        }
    }
}
