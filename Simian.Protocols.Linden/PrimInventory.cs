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
using System.Text;
using OpenMetaverse;

namespace Simian.Protocols.Linden
{
    /// <summary>
    /// Helper class to convert task inventory listings to a serialized format
    /// </summary>
    public class TaskInventoryStringBuilder
    {
        private StringBuilder m_builder = new StringBuilder();

        public TaskInventoryStringBuilder(UUID folderID, UUID parentID)
        {
            m_builder.Append("\tinv_object\t0\n\t{\n");
            AddNameValueLine("obj_id", folderID.ToString());
            AddNameValueLine("parent_id", parentID.ToString());
            AddNameValueLine("type", "category");
            AddNameValueLine("name", "Contents|");
            AddSectionEnd();
        }

        public void AddItemStart()
        {
            m_builder.Append("\tinv_item\t0\n");
            AddSectionStart();
        }

        public void AddPermissionsStart()
        {
            m_builder.Append("\tpermissions 0\n");
            AddSectionStart();
        }

        public void AddSaleStart()
        {
            m_builder.Append("\tsale_info\t0\n");
            AddSectionStart();
        }

        protected void AddSectionStart()
        {
            m_builder.Append("\t{\n");
        }

        public void AddSectionEnd()
        {
            m_builder.Append("\t}\n");
        }

        public void AddLine(string addLine)
        {
            m_builder.Append(addLine);
        }

        public void AddNameValueLine(string name, string value)
        {
            m_builder.Append("\t\t");
            m_builder.Append(name);
            m_builder.Append("\t");
            m_builder.Append(value);
            m_builder.Append("\n");
        }

        public override string ToString()
        {
            return m_builder.ToString();
        }
    }

    public class PrimInventory
    {
        /// <summary>For "decrypting" shadowed AssetIDs. Unlikely that this is 
        /// actually used</summary>
        private static readonly UUID MAGIC_ID = new UUID("3c115e51-04f4-523c-9fa6-98aff1034730");

        private LLPrimitive m_hostObject;
        private string m_inventoryFilename;
        private short m_inventoryFilenameSerial;
        private DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> m_items = new DoubleDictionarySlim<UUID, string, LLInventoryTaskItem>();

        private DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> Items
        {
            get { if (m_items == null) { m_items = new DoubleDictionarySlim<UUID, string, LLInventoryTaskItem>(); } return m_items; }
            set { m_items = value; }
        }

        public short InventorySerial
        {
            get { return m_hostObject.Prim.Properties.InventorySerial; }
            set { m_hostObject.Prim.Properties.InventorySerial = value; }
        }

        public PrimInventory(LLPrimitive hostObject)
        {
            m_hostObject = hostObject;
        }

        public void ChangeInventoryOwner(UUID newOwnerID)
        {
            // TODO: Do we need to do anything when the owner of the parent object changes?
        }

        public void ChangeInventoryGroup(UUID newGroupID)
        {
            // TODO: Do we need to do anything when the group owner of the parent object changes?
        }

        public IList<LLInventoryTaskItem> GetScripts()
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;
            return items.FindAll(delegate(LLInventoryTaskItem match) { return match.AssetType == AssetType.LSLText; });
        }

        public void AddOrUpdateItem(LLInventoryTaskItem item, bool replace)
        {
            item.ParentObjectID = m_hostObject.Prim.ID;
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;

            if (replace)
            {
                LLInventoryTaskItem oldItem;
                if (items.TryGetValue(item.Name, out oldItem))
                {
                    item.ID = oldItem.ID;
                    items.Remove(item.ID, item.Name);
                }
            }
            else
            {
                item.Name = NextAvailableFilename(items, item.Name);
            }

            if (item.ID == UUID.Zero)
                item.ID = UUID.Random();

            items.Add(item.ID, item.Name, item);

            // Update the inventory serial number
            ++InventorySerial;

            // Post a script event
            // FIXME:
            //Changed change = allowedDrop ? Changed.ALLOWED_DROP : Changed.INVENTORY;
            //m_hostObject.Scene.ScriptEngine.PostObjectEvent(m_hostObject.Prim.ID, "changed",
            //    new object[] { new ScriptTypes.LSL_Integer((uint)change) }, new DetectParams[0]);
        }

        public InventoryType RemoveItem(UUID itemID)
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;

            LLInventoryTaskItem item;
            if (items.TryGetValue(itemID, out item))
            {
                items.Remove(itemID, item.Name);

                // Update the inventory serial number
                ++InventorySerial;

                // Post a script event
                // FIXME:
                //m_hostObject.Scene.ScriptEngine.PostObjectEvent(hostObject.Prim.ID, "changed",
                //    new object[] { new ScriptTypes.LSL_Integer((uint)Changed.INVENTORY) }, new DetectParams[0]);

                // FIXME: Check if this prim still classifies as "scripted"

                return (InventoryType)LLUtil.ContentTypeToLLInvType(item.ContentType);
            }
            else
            {
                return InventoryType.Unknown;
            }
        }

        public bool TryGetItem(UUID itemID, out LLInventoryTaskItem item)
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;
            return items.TryGetValue(itemID, out item);
        }

        public bool TryGetItem(string name, out LLInventoryTaskItem item)
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;
            return items.TryGetValue(name, out item);
        }

        public string GetInventoryFilename()
        {
            if (InventorySerial > 0)
            {
                short inventorySerial = InventorySerial;

                if (String.IsNullOrEmpty(m_inventoryFilename) || m_inventoryFilenameSerial < inventorySerial)
                    m_inventoryFilename = "inventory_" + m_hostObject.ID + "_" + inventorySerial + ".tmp";

                m_inventoryFilenameSerial = inventorySerial;

                return m_inventoryFilename;
            }
            else
            {
                return String.Empty;
            }
        }

        public void ForEachItem(Action<LLInventoryTaskItem> action)
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;
            items.ForEach(action);
        }

        public LLInventoryTaskItem FindItem(Predicate<LLInventoryTaskItem> match)
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;
            return items.FindValue(match);
        }

        public IList<LLInventoryTaskItem> FindAllItems(Predicate<LLInventoryTaskItem> match)
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;
            return items.FindAll(match);
        }

        public string GetTaskInventoryAsset()
        {
            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = Items;
            TaskInventoryStringBuilder invString = new TaskInventoryStringBuilder(m_hostObject.Prim.ID, UUID.Zero);

            items.ForEach(
                delegate(LLInventoryTaskItem item)
                {
                    invString.AddItemStart();
                    invString.AddNameValueLine("item_id", item.ID.ToString());
                    invString.AddNameValueLine("parent_id", m_hostObject.Prim.ID.ToString());

                    invString.AddPermissionsStart();

                    invString.AddNameValueLine("base_mask", Utils.UIntToHexString((uint)item.Permissions.BaseMask));
                    invString.AddNameValueLine("owner_mask", Utils.UIntToHexString((uint)item.Permissions.OwnerMask));
                    invString.AddNameValueLine("group_mask", Utils.UIntToHexString((uint)item.Permissions.GroupMask));
                    invString.AddNameValueLine("everyone_mask", Utils.UIntToHexString((uint)item.Permissions.EveryoneMask));
                    invString.AddNameValueLine("next_owner_mask", Utils.UIntToHexString((uint)item.Permissions.NextOwnerMask));

                    invString.AddNameValueLine("creator_id", item.CreatorID.ToString());
                    invString.AddNameValueLine("owner_id", item.OwnerID.ToString());

                    invString.AddNameValueLine("last_owner_id", item.CreatorID.ToString()); // FIXME: Do we need InventoryItem.LastOwnerID?

                    invString.AddNameValueLine("group_id", item.GroupID.ToString());
                    invString.AddSectionEnd();

                    invString.AddNameValueLine("asset_id", item.AssetID.ToString());
                    invString.AddNameValueLine("type", Utils.AssetTypeToString(item.AssetType));
                    invString.AddNameValueLine("inv_type", Utils.InventoryTypeToString(item.InventoryType));
                    invString.AddNameValueLine("flags", Utils.UIntToHexString(item.Flags));

                    invString.AddSaleStart();
                    invString.AddNameValueLine("sale_type", Utils.SaleTypeToString(item.SaleType));
                    invString.AddNameValueLine("sale_price", item.SalePrice.ToString());
                    invString.AddSectionEnd();

                    invString.AddNameValueLine("name", item.Name + "|");
                    invString.AddNameValueLine("desc", item.Description + "|");

                    invString.AddNameValueLine("creation_date", Utils.DateTimeToUnixTime(item.CreationDate).ToString());
                    invString.AddSectionEnd();
                }
            );

            return invString.ToString();
        }

        public void FromTaskInventoryAsset(string asset)
        {
            if (String.IsNullOrEmpty(asset))
            {
                if (m_items != null)
                    m_items.Clear();
                return;
            }

            DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items = new DoubleDictionarySlim<UUID, string, LLInventoryTaskItem>();
            List<LLInventoryTaskItem> parsedItems = ParseTaskInventory(m_hostObject.ID, asset);

            for (int i = 0; i < parsedItems.Count; i++)
            {
                LLInventoryTaskItem item = parsedItems[i];
                items.Add(item.ID, item.Name, item);
            }

            Items = items;
        }

        private static string NextAvailableFilename(DoubleDictionarySlim<UUID, string, LLInventoryTaskItem> items, string name)
        {
            string tryName = name;
            int suffix = 1;

            while (items.ContainsKey(tryName) && suffix < 256)
                tryName = String.Format("{0} {1}", name, suffix++);

            return tryName;
        }

        private static List<LLInventoryTaskItem> ParseTaskInventory(UUID parentObjectID, string taskData)
        {
            List<LLInventoryTaskItem> items = new List<LLInventoryTaskItem>();
            int lineNum = 0;
            string[] lines = taskData.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            while (lineNum < lines.Length)
            {
                string key, value;
                if (ParseLine(lines[lineNum++], out key, out value))
                {
                    if (key == "inv_item")
                    {
                        #region Default Values

                        UUID itemID = UUID.Zero;
                        UUID assetID = UUID.Zero;
                        UUID parentID = UUID.Zero;
                        UUID creatorID = UUID.Zero;
                        UUID ownerID = UUID.Zero;
                        UUID lastOwnerID = UUID.Zero;
                        UUID groupID = UUID.Zero;
                        bool groupOwned = false;
                        string name = String.Empty;
                        string desc = String.Empty;
                        AssetType assetType = AssetType.Unknown;
                        InventoryType inventoryType = InventoryType.Unknown;
                        DateTime creationDate = Utils.Epoch;
                        uint flags = 0;
                        Permissions perms = Permissions.NoPermissions;
                        SaleType saleType = SaleType.Not;
                        int salePrice = 0;

                        #endregion Default Values

                        while (lineNum < lines.Length)
                        {
                            if (ParseLine(lines[lineNum++], out key, out value))
                            {
                                #region Line Parsing

                                if (key == "{")
                                {
                                    continue;
                                }
                                else if (key == "}")
                                {
                                    break;
                                }
                                else if (key == "item_id")
                                {
                                    UUID.TryParse(value, out itemID);
                                }
                                else if (key == "parent_id")
                                {
                                    UUID.TryParse(value, out parentID);
                                }
                                else if (key == "permissions")
                                {
                                    #region permissions

                                    while (lineNum < lines.Length)
                                    {
                                        if (ParseLine(lines[lineNum++], out key, out value))
                                        {
                                            if (key == "{")
                                            {
                                                continue;
                                            }
                                            else if (key == "}")
                                            {
                                                break;
                                            }
                                            else if (key == "creator_mask")
                                            {
                                                // Deprecated
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.BaseMask = (PermissionMask)val;
                                            }
                                            else if (key == "base_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.BaseMask = (PermissionMask)val;
                                            }
                                            else if (key == "owner_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.OwnerMask = (PermissionMask)val;
                                            }
                                            else if (key == "group_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.GroupMask = (PermissionMask)val;
                                            }
                                            else if (key == "everyone_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.EveryoneMask = (PermissionMask)val;
                                            }
                                            else if (key == "next_owner_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.NextOwnerMask = (PermissionMask)val;
                                            }
                                            else if (key == "creator_id")
                                            {

                                                UUID.TryParse(value, out creatorID);
                                            }
                                            else if (key == "owner_id")
                                            {
                                                UUID.TryParse(value, out ownerID);
                                            }
                                            else if (key == "last_owner_id")
                                            {
                                                UUID.TryParse(value, out lastOwnerID);
                                            }
                                            else if (key == "group_id")
                                            {
                                                UUID.TryParse(value, out groupID);
                                            }
                                            else if (key == "group_owned")
                                            {
                                                uint val;
                                                if (UInt32.TryParse(value, out val))
                                                    groupOwned = (val != 0);
                                            }
                                        }
                                    }

                                    #endregion permissions
                                }
                                else if (key == "sale_info")
                                {
                                    #region sale_info

                                    while (lineNum < lines.Length)
                                    {
                                        if (ParseLine(lines[lineNum++], out key, out value))
                                        {
                                            if (key == "{")
                                            {
                                                continue;
                                            }
                                            else if (key == "}")
                                            {
                                                break;
                                            }
                                            else if (key == "sale_type")
                                            {
                                                saleType = Utils.StringToSaleType(value);
                                            }
                                            else if (key == "sale_price")
                                            {
                                                Int32.TryParse(value, out salePrice);
                                            }
                                        }
                                    }

                                    #endregion sale_info
                                }
                                else if (key == "shadow_id")
                                {
                                    UUID shadowID;
                                    if (UUID.TryParse(value, out shadowID))
                                        assetID = DecryptShadowID(shadowID);
                                }
                                else if (key == "asset_id")
                                {
                                    UUID.TryParse(value, out assetID);
                                }
                                else if (key == "type")
                                {
                                    assetType = Utils.StringToAssetType(value);
                                }
                                else if (key == "inv_type")
                                {
                                    inventoryType = Utils.StringToInventoryType(value);
                                }
                                else if (key == "flags")
                                {
                                    UInt32.TryParse(value, out flags);
                                }
                                else if (key == "name")
                                {
                                    name = value.Substring(0, value.IndexOf('|'));
                                }
                                else if (key == "desc")
                                {
                                    desc = value.Substring(0, value.IndexOf('|'));
                                }
                                else if (key == "creation_date")
                                {
                                    uint timestamp;
                                    if (UInt32.TryParse(value, out timestamp))
                                        creationDate = Utils.UnixTimeToDateTime(timestamp);
                                    else
                                        Logger.Log("Failed to parse creation_date " + value, Helpers.LogLevel.Warning);
                                }

                                #endregion Line Parsing
                            }
                        }

                        LLInventoryTaskItem item = new LLInventoryTaskItem();
                        item.AssetID = assetID;
                        item.ContentType = LLUtil.LLAssetTypeToContentType((int)assetType);
                        item.CreationDate = creationDate;
                        item.CreatorID = creatorID;
                        item.Description = desc;
                        item.Flags = flags;
                        item.GroupID = groupID;
                        item.GroupOwned = groupOwned;
                        item.ID = itemID;
                        item.Name = name;
                        item.OwnerID = ownerID;
                        item.ParentID = parentID;
                        item.ParentObjectID = parentObjectID;
                        item.PermissionGranter = UUID.Zero; // TODO: We should be serializing this
                        item.Permissions = perms;
                        item.SalePrice = salePrice;
                        item.SaleType = saleType;

                        items.Add(item);
                    }
                    else
                    {
                        //m_log.Warn("Unrecognized token " + key + " in: " + Environment.NewLine + taskData);
                    }
                }
            }

            return items;
        }

        private static bool ParseLine(string line, out string key, out string value)
        {
            // Clean up and convert tabs to spaces
            line = String.Join(" ", line.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries));

            if (line.Length > 2)
            {
                int sep = line.IndexOf(' ');
                if (sep > 0)
                {
                    key = line.Substring(0, sep);
                    value = line.Substring(sep + 1);

                    return true;
                }
            }
            else if (line.Length == 1)
            {
                key = line;
                value = String.Empty;
                return true;
            }

            key = null;
            value = null;
            return false;
        }

        /// <summary>
        /// Reverses a cheesy XORing with a fixed UUID to convert a shadow_id to an asset_id
        /// </summary>
        /// <param name="shadowID">Obfuscated shadow_id value</param>
        /// <returns>Deobfuscated asset_id value</returns>
        private static UUID DecryptShadowID(UUID shadowID)
        {
            return shadowID ^ MAGIC_ID;
        }
    }
}
