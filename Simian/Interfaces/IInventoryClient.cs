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

namespace Simian
{
    public class InventorySkeleton
    {
        public UUID RootFolderID;
        public InventoryFolder Skeleton;
        public UUID LibraryFolderID;
        public InventoryFolder LibrarySkeleton;
        public UUID LibraryOwner;
    }

    public interface IInventoryClient
    {
        bool TryCreateItem(UUID presenceID, InventoryItem item);
        bool TryCreateFolder(UUID presenceID, InventoryFolder folder);
        bool TryCreateRootFolder(UUID presenceID, string name, out UUID rootFolderID);
        bool TryRemoveNodes(UUID presenceID, IList<UUID> nodeIDs);
        bool TryPurgeFolder(UUID presenceID, UUID folderID);
        bool TryGetInventory(UUID presenceID, UUID objectID, out InventoryBase obj);
        bool TryGetInventorySkeleton(UUID presenceID, out InventorySkeleton skeleton);
        bool TryGetAssetIDs(UUID presenceID, UUID[] itemIDs, out IDictionary<UUID, UUID> itemsToAssetIDs);
        bool TryGetItemsByAssetID(UUID presenceID, UUID assetID, out IList<InventoryItem> items);
    }
}
