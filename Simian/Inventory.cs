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

namespace Simian
{
    /// <summary>
    /// Base class that inventory items and folders inherit from
    /// </summary>
    public abstract class InventoryBase
    {
        /// <summary>UUID of the inventory item</summary>
        public UUID ID;
        /// <summary>UUID of the parent folder</summary>
        public UUID ParentID;
        /// <summary>Item name</summary>
        public string Name = String.Empty;
        /// <summary>Item owner UUID</summary>
        public UUID OwnerID;
        /// <summary>Extra data associated with this item</summary>
        public OSDMap ExtraData = new OSDMap();

        public InventoryBase()
        {
        }

        public InventoryBase(InventoryBase item)
        {
            ID = item.ID;
            ParentID = item.ParentID;
            Name = item.Name;
            OwnerID = item.OwnerID;
            ExtraData = item.ExtraData; // FIXME: Copy by value, not reference
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is InventoryBase))
                return false;

            InventoryBase o = (InventoryBase)obj;
            return o.ID == ID;
        }

        public static bool operator ==(InventoryBase lhs, InventoryBase rhs)
        {
            if ((object)lhs == null)
                return (object)rhs == null;
            else
                return lhs.Equals(rhs);
        }

        public static bool operator !=(InventoryBase lhs, InventoryBase rhs)
        {
            return !(lhs == rhs);
        }
    }

    /// <summary>
    /// Inventory item
    /// </summary>
    public class InventoryItem : InventoryBase
    {
        /// <summary>A Description of this item</summary>
        public string Description = String.Empty;
        /// <summary>UUID of the asset this item points to</summary>
        public UUID AssetID;
        /// <summary>Content type of the asset this item points to</summary>
        public string ContentType = "application/octet-stream";
        /// <summary>The UUID of the creator of this item</summary>
        public UUID CreatorID;
        /// <summary>Time and date this inventory item was created, stored as
        /// UTC (Coordinated Universal Time)</summary>
        public DateTime CreationDate;

        public InventoryItem()
            : base()
        {
        }

        public InventoryItem(InventoryItem item)
            : base(item)
        {
            Description = item.Description;
            AssetID = item.AssetID;
            ContentType = item.ContentType;
            CreatorID = item.CreatorID;
            CreationDate = item.CreationDate;
        }
    }

    /// <summary>
    /// Inventory folder
    /// </summary>
    public class InventoryFolder : InventoryBase
    {
        /// <summary>Preferred content type for this folder</summary>
        public string PreferredContentType;
        /// <summary>Current folder version. This needs to be incremented every
        /// time a child is added or removed</summary>
        public int Version = 1;
        /// <summary>Child items contained in this folder</summary>
        public Dictionary<UUID, InventoryBase> Children = new Dictionary<UUID, InventoryBase>();

        public InventoryFolder()
            : base()
        {
        }

        public InventoryFolder(InventoryFolder folder)
            : base(folder)
        {
            PreferredContentType = folder.PreferredContentType;
            Version = folder.Version;
            Children = new Dictionary<UUID, InventoryBase>(folder.Children);
        }
    }
}
