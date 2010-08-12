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
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;

namespace Simian.Connectors.Standalone
{
    [ApplicationModule("StandaloneAssetClient")]
    public class StandaloneAssetClient : IAssetClient, IApplicationModule
    {
        const string DEFAULT_ASSETS_PATH = "DefaultAssets";
        const string METADATA_MIME_TYPE = "application/x-simian-metadata";
        private static readonly TimeSpan CACHE_TIMEOUT = TimeSpan.FromDays(3.0d);

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private IScheduler m_scheduler;
        private IDataStore m_dataStore;

        public bool Start(Simian simian)
        {
            m_simian = simian;

            m_scheduler = m_simian.GetAppModule<IScheduler>();
            if (m_scheduler == null)
            {
                m_log.Error("StandaloneAssetClient requires an IScheduler service");
                return false;
            }

            m_dataStore = m_simian.GetAppModule<IDataStore>();
            if (m_dataStore == null)
            {
                m_log.Error("StandaloneAssetClient requires an IDataStore service");
                return false;
            }

            LoadDefaultAssets();

            return true;
        }

        public void Stop()
        {
        }
        
        public bool StoreAsset(string contentType, bool local, bool temporary, byte[] data, UUID creatorID, out UUID assetID)
        {
            assetID = UUID.Random();

            Asset asset = new Asset
            {
                ContentType = contentType,
                CreationDate = DateTime.UtcNow,
                CreatorID = creatorID,
                Data = data,
                ID = assetID,
                Local = local,
                Temporary = temporary,
            };

            return StoreAsset(asset);
        }

        public bool StoreAsset(Asset asset)
        {
            Debug.Assert(asset.Data != null, "Cannot store an asset without data");
            Debug.Assert(!String.IsNullOrEmpty(asset.ContentType), "Cannot store an asset without a ContentType");

            if (asset.ID == UUID.Zero)
                asset.ID = UUID.Random();

            byte[] metadata = CreateMetadata(asset.CreatorID, asset.ContentType, asset.Local, asset.Temporary, asset.Data, asset.ExtraHeaders);

            if (asset.Temporary)
            {
                m_dataStore.AddOrUpdateAsset(asset.ID, METADATA_MIME_TYPE, metadata, CACHE_TIMEOUT);
                return m_dataStore.AddOrUpdateAsset(asset.ID, asset.ContentType, asset.Data, CACHE_TIMEOUT);
            }
            else
            {
                m_dataStore.AddOrUpdateAsset(asset.ID, METADATA_MIME_TYPE, metadata);
                return m_dataStore.AddOrUpdateAsset(asset.ID, asset.ContentType, asset.Data);
            }
        }

        public bool RemoveAsset(UUID assetID, string contentType)
        {
            m_dataStore.RemoveAsset(assetID, METADATA_MIME_TYPE);
            return m_dataStore.RemoveAsset(assetID, contentType);
        }

        public bool TryGetAsset(UUID assetID, string contentType, out Asset asset)
        {
            return TryLocalFetch(assetID, contentType, out asset);
        }

        public bool TryGetCachedAsset(UUID assetID, string contentType, out Asset asset)
        {
            return TryLocalFetch(assetID, contentType, out asset);
        }

        private void BeginGetAssetWorker(object o)
        {
            object[] args = (object[])o;

            UUID assetID = (UUID)args[0];
            string contentType = (string)args[1];
            EventHandler<AssetArgs> callback = (EventHandler<AssetArgs>)args[2];

            Asset asset;
            bool success = TryGetAsset(assetID, contentType, out asset);

            callback(this, new AssetArgs { Asset = asset, AssetID = assetID, Success = success });
        }

        private bool TryLocalFetch(UUID assetID, string contentType, out Asset asset)
        {
            byte[] data;

            if (m_dataStore.TryGetAsset(assetID, contentType, out data))
            {
                // Fetched the asset. Now try to fetch the metadata
                byte[] metadata;
                if (m_dataStore.TryGetAsset(assetID, METADATA_MIME_TYPE, out metadata))
                {
                    asset = CreateAsset(assetID, contentType, metadata, data);
                    return true;
                }
                else
                {
                    m_log.Info("Metadata expired for unexpired asset " + assetID + " (" + contentType + "), expiring asset");
                    RemoveAsset(assetID, contentType);
                }
            }

            asset = null;
            return false;
        }

        private byte[] CreateMetadata(UUID creatorID, string contentType, bool local, bool temporary, byte[] data, Dictionary<string, string> extraHeaders)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                OSDMap metadata = new OSDMap();
                metadata["local"] = OSD.FromBoolean(local);
                metadata["temporary"] = OSD.FromBoolean(temporary);
                metadata["content_type"] = OSD.FromString(contentType);
                metadata["creator_id"] = OSD.FromUUID(creatorID);
                metadata["creation_date"] = OSD.FromDate(DateTime.UtcNow);
                metadata["sha1"] = OSD.FromBinary(Utils.SHA1(data));

                if (extraHeaders != null && extraHeaders.Count > 0)
                {
                    OSDMap headerMap = new OSDMap(extraHeaders.Count);
                    foreach (KeyValuePair<string, string> kvp in extraHeaders)
                        headerMap.Add(kvp.Key, OSD.FromString(kvp.Value));
                    metadata["extra_headers"] = headerMap;
                }

                return Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(metadata));
            }
        }

        private Asset CreateAsset(UUID assetID, string contentType, byte[] metadata, byte[] data)
        {
            Asset asset = new Asset { ID = assetID, ContentType = contentType, Data = data };

            try
            {
                using (MemoryStream stream = new MemoryStream(metadata))
                {
                    OSDMap map = OSDParser.DeserializeJson(stream) as OSDMap;

                    asset.Local = map["local"].AsBoolean();
                    asset.Temporary = map["temporary"].AsBoolean();
                    asset.CreationDate = map["creation_date"].AsDate();
                    asset.CreatorID = map["creator_id"].AsUUID();
                    asset.SHA1 = map["sha1"].AsBinary();

                    if (map.ContainsKey("extra_headers"))
                    {
                        OSDMap headerMap = map["extra_headers"] as OSDMap;

                        asset.ExtraHeaders = new Dictionary<string, string>(headerMap.Count);
                        foreach (KeyValuePair<string, OSD> kvp in headerMap)
                            asset.ExtraHeaders.Add(kvp.Key, kvp.Value.AsString());
                    }

                    return asset;
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Failed to decode metadata for " + assetID + " (" + contentType + "): " + ex.Message);
                return null;
            }
        }

        private void LoadDefaultAssets()
        {
            string executingDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string defaultAssetPath = Path.Combine(executingDir, DEFAULT_ASSETS_PATH);

            try
            {
                if (Directory.Exists(defaultAssetPath))
                {
                    string[] assets = Directory.GetFiles(defaultAssetPath);

                    for (int i = 0; i < assets.Length; i++)
                    {
                        string filename = assets[i];
                        byte[] data = File.ReadAllBytes(filename);

                        UUID assetID = ParseUUIDFromFilename(filename);
                        string contentType = m_simian.ExtensionToContentType(Path.GetExtension(filename).TrimStart('.'));
                        byte[] sha1 = Utils.SHA1(data);

                        Asset asset = new Asset
                        {
                            ID = assetID,
                            ContentType = contentType,
                            CreationDate = DateTime.UtcNow,
                            CreatorID = UUID.Zero,
                            Local = false,
                            Temporary = false,
                            SHA1 = sha1,
                            Data = data
                        };

                        StoreAsset(asset);
                    }

                    m_log.Info("Loaded " + assets.Length + " default assets into the local asset store");
                }
            }
            catch (Exception ex)
            {
                m_log.Error("Error loading default asset set: " + ex.Message);
            }
        }

        private static UUID ParseUUIDFromFilename(string filename)
        {
            int dot = filename.LastIndexOf('.');

            if (dot > 35)
            {
                // Grab the last 36 characters of the filename
                string uuidString = filename.Substring(dot - 36, 36);
                UUID uuid;
                UUID.TryParse(uuidString, out uuid);
                return uuid;
            }
            else
            {
                UUID uuid;
                if (UUID.TryParse(Path.GetFileName(filename), out uuid))
                    return uuid;
                else
                    return UUID.Zero;
            }
        }
    }
}
