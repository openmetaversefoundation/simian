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
    [Flags]
    public enum InventorySortOrder : uint
    {
        /// <summary></summary>
        ByDate = 1,
        /// <summary></summary>
        FoldersByName = 2,
        /// <summary></summary>
        SystemFoldersToTop = 4
    }

    public class LLInventoryItem : InventoryItem
    {
        /// <summary>The type of item from the <seealso cref="OpenMetaverse.AssetType"/> enum</summary>
        public AssetType AssetType { get { return (AssetType)LLUtil.ContentTypeToLLAssetType(this.ContentType); } }
        /// <summary>The type of item from the <seealso cref="OpenMetaverse.InventoryType"/> enum</summary>
        public InventoryType InventoryType
        {
            get
            {
                if (IsAttachment)
                    return InventoryType.Attachment;
                else
                    return (InventoryType)LLUtil.ContentTypeToLLInvType(this.ContentType);
            }
        }

        /// <summary>True if this is an attachment, otherwise false</summary>
        public bool IsAttachment
        {
            get { return ExtraData["attachment"].AsBoolean(); }
            set { ExtraData["attachment"] = OSD.FromBoolean(value); }
        }

        /// <summary>Content type of the item this item links to, if this item is an inventory link</summary>
        public string LinkedContentType
        {
            get { return ExtraData["linked_content_type"].AsString(); }
            set { ExtraData["linked_content_type"] = OSD.FromString(value); }
        }

        /// <summary>Asset type of the item this item links to, if this item is an inventory link</summary>
        public AssetType LinkedAssetType { get { return (AssetType)LLUtil.ContentTypeToLLAssetType(this.LinkedContentType); } }
        /// <summary>Inventory type of the item this item links to, if this item is an inventory link</summary>
        public InventoryType LinkedInventoryType { get { return (InventoryType)LLUtil.ContentTypeToLLInvType(this.LinkedContentType); } }

        /// <summary>The <seealso cref="OpenMetaverse.Group"/>s UUID this item is set to or owned by</summary>
        public UUID GroupID
        {
            get { return ExtraData["group_id"].AsUUID(); }
            set { ExtraData["group_id"] = OSD.FromUUID(value); }
        }
        /// <summary>If true, item is owned by a group</summary>
        public bool GroupOwned
        {
            get { return ExtraData["group_owned"].AsBoolean(); }
            set { ExtraData["group_owned"] = OSD.FromBoolean(value); }
        }
        /// <summary>The combined <seealso cref="OpenMetaverse.Permissions"/> of this item</summary>
        public Permissions Permissions
        {
            get
            {
                OSD permData = ExtraData["permissions"];
                if (permData is OSDMap)
                    return Permissions.FromOSD(permData);
                else
                    return Permissions.FullPermissions;
            }
            set { ExtraData["permissions"] = value.GetOSD(); }
        }
        /// <summary>The price this item can be purchased for</summary>
        public int SalePrice
        {
            get { return ExtraData["sale_price"].AsInteger(); }
            set { ExtraData["sale_price"] = OSD.FromInteger(value); }
        }
        /// <summary>The type of sale from the <seealso cref="OpenMetaverse.SaleType"/> enum</summary>
        public SaleType SaleType
        {
            get { return (SaleType)ExtraData["sale_type"].AsInteger(); }
            set { ExtraData["sale_type"] = OSD.FromInteger((int)value); }
        }
        /// <summary>Combined flags from <seealso cref="OpenMetaverse.InventoryItemFlags"/></summary>
        public uint Flags
        {
            get { return ExtraData["flags"].AsUInteger(); }
            set { ExtraData["flags"] = OSD.FromInteger((int)value); }
        }

        public LLInventoryItem()
            : base()
        {
        }

        public LLInventoryItem(InventoryItem item)
            : base(item)
        {
        }

        public uint CRC()
        {
            Permissions perms = Permissions;

            return Helpers.InventoryCRC((int)Utils.DateTimeToUnixTime(CreationDate), (byte)SaleType,
                (sbyte)InventoryType, (sbyte)AssetType, AssetID, GroupID, SalePrice, OwnerID,
                CreatorID, ID, ParentID, (uint)perms.EveryoneMask, Flags, (uint)perms.NextOwnerMask,
                (uint)perms.GroupMask, (uint)perms.OwnerMask);
        }
    }

    public class LLInventoryTaskItem : LLInventoryItem
    {
        public UUID ParentObjectID;
        public UUID PermissionGranter;
        public PermissionMask GrantedPermissions;
    }
}
