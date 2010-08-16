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
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private IUserClient m_userClient;
        private string m_serverUrl;

        public bool Start(Simian simian)
        {
            m_simian = simian;
            m_userClient = m_simian.GetAppModule<IUserClient>();

            IConfigSource source = simian.Config;
            IConfig config = source.Configs["SimianGrid"];
            if (config != null)
                m_serverUrl = config.GetString("InventoryService", null);

            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SimianGrid] config section is missing the InventoryService URL");
                return false;
            }

            return true;
        }

        public void Stop()
        {
        }

        #region IInventoryClient

        public bool TryCreateItem(UUID presenceID, InventoryItem item)
        {
            return false;
        }

        public bool TryCreateFolder(UUID presenceID, InventoryFolder folder)
        {
            return false;
        }

        public bool TryCreateRootFolder(UUID presenceID, string name, out UUID rootFolderID)
        {
            rootFolderID = UUID.Zero;
            return false;
        }

        public bool TryRemoveNodes(UUID presenceID, IList<UUID> nodeIDs)
        {
            return false;
        }

        public bool TryPurgeFolder(UUID presenceID, UUID folderID)
        {
            return false;
        }

        public bool TryGetInventory(UUID presenceID, UUID objectID, out InventoryBase obj)
        {
            obj = null;
            return false;
        }

        public bool TryGetInventorySkeleton(UUID presenceID, out InventorySkeleton skeleton)
        {
            skeleton = null;
            return false;
        }

        public bool TryGetAssetIDs(UUID presenceID, UUID[] itemIDs, out IDictionary<UUID, UUID> itemsToAssetIDs)
        {
            itemsToAssetIDs = null;
            return false;
        }

        public bool TryGetItemsByAssetID(UUID presenceID, UUID assetID, out IList<InventoryItem> items)
        {
            items = null;
            return false;
        }

        #endregion IInventoryClient
    }
}
