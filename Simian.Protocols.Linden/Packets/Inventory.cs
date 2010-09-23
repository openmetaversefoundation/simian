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
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Inventory")]
    public class Inventory : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IScheduler m_scheduler;
        private LLUDP m_udp;
        private IInventoryClient m_inventoryClient;
        private IAssetClient m_assetClient;
        private IPhysicsEngine m_physics;
        private LLPermissions m_permissions;
        private IPrimMesher m_primMesher;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_scheduler = m_scene.Simian.GetAppModule<IScheduler>();
            if (m_scheduler == null)
            {
                m_log.Error("Inventory requires an IScheduler");
                return;
            }

            m_inventoryClient = m_scene.Simian.GetAppModule<IInventoryClient>();
            if (m_inventoryClient == null)
            {
                m_log.Error("Inventory requires an IInventoryClient");
                return;
            }

            m_assetClient = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Error("Inventory requires an IAssetClient");
                return;
            }

            m_primMesher = m_scene.GetSceneModule<IPrimMesher>();
            if (m_primMesher == null)
            {
                m_log.Error("Inventory requires an IPrimMesher");
                return;
            }

            m_physics = m_scene.GetSceneModule<IPhysicsEngine>();
            m_permissions = m_scene.GetSceneModule<LLPermissions>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.CreateInventoryItem, CreateInventoryItemHandler);
                m_udp.AddPacketHandler(PacketType.CreateInventoryFolder, CreateInventoryFolderHandler);
                m_udp.AddPacketHandler(PacketType.UpdateInventoryItem, UpdateInventoryItemHandler);
                m_udp.AddPacketHandler(PacketType.UpdateInventoryFolder, UpdateInventoryFolderHandler);
                m_udp.AddPacketHandler(PacketType.FetchInventoryDescendents, FetchInventoryDescendentsHandler);
                m_udp.AddPacketHandler(PacketType.FetchInventory, FetchInventoryHandler);
                m_udp.AddPacketHandler(PacketType.CopyInventoryItem, CopyInventoryItemHandler);
                m_udp.AddPacketHandler(PacketType.MoveInventoryItem, MoveInventoryItemHandler);
                m_udp.AddPacketHandler(PacketType.MoveInventoryFolder, MoveInventoryFolderHandler);
                m_udp.AddPacketHandler(PacketType.RemoveInventoryItem, RemoveInventoryItemHandler);
                m_udp.AddPacketHandler(PacketType.RemoveInventoryFolder, RemoveInventoryFolderHandler);
                m_udp.AddPacketHandler(PacketType.PurgeInventoryDescendents, PurgeInventoryDescendentsHandler);
                m_udp.AddPacketHandler(PacketType.DeRezObject, DeRezObjectHandler);
                m_udp.AddPacketHandler(PacketType.RezObject, RezObjectHandler);
                m_udp.AddPacketHandler(PacketType.LinkInventoryItem, LinkInventoryItemHandler);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.CreateInventoryItem, CreateInventoryItemHandler);
                m_udp.RemovePacketHandler(PacketType.CreateInventoryFolder, CreateInventoryFolderHandler);
                m_udp.RemovePacketHandler(PacketType.UpdateInventoryItem, UpdateInventoryItemHandler);
                m_udp.RemovePacketHandler(PacketType.FetchInventoryDescendents, FetchInventoryDescendentsHandler);
                m_udp.RemovePacketHandler(PacketType.FetchInventory, FetchInventoryHandler);
                m_udp.RemovePacketHandler(PacketType.CopyInventoryItem, CopyInventoryItemHandler);
                m_udp.RemovePacketHandler(PacketType.MoveInventoryItem, MoveInventoryItemHandler);
                m_udp.RemovePacketHandler(PacketType.MoveInventoryFolder, MoveInventoryFolderHandler);
                m_udp.RemovePacketHandler(PacketType.RemoveInventoryItem, RemoveInventoryItemHandler);
                m_udp.RemovePacketHandler(PacketType.RemoveInventoryFolder, RemoveInventoryFolderHandler);
                m_udp.RemovePacketHandler(PacketType.PurgeInventoryDescendents, PurgeInventoryDescendentsHandler);
                m_udp.RemovePacketHandler(PacketType.DeRezObject, DeRezObjectHandler);
                m_udp.RemovePacketHandler(PacketType.RezObject, RezObjectHandler);
                m_udp.RemovePacketHandler(PacketType.LinkInventoryItem, LinkInventoryItemHandler);
            }
        }

        #region Packet Handlers

        void CreateInventoryItemHandler(Packet packet, LLAgent agent)
        {
            CreateInventoryItemPacket create = (CreateInventoryItemPacket)packet;

            AssetType type = (AssetType)create.InventoryBlock.Type;

            UUID assetID = create.InventoryBlock.TransactionID;
            if (assetID != UUID.Zero)
                assetID = UUID.Combine(assetID, agent.SecureSessionID);

            if (assetID == UUID.Zero)
            {
                if (type == AssetType.LSLText)
                    assetID = TaskInventory.DEFAULT_SCRIPT;
                else
                    m_log.Warn("Creating a " + type + " inventory item with a null AssetID");
            }

            Permissions itemPerms = GetDefaultPermissions();
            itemPerms.NextOwnerMask = (PermissionMask)create.InventoryBlock.NextOwnerMask;

            // Create the inventory item
            LLInventoryItem item = new LLInventoryItem();
            item.AssetID = assetID;
            item.ContentType = LLUtil.LLAssetTypeToContentType((int)type);
            item.CreationDate = DateTime.UtcNow;
            item.CreatorID = agent.ID;
            item.Description = String.Empty;
            item.Flags = create.InventoryBlock.WearableType;
            item.GroupID = UUID.Zero;
            item.GroupOwned = false;
            item.ID = UUID.Zero; // Set on the server side
            item.Name = Utils.BytesToString(create.InventoryBlock.Name);
            item.OwnerID = agent.ID;
            item.ParentID = create.InventoryBlock.FolderID;
            item.Permissions = itemPerms;
            item.SalePrice = 10;
            item.SaleType = SaleType.Not;

            if (m_inventoryClient.TryCreateItem(agent.ID, item))
                SendItemCreatedPacket(agent, new LLInventoryItem(item), create.InventoryBlock.TransactionID, create.InventoryBlock.CallbackID);
            else
                m_log.Warn("Failed to create new inventory item " + item.Name + " in folder " + item.ParentID + " for " + agent.Name);
        }

        void CreateInventoryFolderHandler(Packet packet, LLAgent agent)
        {
            CreateInventoryFolderPacket create = (CreateInventoryFolderPacket)packet;

            UUID folderID = create.FolderData.FolderID;
            UUID parentID = create.FolderData.ParentID;
            AssetType assetType = (AssetType)create.FolderData.Type;
            string folderName = Utils.BytesToString(create.FolderData.Name);

            // Create the inventory folder
            InventoryFolder folder = new InventoryFolder();
            folder.ID = folderID;
            folder.Name = folderName;
            folder.OwnerID = agent.ID;
            folder.ParentID = parentID;
            folder.PreferredContentType = LLUtil.LLAssetTypeToContentType((int)assetType);
            folder.Version = 0;

            if (!m_inventoryClient.TryCreateFolder(agent.ID, folder))
                m_log.Warn(agent.Name + " failed to create inventory folder " + folderName);
        }

        void UpdateInventoryItemHandler(Packet packet, LLAgent agent)
        {
            UpdateInventoryItemPacket update = (UpdateInventoryItemPacket)packet;

            // No packet is sent back to the client, we just need to update the
            // inventory item locally
            for (int i = 0; i < update.InventoryData.Length; i++)
            {
                UpdateInventoryItemPacket.InventoryDataBlock block = update.InventoryData[i];

                UUID assetID = block.TransactionID;
                if (assetID != UUID.Zero)
                    assetID = UUID.Combine(assetID, agent.SecureSessionID);
                string itemName = Utils.BytesToString(block.Name);
                AssetType assetType = (AssetType)block.Type;

                InventoryBase invObject;
                if (m_inventoryClient.TryGetInventory(agent.ID, block.ItemID, out invObject) && invObject is InventoryItem)
                {
                    LLInventoryItem item = new LLInventoryItem((InventoryItem)invObject);

                    // SECURITY TODO: Check if we have permission to modify this item and its permissions

                    //item.AssetID = assetID; // This is not shared with the client, so the client sends it as UUID.Zero
                    item.ContentType = LLUtil.LLAssetTypeToContentType(block.Type);
                    //item.CreationDate = Utils.UnixTimeToDateTime(block.CreationDate); // Do not trust the client
                    //item.CreatorID = block.CreatorID; // Do not trust the client
                    item.Description = Utils.BytesToString(block.Description);
                    item.Flags = block.Flags; // TODO: Probably should not be trusting the client for this

                    if (m_permissions != null && m_permissions.IsInGroup(agent, block.GroupID))
                        item.GroupID = block.GroupID;
                    item.GroupOwned = (item.GroupID != UUID.Zero) ? block.GroupOwned : false;

                    //item.ID = block.ItemID; // Do not trust the client
                    item.Name = itemName;
                    //item.OwnerID = block.OwnerID; // Do not trust the client
                    item.ParentID = block.FolderID;

                    Permissions perms = item.Permissions;
                    perms.BaseMask = (PermissionMask)block.BaseMask;
                    perms.EveryoneMask = (PermissionMask)block.EveryoneMask;
                    perms.GroupMask = (PermissionMask)block.GroupMask;
                    perms.NextOwnerMask = (PermissionMask)block.NextOwnerMask;
                    perms.OwnerMask = (PermissionMask)block.OwnerMask;
                    item.Permissions = perms;
                    item.SalePrice = block.SalePrice;
                    item.SaleType = (SaleType)block.SaleType;

                    if (!m_inventoryClient.TryCreateItem(agent.ID, item))
                        m_log.Warn(agent.Name + "'s UpdateInventoryItem failed for " + item.Name);
                }
                else
                {
                    m_log.Warn(agent.Name + " sent UpdateInventoryItem for missing item " + block.ItemID);
                }
            }
        }

        void UpdateInventoryFolderHandler(Packet packet, LLAgent agent)
        {
            UpdateInventoryFolderPacket update = (UpdateInventoryFolderPacket)packet;

            // No packet is sent back to the client, we just need to update the
            // inventory folder locally
            for (int i = 0; i < update.FolderData.Length; i++)
            {
                UpdateInventoryFolderPacket.FolderDataBlock block = update.FolderData[i];

                string folderName = Utils.BytesToString(block.Name);
                AssetType assetType = (AssetType)block.Type;

                InventoryFolder folder = new InventoryFolder();
                folder.ID = block.FolderID;
                folder.Name = folderName;
                folder.OwnerID = agent.ID;
                folder.ParentID = block.ParentID;
                folder.PreferredContentType = LLUtil.LLAssetTypeToContentType((int)assetType);

                if (!m_inventoryClient.TryCreateFolder(agent.ID, folder))
                    m_log.Warn(agent.Name + "'s UpdateInventoryFolder failed for " + folder.Name);
            }
        }

        void FetchInventoryDescendentsHandler(Packet packet, LLAgent agent)
        {
            FetchInventoryDescendentsPacket fetch = (FetchInventoryDescendentsPacket)packet;
            bool sendFolders = fetch.InventoryData.FetchFolders;
            bool sendItems = fetch.InventoryData.FetchItems;
            // TODO: Obey SortOrder
            InventorySortOrder order = (InventorySortOrder)fetch.InventoryData.SortOrder;
            // TODO: Use OwnerID, for library access only

            InventoryBase invObject;
            if (m_inventoryClient.TryGetInventory(agent.ID, fetch.InventoryData.FolderID, out invObject) && invObject is InventoryFolder)
            {
                InventoryFolder folder = (InventoryFolder)invObject;

                List<InventoryItem> items = new List<InventoryItem>();
                List<InventoryFolder> folders = new List<InventoryFolder>();
                int descendCount = folder.Children.Count;
                int version = folder.Version;

                InventoryDescendentsPacket descendents = new InventoryDescendentsPacket();
                descendents.AgentData.AgentID = agent.ID;
                descendents.AgentData.FolderID = folder.ID;
                descendents.AgentData.OwnerID = folder.OwnerID;
                descendents.AgentData.Descendents = descendCount;
                descendents.AgentData.Version = version;

                if (sendItems || sendFolders)
                {
                    // Create a list of all of the folders and items under this folder
                    lock (folder.Children)
                    {
                        foreach (InventoryBase obj in folder.Children.Values)
                        {
                            if (obj is InventoryItem)
                                items.Add((InventoryItem)obj);
                            else
                                folders.Add((InventoryFolder)obj);
                        }
                    }
                }

                if (sendFolders)
                {
                    descendents.FolderData = new InventoryDescendentsPacket.FolderDataBlock[folders.Count];
                    for (int i = 0; i < folders.Count; i++)
                    {
                        InventoryFolder currentFolder = folders[i];

                        descendents.FolderData[i] = new InventoryDescendentsPacket.FolderDataBlock();
                        descendents.FolderData[i].FolderID = currentFolder.ID;
                        descendents.FolderData[i].Name = Utils.StringToBytes(currentFolder.Name);
                        descendents.FolderData[i].ParentID = currentFolder.ParentID;
                        descendents.FolderData[i].Type = LLUtil.ContentTypeToLLAssetType(currentFolder.PreferredContentType);
                    }
                }
                else
                {
                    descendents.FolderData = new InventoryDescendentsPacket.FolderDataBlock[0];

                    /*descendents.FolderData = new InventoryDescendentsPacket.FolderDataBlock[1];
                    descendents.FolderData[0] = new InventoryDescendentsPacket.FolderDataBlock();
                    descendents.FolderData[0].FolderID = folder.ID;
                    descendents.FolderData[0].Name = Utils.StringToBytes(folder.Name);
                    descendents.FolderData[0].ParentID = folder.ParentID;
                    descendents.FolderData[0].Type = Util.ContentTypeToLLAssetType(folder.PreferredContentType);*/
                }

                if (sendItems)
                {
                    descendents.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count];
                    for (int i = 0; i < items.Count; i++)
                    {
                        LLInventoryItem currentItem = new LLInventoryItem(items[i]);

                        InventoryDescendentsPacket.ItemDataBlock block = new InventoryDescendentsPacket.ItemDataBlock();
                        block.AssetID = currentItem.AssetID;
                        block.BaseMask = (uint)currentItem.Permissions.BaseMask;
                        block.CRC = currentItem.CRC();
                        block.CreationDate = (int)Utils.DateTimeToUnixTime(currentItem.CreationDate);
                        block.CreatorID = currentItem.CreatorID;
                        block.Description = Utils.StringToBytes(currentItem.Description);
                        block.EveryoneMask = (uint)currentItem.Permissions.EveryoneMask;
                        block.Flags = currentItem.Flags;
                        block.FolderID = currentItem.ParentID;
                        block.GroupID = currentItem.GroupID;
                        block.GroupMask = (uint)currentItem.Permissions.GroupMask;
                        block.GroupOwned = currentItem.GroupOwned;
                        block.InvType = (sbyte)currentItem.InventoryType;
                        block.ItemID = currentItem.ID;
                        block.Name = Utils.StringToBytes(currentItem.Name);
                        block.NextOwnerMask = (uint)currentItem.Permissions.NextOwnerMask;
                        block.OwnerID = currentItem.OwnerID;
                        block.OwnerMask = (uint)currentItem.Permissions.OwnerMask;
                        block.SalePrice = currentItem.SalePrice;
                        block.SaleType = (byte)currentItem.SaleType;
                        block.Type = (sbyte)currentItem.AssetType;

                        // Handle inventory links
                        InventoryType linkedInvType = currentItem.LinkedInventoryType;
                        if (linkedInvType != InventoryType.Unknown)
                            block.InvType = (sbyte)linkedInvType;

                        descendents.ItemData[i] = block;
                    }
                }
                else
                {
                    descendents.ItemData = new InventoryDescendentsPacket.ItemDataBlock[0];
                }

                m_udp.SendPacket(agent, descendents, ThrottleCategory.Task, false);
            }
            else
            {
                m_log.Warn("FetchInventoryDescendents called for an unknown folder " + fetch.InventoryData.FolderID);
            }
        }

        void FetchInventoryHandler(Packet packet, LLAgent agent)
        {
            FetchInventoryPacket fetch = (FetchInventoryPacket)packet;

            // Fetch all of the items from the inventory server
            Dictionary<UUID, InventoryBase> items = FetchInventoryItems(agent, fetch.InventoryData);

            FetchInventoryReplyPacket reply = new FetchInventoryReplyPacket();
            reply.AgentData.AgentID = agent.ID;
            List<FetchInventoryReplyPacket.InventoryDataBlock> replies = new List<FetchInventoryReplyPacket.InventoryDataBlock>();

            for (int i = 0; i < fetch.InventoryData.Length; i++)
            {
                UUID itemID = fetch.InventoryData[i].ItemID;
                InventoryBase obj;

                if (itemID != UUID.Zero)
                {
                    FetchInventoryReplyPacket.InventoryDataBlock block = new FetchInventoryReplyPacket.InventoryDataBlock();
                    block.ItemID = itemID;

                    if (items.TryGetValue(itemID, out obj) && obj is InventoryItem)
                    {
                        LLInventoryItem item = new LLInventoryItem((InventoryItem)obj);

                        Permissions perms = item.Permissions;

                        block.AssetID = item.AssetID;
                        block.BaseMask = (uint)perms.BaseMask;
                        block.CRC = item.CRC();
                        block.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);
                        block.CreatorID = item.CreatorID;
                        block.Description = Utils.StringToBytes(item.Description);
                        block.EveryoneMask = (uint)perms.EveryoneMask;
                        block.Flags = item.Flags;
                        block.FolderID = item.ParentID;
                        block.GroupID = item.GroupID;
                        block.GroupMask = (uint)perms.GroupMask;
                        block.GroupOwned = item.GroupOwned;
                        block.InvType = (sbyte)item.InventoryType;
                        block.Name = Utils.StringToBytes(item.Name);
                        block.NextOwnerMask = (uint)perms.NextOwnerMask;
                        block.OwnerID = item.OwnerID;
                        block.OwnerMask = (uint)perms.OwnerMask;
                        block.SalePrice = item.SalePrice;
                        block.SaleType = (byte)item.SaleType;
                        block.Type = (sbyte)item.AssetType;
                    }
                    else
                    {
                        m_log.Warn("FetchInventory failed for item " + itemID);

                        block.Name = Utils.EmptyBytes;
                        block.Description = Utils.EmptyBytes;
                    }

                    replies.Add(block);
                }
            }

            if (replies.Count > 0)
            {
                reply.InventoryData = replies.ToArray();

                m_udp.SendPacket(agent, reply, ThrottleCategory.Task, true);
            }
        }

        void CopyInventoryItemHandler(Packet packet, LLAgent agent)
        {
            CopyInventoryItemPacket copy = (CopyInventoryItemPacket)packet;

            for (int i = 0; i < copy.InventoryData.Length; i++)
            {
                CopyInventoryItemPacket.InventoryDataBlock block = copy.InventoryData[i];
                CopyItem(agent, block.OldItemID, Utils.BytesToString(block.NewName), block.NewFolderID, UUID.Zero, block.CallbackID);
            }
        }

        void MoveInventoryItemHandler(Packet packet, LLAgent agent)
        {
            MoveInventoryItemPacket move = (MoveInventoryItemPacket)packet;
            // TODO: What is move.AgentData.Stamp for?

            List<InventoryBase> objs = new List<InventoryBase>(move.InventoryData.Length);

            for (int i = 0; i < move.InventoryData.Length; i++)
            {
                MoveInventoryItemPacket.InventoryDataBlock block = move.InventoryData[i];
                UUID newFolderID = block.FolderID;
                string newName = Utils.BytesToString(block.NewName);

                InventoryItem newObj = MoveItem(agent, block.ItemID, newName, newFolderID);
                if (newObj != null)
                    objs.Add(newObj);
            }

            SendBulkUpdate(agent, objs, UUID.Zero, 0);
        }

        void MoveInventoryFolderHandler(Packet packet, LLAgent agent)
        {
            MoveInventoryFolderPacket move = (MoveInventoryFolderPacket)packet;
            List<InventoryBase> objs = new List<InventoryBase>(move.InventoryData.Length);

            // TODO: What is move.AgentData.Stamp for?

            for (int i = 0; i < move.InventoryData.Length; i++)
            {
                MoveInventoryFolderPacket.InventoryDataBlock block = move.InventoryData[i];
                UUID newFolderID = block.ParentID;

                InventoryFolder newFolder = MoveFolder(agent, block.FolderID, block.ParentID);
                if (newFolder != null)
                    objs.Add(newFolder);
            }

            SendBulkUpdate(agent, objs, UUID.Zero, 0);
        }

        void RemoveInventoryItemHandler(Packet packet, LLAgent agent)
        {
            RemoveInventoryItemPacket remove = (RemoveInventoryItemPacket)packet;

            List<UUID> itemIDs = new List<UUID>(remove.InventoryData.Length);
            for (int i = 0; i < remove.InventoryData.Length; i++)
                itemIDs.Add(remove.InventoryData[i].ItemID);

            if (!m_inventoryClient.TryRemoveNodes(agent.ID, itemIDs))
                m_log.Warn(agent.Name + " failed to delete " + itemIDs.Count + " inventory items");
        }

        void RemoveInventoryFolderHandler(Packet packet, LLAgent agent)
        {
            RemoveInventoryFolderPacket remove = (RemoveInventoryFolderPacket)packet;

            List<UUID> folderIDs = new List<UUID>(remove.FolderData.Length);
            for (int i = 0; i < remove.FolderData.Length; i++)
                folderIDs.Add(remove.FolderData[i].FolderID);

            if (!m_inventoryClient.TryRemoveNodes(agent.ID, folderIDs))
                m_log.Warn(agent.Name + " failed to delete " + folderIDs.Count + " inventory folders");
        }

        void PurgeInventoryDescendentsHandler(Packet packet, LLAgent agent)
        {
            PurgeInventoryDescendentsPacket purge = (PurgeInventoryDescendentsPacket)packet;

            if (!m_inventoryClient.TryPurgeFolder(agent.ID, purge.InventoryData.FolderID))
                m_log.Warn(agent.Name + " failed to purge folder " + purge.InventoryData.FolderID);
        }

        void DeRezObjectHandler(Packet packet, LLAgent agent)
        {
            DeRezObjectPacket derez = (DeRezObjectPacket)packet;
            DeRezDestination destination = (DeRezDestination)derez.AgentBlock.Destination;

            for (int i = 0; i < derez.ObjectData.Length; i++)
            {
                uint localID = derez.ObjectData[i].ObjectLocalID;

                ISceneEntity obj;
                if (m_scene.TryGetEntity(localID, out obj) && obj is LLPrimitive)
                {
                    // We only support LLPrimitive for now
                    LLPrimitive prim = (LLPrimitive)obj;
                    string contentType = LLUtil.LLAssetTypeToContentType((int)AssetType.Object);

                    if (prim.Parent == null)
                    {
                        // Unsit any avatars sitting on this object
                        m_scene.ForEachPresence(delegate(IScenePresence presence)
                        {
                            if (presence is ILinkable)
                            {
                                ILinkable avatar = (ILinkable)presence;
                                if (avatar.Parent == prim)
                                    avatar.SetParent(null, true, true);
                            }
                        });

                        // Serialize this prim and any children
                        byte[] assetData = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(LLPrimitive.SerializeLinkset(prim)));

                        switch (destination)
                        {
                            case DeRezDestination.AgentInventorySave:
                                m_log.Warn("DeRezObject: Got a AgentInventorySave, DestID: " +
                                    derez.AgentBlock.DestinationID.ToString());
                                break;
                            case DeRezDestination.AgentInventoryCopy:
                                // TODO: Check copy permission
                                ObjectToInventory(agent.ID, obj, assetData, contentType, 0, false, derez.AgentBlock.DestinationID, derez.AgentBlock.TransactionID, false, 0);
                                break;
                            case DeRezDestination.TaskInventory:
                                m_log.Warn("DeRezObject: Got a TaskInventory, DestID: " +
                                    derez.AgentBlock.DestinationID.ToString());
                                break;
                            case DeRezDestination.Attachment:
                                m_log.Warn("DeRezObject: Got an Attachment, DestID: " +
                                    derez.AgentBlock.DestinationID.ToString());
                                break;
                            case DeRezDestination.AgentInventoryTake:
                                ObjectToInventory(agent.ID, obj, assetData, contentType, 0, false, derez.AgentBlock.DestinationID, derez.AgentBlock.TransactionID, true, 0);
                                break;
                            case DeRezDestination.ForceToGodInventory:
                                m_log.Warn("DeRezObject: Got a ForceToGodInventory, DestID: " +
                                    derez.AgentBlock.DestinationID.ToString());
                                break;
                            case DeRezDestination.TrashFolder:
                                // TODO: Check permissions
                                ObjectToInventory(agent.ID, obj, assetData, contentType, 0, false, derez.AgentBlock.DestinationID, derez.AgentBlock.TransactionID, true, 0);
                                break;
                            case DeRezDestination.AttachmentToInventory:
                                m_log.Warn("DeRezObject: Got an AttachmentToInventory, DestID: " +
                                    derez.AgentBlock.DestinationID.ToString());
                                break;
                            case DeRezDestination.AttachmentExists:
                                m_log.Warn("DeRezObject: Got an AttachmentExists, DestID: " +
                                    derez.AgentBlock.DestinationID.ToString());
                                break;
                            case DeRezDestination.ReturnToOwner:
                                // TODO: Check permissions
                                ObjectToInventory(obj.OwnerID, obj, assetData, contentType, 0, false, UUID.Zero, derez.AgentBlock.TransactionID, false, 0);
                                break;
                            case DeRezDestination.ReturnToLastOwner:
                                m_log.Warn("DeRezObject: Got a ReturnToLastOwner, DestID: " +
                                    derez.AgentBlock.DestinationID.ToString());
                                break;
                        }
                    }
                    else
                    {
                        m_log.Warn("Cannot DeRez prim " + localID + ", it is a child of prim " + prim.Parent.LocalID);
                    }
                }
                else
                {
                    m_log.Warn("Cannot DeRez object " + localID + ", entity is missing or of the wrong type");
                }
            }
        }

        void RezObjectHandler(Packet packet, LLAgent agent)
        {
            RezObjectPacket rez = (RezObjectPacket)packet;

            // Find the target position
            Vector3 position = Vector3.Zero;
            Vector3 linksetScale = Vector3.Zero;
            bool bypassRaycast = (rez.RezData.BypassRaycast == 1);
            //bool rayEndIsIntersection = rez.RezData.RayEndIsIntersection;

            #region Position Calculation

            if (bypassRaycast || m_physics == null)
            {
                position = rez.RezData.RayEnd;
            }
            else
            {
                Vector3 direction = (rez.RezData.RayEnd - rez.RezData.RayStart);
                direction /= direction.Length();
                Ray ray = new Ray(rez.RezData.RayStart, direction);

                ISceneEntity collisionObj;
                float collisionDist;
                if (m_physics.FullSceneCollisionTest(true, ray, null, out collisionObj, out collisionDist))
                {
                    position = ray.GetPoint(collisionDist);
                }
                else
                {
                    m_log.Warn("Full scene collision test for ray " + ray + " failed");
                    position = agent.ScenePosition + Vector3.UnitZ;
                }
            }

            position.Z += linksetScale.Z * 0.5f;

            #endregion Position Calculation

            InventoryBase invObject;
            if (m_inventoryClient.TryGetInventory(agent.ID, rez.InventoryData.ItemID, out invObject) & invObject is InventoryItem)
            {
                InventoryItem item = (InventoryItem)invObject;

                Asset asset;
                if (m_assetClient.TryGetAsset(item.AssetID, item.ContentType, out asset))
                {
                    #region Object Deserialization/Rezzing

                    // Deserialize the asset data into a linkset
                    using (MemoryStream stream = new MemoryStream(asset.Data))
                    {
                        OSDMap linksetMap = OSDParser.DeserializeJson(stream) as OSDMap;
                        if (linksetMap != null)
                        {
                            IList<LLPrimitive> linkset = LLPrimitive.DeserializeLinkset(linksetMap, m_scene, m_primMesher, true);

                            // Rez the parent(s) first
                            for (int i = 0; i < linkset.Count; i++)
                            {
                                LLPrimitive prim = linkset[i];

                                // Make sure the ownerID is set correctly
                                prim.OwnerID = agent.ID;

                                if (prim.Parent == null)
                                {
                                    RezSinglePrim(prim, rez.RezData, position);
                                    m_log.Debug("Deserialized root prim " + prim.ID + " (" + prim.LocalID + ") from inventory");
                                }
                            }

                            // Rez the children
                            for (int i = 0; i < linkset.Count; i++)
                            {
                                if (linkset[i].Parent != null)
                                    RezSinglePrim(linkset[i], rez.RezData, position);
                            }

                            // FIXME: Use these to determine if we need to delete the source inventory or task item
                            //rez.RezData.FromTaskID
                            //rez.RezData.RemoveItem
                        }
                        else
                        {
                            m_log.WarnFormat("Failed to deserialize asset {0} ({1} bytes, Content-Type: {2}) into a linkset",
                                asset.ID, asset.Data.Length, asset.ContentType);
                        }
                    }

                    #endregion Object Deserialization/Rezzing
                }
                else
                {
                    m_log.Warn(agent.Name + "'s RezObject failed to retrieve asset " + item.AssetID);
                }
            }
            else
            {
                m_log.Warn(agent.Name + " called RezObject for unknown item " + rez.InventoryData.ItemID);
            }
        }

        void LinkInventoryItemHandler(Packet packet, LLAgent agent)
        {
            LinkInventoryItemPacket link = (LinkInventoryItemPacket)packet;

            // Try to fetch the inventory item we're linking to
            InventoryBase linkedObj;
            if (m_inventoryClient.TryGetInventory(agent.ID, link.InventoryBlock.OldItemID, out linkedObj) && linkedObj is InventoryItem)
            {
                LLInventoryItem linkedItem = new LLInventoryItem((InventoryItem)linkedObj);

                // Create the inventory item
                LLInventoryItem item = new LLInventoryItem();
                item.AssetID = link.InventoryBlock.OldItemID;
                item.ContentType = LLUtil.LLAssetTypeToContentType(link.InventoryBlock.Type);
                item.LinkedContentType = linkedItem.ContentType;
                item.CreationDate = DateTime.UtcNow;
                item.CreatorID = linkedItem.CreatorID;
                item.Description = Utils.BytesToString(link.InventoryBlock.Description);
                item.Flags = linkedItem.Flags;
                item.GroupID = linkedItem.GroupID;
                item.GroupOwned = linkedItem.GroupOwned;
                item.ID = UUID.Zero; // Set on the server side
                item.Name = Utils.BytesToString(link.InventoryBlock.Name);
                item.OwnerID = agent.ID;
                item.ParentID = link.InventoryBlock.FolderID;
                item.Permissions = linkedItem.Permissions;
                item.SalePrice = linkedItem.SalePrice;
                item.SaleType = linkedItem.SaleType;

                if (m_inventoryClient.TryCreateItem(agent.ID, item))
                    SendItemCreatedPacket(agent, new LLInventoryItem(item), link.InventoryBlock.TransactionID, link.InventoryBlock.CallbackID);
                else
                    m_log.Warn("Failed to create new inventory link " + item.Name + " in folder " + item.ParentID + " for " + agent.Name);
            }
            else
            {
                m_log.Warn(agent.Name + " tried to create inventory link to missing item " + link.InventoryBlock.OldItemID);
            }
        }

        private void RezSinglePrim(LLPrimitive prim, RezObjectPacket.RezDataBlock rezData, Vector3 position)
        {
            // Set the target position for root prims
            if (prim.Parent == null)
                prim.RelativePosition = position;

            // TODO: Is this right?
            prim.Prim.Flags |= (PrimFlags)rezData.ItemFlags;

            if (rezData.RezSelected)
                prim.Prim.Flags |= PrimFlags.CreateSelected;

            // TODO: Is this right?
            prim.Prim.Properties.Permissions = GetDefaultPermissions();
            prim.Prim.Properties.Permissions.EveryoneMask = (PermissionMask)rezData.EveryoneMask;
            prim.Prim.Properties.Permissions.GroupMask = (PermissionMask)rezData.GroupMask;
            prim.Prim.Properties.Permissions.NextOwnerMask = (PermissionMask)rezData.NextOwnerMask;

            m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
        }

        #endregion Packet Handlers

        #region Helper Methods

        public LLInventoryItem ObjectToInventory(UUID agentID, ISceneEntity obj, byte[] assetData, string contentType, uint flags, bool isAttachment,
            UUID destinationID, UUID transactionID, bool derez, uint callbackID)
        {
            // Create the asset
            UUID assetID;
            if (m_assetClient.StoreAsset(contentType, false, false, assetData, obj.CreatorID, out assetID))
            {
                // Create the inventory item
                LLInventoryItem item = new LLInventoryItem();
                item.AssetID = assetID;
                item.ContentType = LLUtil.LLAssetTypeToContentType((int)AssetType.Object);
                item.CreationDate = DateTime.UtcNow;
                item.CreatorID = obj.CreatorID;
                item.Description = String.Empty;
                item.Flags = flags;
                item.GroupOwned = false;
                item.ID = UUID.Random();
                item.IsAttachment = isAttachment;
                item.Name = obj.Name;
                item.OwnerID = agentID;
                item.ParentID = destinationID;
                item.Permissions = GetDefaultPermissions();
                item.SalePrice = 10;
                item.SaleType = SaleType.Not;

                if (obj is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)obj;
                    item.Description = prim.Prim.Properties.Description ?? String.Empty;
                    item.GroupID = prim.Prim.Properties.GroupID;
                    item.Permissions = prim.Prim.Properties.Permissions;
                    item.SalePrice = prim.Prim.Properties.SalePrice;
                    item.SaleType = prim.Prim.Properties.SaleType;
                }

                if (m_inventoryClient.TryCreateItem(agentID, item))
                {
                    if (derez)
                    {
                        // Remove the object from the scene
                        m_scene.EntityRemove(this, obj);
                    }

                    ISceneEntity entity;
                    if (m_scene.TryGetEntity(agentID, out entity) && entity is LLAgent)
                        SendItemCreatedPacket((LLAgent)entity, item, transactionID, callbackID);

                    m_log.DebugFormat("Serialized prim {0} ({1}) to agent inventory", obj.ID, obj.LocalID);
                    return item;
                }
                else
                {
                    m_log.Warn("Failed to create an inventory item for serialized object " + obj.LocalID);
                }
            }
            else
            {
                m_log.Warn("Failed to store asset for object " + obj.LocalID);
            }

            return null;
        }

        private Dictionary<UUID, InventoryBase> FetchInventoryItems(LLAgent agent, FetchInventoryPacket.InventoryDataBlock[] blocks)
        {
            Dictionary<UUID, InventoryBase> items = new Dictionary<UUID, InventoryBase>(blocks.Length);

            int operationCount = blocks.Length;
            bool sentRequest = false;

            for (int i = 0; i < blocks.Length; i++)
            {
                UUID itemID = blocks[i].ItemID;

                // TODO: Why does the client send fetch inventory packets with ItemID=UUID.Zero?
                if (itemID != UUID.Zero)
                {
                    sentRequest = true;

                    m_scheduler.FireAndForget(
                        delegate(object o)
                        {
                            InventoryBase obj;
                            if (m_inventoryClient.TryGetInventory(agent.ID, (UUID)o, out obj))
                            {
                                lock (items)
                                    items[obj.ID] = obj;
                            }
                            --operationCount;
                        }, itemID
                    );
                }
                else
                {
                    m_log.Warn(agent.Name + "(" + agent.ID + ") tried to fetch inventory itemID UUID.Zero and ownerID " +
                        blocks[i].OwnerID);
                }
            }

            if (sentRequest)
            {
                const int SLEEP_TIME = 100;
                int forceStop = (1000 * 5 * operationCount) / SLEEP_TIME;
                int n = 0;

                while (operationCount > 0 && n < forceStop)
                {
                    Thread.Sleep(SLEEP_TIME);
                    ++n;
                }

                if (n >= forceStop)
                    m_log.Warn("FetchInventoryItems timed out, successfully fetched " + items.Count + " items");
            }

            return items;
        }

        private void CopyItem(LLAgent agent, UUID itemID, string newName, UUID targetFolderID, UUID transactionID, uint callbackID)
        {
            // Get the original object
            InventoryBase obj;
            if (m_inventoryClient.TryGetInventory(agent.ID, itemID, out obj) && obj is InventoryItem)
            {
                InventoryItem oldItem = (InventoryItem)obj;

                // The client will send an empty name for the item to be copied if the item name is not changing
                if (String.IsNullOrEmpty(newName))
                    newName = oldItem.Name;

                // Get the new folder
                InventoryBase folderObj;
                if (m_inventoryClient.TryGetInventory(agent.ID, targetFolderID, out folderObj) && folderObj is InventoryFolder)
                {
                    InventoryFolder targetFolder = (InventoryFolder)folderObj;

                    // Create the copy
                    LLInventoryItem newItem = new LLInventoryItem();
                    newItem.AssetID = oldItem.AssetID;
                    newItem.ContentType = oldItem.ContentType;
                    newItem.CreationDate = oldItem.CreationDate;
                    newItem.CreatorID = oldItem.CreatorID;
                    newItem.Description = oldItem.Description;
                    newItem.Flags = oldItem.ExtraData["flags"].AsUInteger();
                    newItem.GroupID = oldItem.ExtraData["group_id"].AsUUID();
                    newItem.GroupOwned = oldItem.ExtraData["group_owned"].AsBoolean();
                    newItem.ID = UUID.Random();
                    newItem.Name = oldItem.Name;
                    newItem.OwnerID = oldItem.OwnerID;
                    newItem.ParentID = targetFolder.ID;
                    newItem.Permissions = Permissions.FromOSD(oldItem.ExtraData["permissions"]);
                    if (newItem.Permissions == Permissions.NoPermissions)
                        newItem.Permissions = GetDefaultPermissions();
                    newItem.SalePrice = oldItem.ExtraData["sale_price"].AsInteger();
                    newItem.SaleType = (SaleType)oldItem.ExtraData["sale_type"].AsInteger();

                    if (m_inventoryClient.TryCreateItem(agent.ID, newItem))
                        SendItemCreatedPacket(agent, newItem, transactionID, callbackID);
                    else
                        m_log.Warn(agent.Name + " failed to create new inventory item " + newItem.ID + ", copied from " + oldItem.ID);
                }
                else
                {
                    m_log.Warn("CopyInventoryItem called with an unknown target folder " + targetFolderID);
                }
            }
            else
            {
                m_log.Warn(agent.Name + " sent CopyInventoryItem called for an unknown item " + itemID);
            }
        }

        private InventoryItem MoveItem(LLAgent agent, UUID itemID, string newName, UUID targetFolderID)
        {
            InventoryBase obj;
            if (m_inventoryClient.TryGetInventory(agent.ID, itemID, out obj) && obj is InventoryItem)
            {
                InventoryItem oldItem = (InventoryItem)obj;

                if (String.IsNullOrEmpty(newName))
                    newName = oldItem.Name;

                oldItem.Name = newName;
                oldItem.ParentID = targetFolderID;

                if (m_inventoryClient.TryCreateItem(agent.ID, oldItem))
                    return oldItem;
                else
                    m_log.Warn(agent.Name + " failed to move item " + oldItem.ID + " to folder " + targetFolderID);
            }
            else
            {
                m_log.Warn(agent.Name + " failed to fetch item " + itemID + " for a move to " + targetFolderID);
            }

            return null;
        }

        private InventoryFolder MoveFolder(LLAgent agent, UUID folderID, UUID targetFolderID)
        {
            InventoryBase obj;
            if (m_inventoryClient.TryGetInventory(agent.ID, folderID, out obj) && obj is InventoryFolder)
            {
                InventoryFolder oldFolder = (InventoryFolder)obj;
                oldFolder.ParentID = targetFolderID;

                if (m_inventoryClient.TryCreateFolder(agent.ID, oldFolder))
                    return oldFolder;
                else
                    m_log.Warn(agent.Name + " failed to move folder " + oldFolder.ID + " to folder " + targetFolderID);
            }
            else
            {
                m_log.Warn(agent.Name + " failed to fetch folder " + folderID + " for a move to " + targetFolderID);
            }

            return null;
        }

        private void SendItemCreatedPacket(LLAgent agent, LLInventoryItem item, UUID transactionID, uint callbackID)
        {
            UpdateCreateInventoryItemPacket update = new UpdateCreateInventoryItemPacket();
            update.AgentData.AgentID = agent.ID;
            update.AgentData.SimApproved = true;
            if (transactionID != UUID.Zero)
                update.AgentData.TransactionID = transactionID;
            else
                update.AgentData.TransactionID = UUID.Random();

            Permissions perms = item.Permissions;

            UpdateCreateInventoryItemPacket.InventoryDataBlock invData = new UpdateCreateInventoryItemPacket.InventoryDataBlock();
            invData.AssetID = item.AssetID;
            invData.BaseMask = (uint)perms.BaseMask;
            invData.CallbackID = callbackID;
            invData.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);
            invData.CRC = item.CRC();
            invData.CreatorID = item.CreatorID;
            invData.Description = Utils.StringToBytes(item.Description);
            invData.EveryoneMask = (uint)perms.EveryoneMask;
            invData.Flags = item.Flags;
            invData.FolderID = item.ParentID;
            invData.GroupID = item.GroupID;
            invData.GroupMask = (uint)perms.GroupMask;
            invData.GroupOwned = item.GroupOwned;
            invData.InvType = (sbyte)item.InventoryType;
            invData.ItemID = item.ID;
            invData.Name = Utils.StringToBytes(item.Name);
            invData.NextOwnerMask = (uint)perms.NextOwnerMask;
            invData.OwnerID = item.OwnerID;
            invData.OwnerMask = (uint)perms.OwnerMask;
            invData.SalePrice = item.SalePrice;
            invData.SaleType = (byte)item.SaleType;
            invData.Type = (sbyte)item.AssetType;

            // Handle inventory links
            InventoryType linkedInvType = item.LinkedInventoryType;
            if (linkedInvType != InventoryType.Unknown)
                invData.InvType = (sbyte)linkedInvType;

            update.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[1];
            update.InventoryData[0] = invData;

            m_log.DebugFormat("Created inventory item {0}. ItemID: {1}, AssetID: {2}, ParentID: {3}, TransactionID: {4}, CallbackID: {5}",
                item.Name, item.ID, item.AssetID, item.ParentID, transactionID, callbackID);

            m_udp.SendPacket(agent, update, ThrottleCategory.Task, false);
        }

        private void SendBulkUpdate(LLAgent agent, List<InventoryBase> objs, UUID transactionID, uint callbackID)
        {
            BulkUpdateInventoryPacket update = new BulkUpdateInventoryPacket();
            update.AgentData.AgentID = agent.ID;
            update.AgentData.TransactionID = transactionID;

            // Count the number of folders and items
            int items = 0;
            int folders = 0;
            for (int i = 0; i < objs.Count; i++)
            {
                if (objs[i] is InventoryItem)
                    ++items;
                else
                    ++folders;
            }

            update.FolderData = new BulkUpdateInventoryPacket.FolderDataBlock[folders];
            update.ItemData = new BulkUpdateInventoryPacket.ItemDataBlock[items];

            items = 0;
            folders = 0;

            for (int i = 0; i < objs.Count; i++)
            {
                InventoryBase obj = objs[i];

                if (obj is InventoryItem)
                {
                    LLInventoryItem item = new LLInventoryItem((InventoryItem)obj);
                    BulkUpdateInventoryPacket.ItemDataBlock itemData = new BulkUpdateInventoryPacket.ItemDataBlock();

                    Permissions perms = item.Permissions;

                    itemData.AssetID = item.AssetID;
                    itemData.BaseMask = (uint)perms.BaseMask;
                    itemData.CallbackID = callbackID;
                    itemData.CRC = item.CRC();
                    itemData.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);
                    itemData.CreatorID = item.CreatorID;
                    itemData.Description = Utils.StringToBytes(item.Description);
                    itemData.EveryoneMask = (uint)perms.EveryoneMask;
                    itemData.Flags = item.Flags;
                    itemData.FolderID = item.ParentID;
                    itemData.GroupID = item.GroupID;
                    itemData.GroupMask = (uint)perms.GroupMask;
                    itemData.GroupOwned = item.GroupOwned;
                    itemData.InvType = (sbyte)item.InventoryType;
                    itemData.ItemID = item.ID;
                    itemData.Name = Utils.StringToBytes(item.Name);
                    itemData.NextOwnerMask = (uint)perms.NextOwnerMask;
                    itemData.OwnerID = item.OwnerID;
                    itemData.OwnerMask = (uint)perms.OwnerMask;
                    itemData.SalePrice = item.SalePrice;
                    itemData.SaleType = (byte)item.SaleType;
                    itemData.Type = (sbyte)item.AssetType;

                    // Handle inventory links
                    InventoryType linkedInvType = item.LinkedInventoryType;
                    if (linkedInvType != InventoryType.Unknown)
                        itemData.InvType = (sbyte)linkedInvType;

                    update.ItemData[items] = itemData;
                    ++items;
                }
                else
                {
                    InventoryFolder folder = (InventoryFolder)obj;
                    BulkUpdateInventoryPacket.FolderDataBlock folderData = new BulkUpdateInventoryPacket.FolderDataBlock();

                    folderData.FolderID = folder.ID;
                    folderData.Name = Utils.StringToBytes(folder.Name);
                    folderData.ParentID = folder.ParentID;
                    folderData.Type = LLUtil.ContentTypeToLLAssetType(folder.PreferredContentType);

                    update.FolderData[folders] = folderData;
                    ++folders;
                }
            }

            m_udp.SendPacket(agent, update, ThrottleCategory.Task, true);
        }

        private Permissions GetDefaultPermissions()
        {
            Permissions perms = Permissions.NoPermissions;
            perms.BaseMask = PermissionMask.All;
            perms.OwnerMask = PermissionMask.All;
            perms.NextOwnerMask = PermissionMask.All;
            return perms;
        }

        #endregion Helper Methods
    }
}
