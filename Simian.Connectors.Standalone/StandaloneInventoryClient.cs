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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Connectors.Standalone
{
    [ApplicationModule("StandaloneInventoryClient")]
    public class StandaloneInventoryClient : IInventoryClient, IApplicationModule
    {
        private static readonly UUID HAIR_ASSET = new UUID("dc675529-7ba5-4976-b91d-dcb9e5e36188");
        private static readonly UUID PANTS_ASSET = new UUID("3e8ee2d6-4f21-4a55-832d-77daa505edff");
        private static readonly UUID SHAPE_ASSET = new UUID("530a2614-052e-49a2-af0e-534bb3c05af0");
        private static readonly UUID SHIRT_ASSET = new UUID("6a714f37-fe53-4230-b46f-8db384465981");
        private static readonly UUID SKIN_ASSET = new UUID("5f787f25-f761-4a35-9764-6418ee4774c4");
        private static readonly UUID EYES_ASSET = new UUID("78d20332-9b07-44a2-bf74-3b368605f4b5");
        private static readonly UUID LIBRARY_OWNER = new UUID("ba2a564a-f0f1-4b82-9c61-b7520bfcd09f");

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private IUserClient m_userClient;
        private Dictionary<UUID, Dictionary<UUID, InventoryBase>> m_inventory = new Dictionary<UUID, Dictionary<UUID, InventoryBase>>();
        private Dictionary<UUID, InventoryFolder> m_rootFolders = new Dictionary<UUID, InventoryFolder>();
        private FileDataStore m_fileDataStore;
        private object m_syncRoot = new object();

        public bool Start(Simian simian)
        {
            m_simian = simian;
            m_userClient = m_simian.GetAppModule<IUserClient>();
            
            // Library user and inventory creation
            string libraryOwnerName = "Library Owner";
            IConfig config = simian.Config.Configs["StandaloneInventoryClient"];
            if (config != null)
            {
                libraryOwnerName = config.GetString("LibraryOwnerName", "Library Owner");
            }
            CreateLibrary(libraryOwnerName);

            // Deserialize inventories from disk
            m_fileDataStore = m_simian.GetAppModule<FileDataStore>();
            if (m_fileDataStore != null)
            {
                IList<SerializedData> inventories = m_fileDataStore.Deserialize(UUID.Zero, "Inventory");
                for (int i = 0; i < inventories.Count; i++)
                {
                    string name = inventories[i].Name;

                    UUID ownerID;
                    if (UUID.TryParse(name, out ownerID))
                    {
                        OSDMap map = null;
                        try { map = OSDParser.Deserialize(inventories[i].Data) as OSDMap; }
                        catch (Exception) { }

                        if (map != null)
                            DeserializeInventory(ownerID, map);
                        else
                            m_log.Warn("Failed to deserialize inventory file " + name);
                    }
                }
            }

            return true;
        }

        public void Stop()
        {
        }

        #region IInventoryClient

        public bool TryCreateItem(UUID presenceID, InventoryItem item)
        {
            bool success = false;

            if (IsVerified(presenceID))
            {
                Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(presenceID);

                if (item.ID == UUID.Zero)
                    item.ID = UUID.Random();

                lock (m_syncRoot)
                {
                    InventoryBase existingObj;
                    if (TryGetInventory(presenceID, item.ID, out existingObj) && existingObj is InventoryItem)
                    {
                        InventoryItem existingItem = (InventoryItem)existingObj;
                        m_log.DebugFormat("Updating inventory item {0} ({1})", item.Name, item.ContentType);

                        // Set the item name
                        if (String.IsNullOrEmpty(item.Name))
                            item.Name = existingItem.Name;

                        // Set the parent folder
                        if (item.ParentID == UUID.Zero)
                        {
                            item.ParentID = existingItem.ParentID;
                        }
                        else if (item.ParentID != existingItem.ParentID)
                        {
                            // Remove this item from the previous parent
                            InventoryBase oldParentFolderObj;
                            if (agentInventory.TryGetValue(existingItem.ParentID, out oldParentFolderObj) && oldParentFolderObj is InventoryFolder)
                            {
                                InventoryFolder oldParentFolder = (InventoryFolder)oldParentFolderObj;
                                lock (oldParentFolder.Children)
                                    oldParentFolder.Children.Remove(existingItem.ID);
                                ++oldParentFolder.Version;
                            }
                        }
                    }
                    else
                    {
                        m_log.DebugFormat("Creating inventory item {0} ({1})", item.Name, item.ContentType);

                        // Set the parent folder
                        if (item.ParentID == UUID.Zero)
                        {
                            InventoryFolder rootFolder;
                            if (m_rootFolders.TryGetValue(presenceID, out rootFolder))
                                item.ParentID = GetDefaultFolder(rootFolder, item.ContentType);
                            else
                                m_log.Warn("Could not find the root inventory folder for " + presenceID);
                        }
                    }

                    InventoryBase parent;
                    if (agentInventory.TryGetValue(item.ParentID, out parent) && parent is InventoryFolder)
                    {
                        InventoryFolder parentFolder = (InventoryFolder)parent;

                        // Add or overwrite the inventory item
                        agentInventory[item.ID] = item;

                        // Add this folder to the new parent
                        lock (parentFolder.Children)
                            parentFolder.Children[item.ID] = item;
                        ++parentFolder.Version;

                        SerializeInventory(presenceID, agentInventory);
                        success = true;
                    }
                }
            }

            return success;
        }

        public bool TryCreateFolder(UUID presenceID, InventoryFolder folder)
        {
            bool success = false;

            if (IsVerified(presenceID))
            {
                Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(presenceID);

                if (folder.ID == UUID.Zero)
                    folder.ID = UUID.Random();

                lock (m_syncRoot)
                {
                    // Set the version number and possibly the folder name
                    InventoryBase existingObj;
                    if (TryGetInventory(presenceID, folder.ID, out existingObj) && existingObj is InventoryFolder)
                    {
                        InventoryFolder existingFolder = (InventoryFolder)existingObj;
                        m_log.Debug("Updating inventory folder " + folder.Name);

                        // Set the version number
                        folder.Version = existingFolder.Version + 1;

                        // Set the folder name
                        if (String.IsNullOrEmpty(folder.Name))
                            folder.Name = existingFolder.Name;

                        // Set the parent folder
                        if (folder.ParentID == UUID.Zero)
                        {
                            folder.ParentID = existingFolder.ParentID;
                        }
                        else if (folder.ParentID != existingFolder.ParentID)
                        {
                            // Remove this folder from the previous parent
                            InventoryBase oldParentFolderObj;
                            if (agentInventory.TryGetValue(existingFolder.ParentID, out oldParentFolderObj) && oldParentFolderObj is InventoryFolder)
                            {
                                InventoryFolder oldParentFolder = (InventoryFolder)oldParentFolderObj;
                                lock (oldParentFolder.Children)
                                    oldParentFolder.Children.Remove(existingFolder.ID);
                                ++oldParentFolder.Version;
                            }
                        }
                    }
                    else
                    {
                        m_log.Debug("Creating inventory folder " + folder.Name);
                        folder.Version = 1;

                        // Set the parent folder
                        if (folder.ParentID == UUID.Zero)
                        {
                            InventoryFolder rootFolder;
                            if (m_rootFolders.TryGetValue(presenceID, out rootFolder))
                                folder.ParentID = GetDefaultFolder(rootFolder, "application/vnd.ll.folder");
                            else
                                m_log.Warn("Could not find the root inventory folder for " + presenceID);
                        }
                    }

                    InventoryBase parent;
                    if (agentInventory.TryGetValue(folder.ParentID, out parent) && parent is InventoryFolder)
                    {
                        InventoryFolder parentFolder = (InventoryFolder)parent;

                        // Add or overwrite the inventory folder
                        agentInventory[folder.ID] = folder;

                        // Add this folder to the new parent
                        lock (parentFolder.Children)
                            parentFolder.Children[folder.ID] = folder;
                        ++parentFolder.Version;

                        SerializeInventory(presenceID, agentInventory);
                        success = true;
                    }
                    else
                    {
                        m_log.WarnFormat("Cannot create new inventory folder, parent folder {0} does not exist", folder.ParentID);
                    }
                }
            }

            return success;
        }

        public bool TryCreateRootFolder(UUID presenceID, string name, out UUID rootFolderID)
        {
            bool success = false;
            rootFolderID = UUID.Random();

            if (IsVerified(presenceID))
            {
                Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(presenceID);

                lock (m_syncRoot)
                {
                    if (!agentInventory.ContainsKey(rootFolderID))
                    {
                        InventoryFolder folder = new InventoryFolder();
                        folder.Name = name;
                        folder.OwnerID = presenceID;
                        folder.ParentID = UUID.Zero;
                        folder.PreferredContentType = "application/vnd.ll.folder";
                        folder.ID = rootFolderID;
                        folder.Version = 1;

                        m_log.Debug("Creating root inventory folder " + folder.Name);

                        // Store the inventory folder
                        agentInventory[folder.ID] = folder;
                        m_rootFolders[presenceID] = folder;

                        SerializeInventory(presenceID, agentInventory);
                        success = true;
                    }
                    else
                    {
                        m_log.WarnFormat("Cannot create root inventory folder, item {0} already exists", rootFolderID);
                    }
                }
            }

            return success;
        }

        public bool TryRemoveNodes(UUID presenceID, IList<UUID> nodeIDs)
        {
            lock (m_syncRoot)
            {
                Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(presenceID);

                for (int i = 0; i < nodeIDs.Count; i++)
                {
                    InventoryBase node;
                    if (agentInventory.TryGetValue(nodeIDs[i], out node))
                    {
                        if (node is InventoryFolder)
                            PurgeFolder(presenceID, node.ID);

                        agentInventory.Remove(node.ID);
                    }
                    else
                    {
                        m_log.Warn("Cannot remove missing inventory node " + nodeIDs[i] + " for " + presenceID);
                    }
                }

                SerializeInventory(presenceID, agentInventory);
                return true;
            }
        }

        public bool TryPurgeFolder(UUID presenceID, UUID folderID)
        {
            bool success = false;

            if (IsVerified(presenceID))
            {
                lock (m_syncRoot)
                {
                    success = PurgeFolder(presenceID, folderID);
                    SerializeInventory(presenceID, GetAgentInventory(presenceID));
                }
            }

            return success;
        }

        public bool TryGetInventory(UUID presenceID, UUID objectID, out InventoryBase obj)
        {
            if (IsVerified(presenceID))
            {
                Dictionary<UUID, InventoryBase> inventory;

                // Agent inventory lookup
                if (m_inventory.TryGetValue(presenceID, out inventory) && inventory.TryGetValue(objectID, out obj))
                    return true;

                // Library lookup
                if (m_inventory.TryGetValue(LIBRARY_OWNER, out inventory) && inventory.TryGetValue(objectID, out obj))
                    return true;
            }

            obj = null;
            return false;
        }

        public bool TryGetInventorySkeleton(UUID presenceID, out InventorySkeleton skeleton)
        {
            bool success = false;
            skeleton = new InventorySkeleton();

            if (IsVerified(presenceID))
            {
                Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(presenceID);
                Dictionary<UUID, InventoryBase> libraryInventory = GetAgentInventory(LIBRARY_OWNER);

                skeleton.Skeleton = new InventoryFolder();
                skeleton.LibrarySkeleton = new InventoryFolder();

                lock (m_syncRoot)
                {
                    foreach (InventoryBase obj in agentInventory.Values)
                    {
                        if (obj is InventoryFolder)
                        {
                            InventoryFolder folderObj = (InventoryFolder)obj;
                            if (folderObj.ParentID == UUID.Zero)
                            {
                                skeleton.Skeleton.ID = folderObj.ID;
                                skeleton.Skeleton.Name = folderObj.Name;
                                skeleton.Skeleton.ExtraData = folderObj.ExtraData;
                                skeleton.Skeleton.OwnerID = folderObj.OwnerID;
                                skeleton.Skeleton.PreferredContentType = folderObj.PreferredContentType;
                                skeleton.Skeleton.Version = folderObj.Version;

                                skeleton.RootFolderID = folderObj.ID;
                            }
                            else
                            {
                                skeleton.Skeleton.Children.Add(folderObj.ID, folderObj);
                            }
                        }
                    }

                    foreach (InventoryBase obj in libraryInventory.Values)
                    {
                        if (obj is InventoryFolder)
                        {
                            InventoryFolder folderObj = (InventoryFolder)obj;
                            if (folderObj.ParentID == UUID.Zero)
                            {
                                skeleton.LibrarySkeleton.ID = folderObj.ID;
                                skeleton.LibrarySkeleton.ExtraData = folderObj.ExtraData;
                                skeleton.LibrarySkeleton.Name = folderObj.Name;
                                skeleton.LibrarySkeleton.OwnerID = folderObj.OwnerID;
                                skeleton.LibrarySkeleton.PreferredContentType = folderObj.PreferredContentType;
                                skeleton.LibrarySkeleton.Version = folderObj.Version;

                                skeleton.LibraryFolderID = folderObj.ID;
                                skeleton.LibraryOwner = folderObj.OwnerID;
                            }
                            else
                            {
                                skeleton.LibrarySkeleton.Children.Add(folderObj.ID, folderObj);
                            }
                        }
                    }
                }

                m_log.Debug("Fetched inventory skeleton for " + presenceID + ". RootFolderID= " +
                    skeleton.RootFolderID + ", LibraryFolderID=" + skeleton.LibraryFolderID);
                success = true;
            }

            return success;
        }

        public bool TryGetAssetIDs(UUID presenceID, UUID[] itemIDs, out IDictionary<UUID, UUID> itemsToAssetIDs)
        {
            bool success = false;
            itemsToAssetIDs = null;

            if (IsVerified(presenceID))
            {
                HashSet<UUID> itemIDHashSet = new HashSet<UUID>();
                for (int i = 0; i < itemIDs.Length; i++)
                    itemIDHashSet.Add(itemIDs[i]);

                Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(presenceID);
                itemsToAssetIDs = new Dictionary<UUID, UUID>();

                lock (m_syncRoot)
                {
                    foreach (InventoryBase obj in agentInventory.Values)
                    {
                        if (itemIDHashSet.Contains(obj.ID) && obj is InventoryItem)
                        {
                            InventoryItem item = (InventoryItem)obj;
                            itemsToAssetIDs[item.ID] = item.AssetID;
                        }
                    }
                }

                success = true;
            }

            return success;
        }

        public bool TryGetItemsByAssetID(UUID presenceID, UUID assetID, out IList<InventoryItem> items)
        {
            bool success = false;
            items = null;

            if (IsVerified(presenceID))
            {
                Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(presenceID);
                items = new List<InventoryItem>();

                lock (m_syncRoot)
                {
                    foreach (InventoryBase obj in agentInventory.Values)
                    {
                        if (obj is InventoryItem && ((InventoryItem)obj).AssetID == assetID)
                        {
                            InventoryItem item = (InventoryItem)obj;
                            items.Add(item);
                        }
                    }
                }

                success = true;
            }

            return success;
        }

        #endregion IInventoryClient

        #region Helper Methods

        private bool IsVerified(UUID presenceID)
        {
            User user;
            if (m_userClient != null && m_userClient.TryGetUser(presenceID, out user))
                return (user.AccessLevel > 0);

            return false;
        }

        private bool PurgeFolder(UUID agentID, UUID folderID)
        {
            bool success = true;
            Dictionary<UUID, InventoryBase> agentInventory = GetAgentInventory(agentID);

            InventoryBase obj;
            if (TryGetInventory(agentID, folderID, out obj) && obj is InventoryFolder)
            {
                InventoryFolder folder = (InventoryFolder)obj;

                lock (folder.Children)
                {
                    foreach (InventoryBase child in folder.Children.Values)
                    {
                        agentInventory.Remove(child.ID);

                        if (child is InventoryFolder)
                            success &= PurgeFolder(agentID, child.ID);
                    }

                    folder.Children.Clear();
                }

                ++folder.Version;
            }
            else
            {
                m_log.Warn("PurgeFolder called on a missing folder " + folderID);
                success = false;
            }

            return success;
        }

        private UUID GetDefaultFolder(InventoryFolder rootFolder, string preferredContentType)
        {
            InventoryBase folderObj = null;

            lock (rootFolder.Children)
            {
                foreach (InventoryBase child in rootFolder.Children.Values)
                {
                    if (child is InventoryFolder && ((InventoryFolder)child).PreferredContentType.Equals(preferredContentType, StringComparison.InvariantCultureIgnoreCase))
                    {
                        folderObj = child;
                        break;
                    }
                }
            }

            if (folderObj != null)
                return folderObj.ID;
            else
                return rootFolder.ID;
        }

        private Dictionary<UUID, InventoryBase> GetAgentInventory(UUID agentID)
        {
            lock (m_syncRoot)
            {
                if (!m_inventory.ContainsKey(agentID))
                {
                    m_log.Info("Creating a new inventory for agent " + agentID);

                    InventoryFolder rootFolder;
                    m_inventory[agentID] = CreateInventory(agentID, out rootFolder);
                    m_rootFolders[agentID] = rootFolder;
                }

                return m_inventory[agentID];
            }
        }

        #endregion Helper Methods

        #region Inventory Creation Methods

        private void CreateLibrary(string libraryOwnerName)
        {
            // User creation
            if (m_userClient != null)
            {
                User libraryUser;
                if (!m_userClient.TryGetUser(LIBRARY_OWNER, out libraryUser))
                {
                    // Create the library owner
                    libraryUser = new User
                    {
                        AccessLevel = 1,
                        Email = "INVALID " + UUID.Random().ToString(),
                        ID = LIBRARY_OWNER,
                        LastLogin = DateTime.UtcNow,
                        Name = libraryOwnerName
                    };

                    if (!m_userClient.CreateUser(libraryUser))
                        m_log.ErrorFormat("Failed to create library owner \"{0}\" ({1})", libraryOwnerName, LIBRARY_OWNER);
                }
            }

            // Inventory creation
            Dictionary<UUID, InventoryBase> library = new Dictionary<UUID, InventoryBase>();

            UUID libraryRoot = CreateFolder(library, UUID.Zero, "Library", "application/vnd.ll.folder", LIBRARY_OWNER);
            UUID clothingFolder = CreateFolder(library, libraryRoot, "Clothing", "application/vnd.ll.clothing", LIBRARY_OWNER);

            m_inventory[LIBRARY_OWNER] = library;

            m_log.InfoFormat("Created library owned by \"{0}\" ({1})", libraryOwnerName, LIBRARY_OWNER);
        }

        private Dictionary<UUID, InventoryBase> CreateInventory(UUID ownerID, out InventoryFolder rootFolder)
        {
            Dictionary<UUID, InventoryBase> inventory = new Dictionary<UUID, InventoryBase>();

            // Create a default inventory
            UUID rootFolderID = CreateFolder(inventory, UUID.Zero, "My Inventory", "application/vnd.ll.folder", ownerID);
            rootFolder = (InventoryFolder)inventory[rootFolderID];

            CreateFolder(inventory, rootFolderID, "Animations", "application/vnd.ll.animation", ownerID);
            CreateFolder(inventory, rootFolderID, "Body Parts", "application/vnd.ll.bodypart", ownerID);
            CreateFolder(inventory, rootFolderID, "Calling Cards", "application/vnd.ll.callingcard", ownerID);
            CreateFolder(inventory, rootFolderID, "Gestures", "application/vnd.ll.gesture", ownerID);
            CreateFolder(inventory, rootFolderID, "Landmarks", "application/vnd.ll.landmark", ownerID);
            CreateFolder(inventory, rootFolderID, "Lost and Found", "application/vnd.ll.lostandfoundfolder", ownerID);
            CreateFolder(inventory, rootFolderID, "Notecards", "application/vnd.ll.notecard", ownerID);
            CreateFolder(inventory, rootFolderID, "Objects", "application/vnd.ll.primitive", ownerID);
            CreateFolder(inventory, rootFolderID, "Photo Album", "application/vnd.ll.snapshotfolder", ownerID);
            CreateFolder(inventory, rootFolderID, "Scripts", "application/vnd.ll.lsltext", ownerID);
            CreateFolder(inventory, rootFolderID, "Sounds", "audio/ogg", ownerID);
            CreateFolder(inventory, rootFolderID, "Textures", "image/x-j2c", ownerID);
            CreateFolder(inventory, rootFolderID, "Trash", "application/vnd.ll.trashfolder", ownerID);

            UUID clothingFolder = CreateFolder(inventory, rootFolderID, "Clothing", "application/vnd.ll.clothing", ownerID);
            UUID outfitFolder = CreateFolder(inventory, clothingFolder, "Default Outfit", "application/octet-stream", ownerID);

            UUID hairItem = CreateItem(inventory, outfitFolder, "Default Hair", "Default Hair", WearableType.Hair, HAIR_ASSET, "application/vnd.ll.bodypart", ownerID);
            UUID pantsItem = CreateItem(inventory, outfitFolder, "Default Pants", "Default Pants", WearableType.Pants, PANTS_ASSET, "application/vnd.ll.clothing", ownerID);
            UUID shapeItem = CreateItem(inventory, outfitFolder, "Default Shape", "Default Shape", WearableType.Shape, SHAPE_ASSET, "application/vnd.ll.bodypart", ownerID);
            UUID shirtItem = CreateItem(inventory, outfitFolder, "Default Shirt", "Default Shirt", WearableType.Shirt, SHIRT_ASSET, "application/vnd.ll.clothing", ownerID);
            UUID skinItem = CreateItem(inventory, outfitFolder, "Default Skin", "Default Skin", WearableType.Skin, SKIN_ASSET, "application/vnd.ll.bodypart", ownerID);
            UUID eyesItem = CreateItem(inventory, outfitFolder, "Default Eyes", "Default Eyes", WearableType.Eyes, EYES_ASSET, "application/vnd.ll.bodypart", ownerID);

            if (m_userClient != null)
            {
                OSDMap appearanceMap = new OSDMap
                {
                    { "Height", OSD.FromReal(1.771488d) },
                    { "ShapeItem", OSD.FromUUID(shapeItem) },
                    { "ShapeAsset", OSD.FromUUID(SHAPE_ASSET) },
                    { "EyesItem", OSD.FromUUID(eyesItem) },
                    { "EyesAsset", OSD.FromUUID(EYES_ASSET) },
                    { "HairItem", OSD.FromUUID(hairItem) },
                    { "HairAsset", OSD.FromUUID(HAIR_ASSET) },
                    { "PantsItem", OSD.FromUUID(pantsItem) },
                    { "PantsAsset", OSD.FromUUID(PANTS_ASSET) },
                    { "ShirtItem", OSD.FromUUID(shirtItem) },
                    { "ShirtAsset", OSD.FromUUID(SHIRT_ASSET) },
                    { "SkinItem", OSD.FromUUID(skinItem) },
                    { "SkinAsset", OSD.FromUUID(SKIN_ASSET) }
                };

                m_userClient.UpdateUserFields(ownerID, new OSDMap { { "LLAppearance", appearanceMap } });
            }

            return inventory;
        }

        private static UUID CreateFolder(Dictionary<UUID, InventoryBase> inventory, UUID parentID, string name, string preferredType, UUID agentID)
        {
            InventoryFolder folder = new InventoryFolder();
            folder.Children = new Dictionary<UUID, InventoryBase>();
            folder.ID = UUID.Random();
            folder.Name = name;
            folder.OwnerID = agentID;
            folder.ParentID = parentID;
            folder.PreferredContentType = preferredType;
            folder.Version = 1;

            InventoryBase parent;
            if (inventory.TryGetValue(parentID, out parent) && parent is InventoryFolder)
                ((InventoryFolder)parent).Children[folder.ID] = folder;

            inventory[folder.ID] = folder;
            return folder.ID;
        }

        private static UUID CreateItem(Dictionary<UUID, InventoryBase> inventory, UUID parentID, string name, string description,
            WearableType wearableType, UUID assetID, string assetType, UUID agentID)
        {
            InventoryItem item = new InventoryItem();
            item.ID = UUID.Random();
            item.Name = name;
            item.Description = description;
            item.OwnerID = agentID;
            item.ParentID = parentID;
            item.AssetID = assetID;
            item.ContentType = assetType;
            item.CreationDate = DateTime.UtcNow;
            
            // HACK: Set LLInventoryItem flags
            item.ExtraData["flags"] = OSD.FromInteger((int)wearableType);

            // HACK: Set default LLInventoryItem permissions
            Permissions perm = Permissions.NoPermissions;
            perm.BaseMask = PermissionMask.All;
            perm.OwnerMask = PermissionMask.All;
            perm.NextOwnerMask = PermissionMask.All;
            item.ExtraData["permissions"] = perm.GetOSD();

            InventoryBase parent;
            if (inventory.TryGetValue(parentID, out parent) && parent is InventoryFolder)
                ((InventoryFolder)parent).Children[item.ID] = item;

            inventory[item.ID] = item;
            return item.ID;
        }

        #endregion Inventory Creation Methods

        #region Serialization/Deserialization

        private void SerializeInventory(UUID ownerID, Dictionary<UUID, InventoryBase> inventory)
        {
            if (m_fileDataStore != null)
            {
                OSDMap map = new OSDMap(inventory.Count);

                foreach (KeyValuePair<UUID, InventoryBase> kvp in inventory)
                    map[kvp.Key.ToString()] = SerializeInventoryNode(kvp.Value);

                SerializedData data = new SerializedData
                {
                    StoreID = UUID.Zero,
                    Section = "Inventory",
                    ContentType = "application/llsd+json",
                    Name = ownerID.ToString(),
                    Data = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(map)),
                    Version = 1
                };

                m_fileDataStore.BeginSerialize(data);
            }
        }

        private OSDMap SerializeInventoryNode(InventoryBase node)
        {
            OSDMap map = new OSDMap();

            if (node is InventoryItem)
            {
                InventoryItem item = (InventoryItem)node;
                map["asset_id"] = OSD.FromUUID(item.AssetID);
                map["content_type"] = OSD.FromString(item.ContentType);
                map["creation_date"] = OSD.FromDate(item.CreationDate);
                map["creator_id"] = OSD.FromUUID(item.CreatorID);
                map["description"] = OSD.FromString(item.Description);
                map["extra_data"] = item.ExtraData;
                map["id"] = OSD.FromUUID(item.ID);
                map["name"] = OSD.FromString(item.Name);
                map["owner_id"] = OSD.FromUUID(item.OwnerID);
                map["parent_id"] = OSD.FromUUID(item.ParentID);
            }
            else if (node is InventoryFolder)
            {
                InventoryFolder folder = (InventoryFolder)node;
                map["extra_data"] = folder.ExtraData;
                map["id"] = OSD.FromUUID(folder.ID);
                map["name"] = OSD.FromString(folder.Name);
                map["owner_id"] = OSD.FromUUID(folder.OwnerID);
                map["parent_id"] = OSD.FromUUID(folder.ParentID);
                map["content_type"] = OSD.FromString(folder.PreferredContentType);
                map["version"] = OSD.FromInteger(folder.Version);
            }
            else
            {
                m_log.Warn("Unrecognized inventory node " + node);
            }

            return map;
        }

        private void DeserializeInventory(UUID ownerID, OSDMap map)
        {
            Dictionary<UUID, InventoryBase> inventory = new Dictionary<UUID, InventoryBase>(map.Count);

            // Deserialize each node
            foreach (KeyValuePair<string, OSD> kvp in map)
            {
                UUID nodeID;
                OSDMap nodeMap = kvp.Value as OSDMap;
                if (nodeMap != null && UUID.TryParse(kvp.Key, out nodeID))
                {
                    InventoryBase node = DeserializeInventoryNode(nodeMap);
                    inventory[nodeID] = node;

                    if (node is InventoryFolder && node.ParentID == UUID.Zero)
                        m_rootFolders[ownerID] = (InventoryFolder)node;
                }
            }

            // Hook up children nodes to their parents
            foreach (KeyValuePair<UUID, InventoryBase> kvp in inventory)
            {
                InventoryBase parent;

                if (kvp.Value.ParentID != UUID.Zero && inventory.TryGetValue(kvp.Value.ParentID, out parent) && parent is InventoryFolder)
                {
                    InventoryFolder parentFolder = (InventoryFolder)parent;
                    parentFolder.Children[kvp.Key] = kvp.Value;
                }
            }

            m_inventory[ownerID] = inventory;
        }

        private InventoryBase DeserializeInventoryNode(OSDMap map)
        {
            if (map.ContainsKey("asset_id"))
            {
                InventoryItem item = new InventoryItem();
                item.AssetID = map["asset_id"].AsUUID();
                item.ContentType = map["content_type"].AsString();
                item.CreationDate = map["creation_date"].AsDate();
                item.CreatorID = map["creator_id"].AsUUID();
                item.Description = map["description"].AsString();
                item.ExtraData = map["extra_data"] as OSDMap;
                item.ID = map["id"].AsUUID();
                item.Name = map["name"].AsString();
                item.OwnerID = map["owner_id"].AsUUID();
                item.ParentID = map["parent_id"].AsUUID();

                return item;
            }
            else
            {
                InventoryFolder folder = new InventoryFolder();
                folder.Children = new Dictionary<UUID, InventoryBase>();
                folder.ExtraData = map["extra_data"] as OSDMap;
                folder.ID = map["id"].AsUUID();
                folder.Name = map["name"].AsString();
                folder.OwnerID = map["owner_id"].AsUUID();
                folder.ParentID = map["parent_id"].AsUUID();
                folder.PreferredContentType = map["content_type"].AsString();
                folder.Version = map["version"].AsInteger();

                return folder;
            }
        }

        #endregion Serialization/Deserialization
    }
}
