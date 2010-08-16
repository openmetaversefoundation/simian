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
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Connectors.Remote
{
    [ApplicationModule("SimianGridAssetClient")]
    public class SimianGridAssetClient : IAssetClient, IApplicationModule
    {
        const string METADATA_MIME_TYPE = "application/x-simian-metadata";

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;
        private IHttpServer m_httpServer;
        private IDataStore m_dataStore;
        private string m_serverUrl;

        public bool Start(Simian simian)
        {
            m_simian = simian;
            m_httpServer = simian.GetAppModule<IHttpServer>();
            m_dataStore = m_simian.GetAppModule<IDataStore>();

            IConfigSource source = simian.Config;
            IConfig config = source.Configs["SimianGrid"];
            if (config != null)
                m_serverUrl = config.GetString("AssetService", null);

            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SimianGrid] config section is missing the AssetService URL");
                return false;
            }

            return true;
        }

        public void Stop()
        {
        }

        #region IAssetClient Members

        public bool StoreAsset(string contentType, bool local, bool temporary, byte[] data, UUID creatorID, out UUID assetID)
        {
            assetID = UUID.Random();

            return StoreAsset(new Asset
            {
                ContentType = contentType,
                CreationDate = DateTime.UtcNow,
                CreatorID = creatorID,
                Data = data,
                ID = assetID,
                Local = local,
                SHA256 = Utils.SHA256(data),
                Temporary = temporary
            });
        }

        public bool StoreAsset(Asset asset)
        {
            Debug.Assert(asset.Data != null, "Cannot store an asset without data");
            Debug.Assert(!String.IsNullOrEmpty(asset.ContentType), "Cannot store an asset without a ContentType");

            bool storedInCache = false;

            if (asset.ID == UUID.Zero)
                asset.ID = UUID.Random();

            // Run this asset through the incoming asset filter
            if (!m_simian.FilterAsset(asset))
            {
                m_log.InfoFormat("Asset {0} ({1}, {2} bytes) was rejected", asset.ID, asset.ContentType, asset.Data.Length);
                return false;
            }

            #region Caching

            if (m_dataStore != null)
            {
                byte[] metadata = CreateMetadata(asset.CreatorID, asset.ContentType, asset.Local, asset.Temporary, asset.Data, asset.ExtraHeaders);
                m_dataStore.AddOrUpdateAsset(asset.ID, METADATA_MIME_TYPE, metadata, true);
                m_dataStore.AddOrUpdateAsset(asset.ID, asset.ContentType, asset.Data, true);

                storedInCache = true;
            }

            #endregion Caching

            // If this is a local asset we don't need to store it remotely
            if (asset.Local)
            {
                if (!storedInCache)
                    m_log.Error("Cannot store asset " + asset.ID + " (" + asset.ContentType + ") without an IDataStore");
                return storedInCache;
            }

            #region Remote Storage

            // Distinguish public and private assets
            bool isPublic = true;
            switch (asset.ContentType)
            {
                case "application/vnd.ll.callingcard":
                case "application/vnd.ll.gesture":
                case "application/vnd.ll.lslbyte":
                case "application/vnd.ll.lsltext":
                    isPublic = false;
                    break;
            }

            // Build the remote storage request
            List<MultipartForm.Element> postParameters = new List<MultipartForm.Element>()
            {
                new MultipartForm.Parameter("AssetID", asset.ID.ToString()),
                new MultipartForm.Parameter("CreatorID", asset.CreatorID.ToString()),
                new MultipartForm.Parameter("Temporary", asset.Temporary ? "1" : "0"),
                new MultipartForm.Parameter("Public", isPublic ? "1" : "0"),
                new MultipartForm.File("Asset", asset.ID.ToString(), asset.ContentType, asset.Data)
            };

            // Make the remote storage request
            string errorMessage = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(m_serverUrl);

                HttpWebResponse response = MultipartForm.Post(request, postParameters);
                using (Stream responseStream = response.GetResponseStream())
                {
                    string responseStr = null;

                    try
                    {
                        responseStr = responseStream.GetStreamString();
                        OSD responseOSD = OSDParser.Deserialize(responseStr);
                        if (responseOSD.Type == OSDType.Map)
                        {
                            OSDMap responseMap = (OSDMap)responseOSD;
                            if (responseMap["Success"].AsBoolean())
                                return true;
                            else
                                errorMessage = "Upload failed: " + responseMap["Message"].AsString();
                        }
                        else
                        {
                            errorMessage = "Response format was invalid:\n" + responseStr;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!String.IsNullOrEmpty(responseStr))
                            errorMessage = "Failed to parse the response:\n" + responseStr;
                        else
                            errorMessage = "Failed to retrieve the response: " + ex.Message;
                    }
                }
            }
            catch (WebException ex)
            {
                errorMessage = ex.Message;
            }

            #endregion Remote Storage

            m_log.WarnFormat("Failed to remotely store asset {0} ({1}): {2}", asset.ID, asset.ContentType, errorMessage);
            return false;
        }

        public bool RemoveAsset(UUID assetID, string contentType)
        {
            Uri url = new Uri(m_serverUrl + assetID);

            // Expire from the local cache
            if (m_dataStore != null)
                m_dataStore.RemoveAsset(assetID, contentType);

            // Delete from SimianGrid
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "DELETE";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.NoContent)
                    {
                        m_log.Warn("Unexpected response when deleting asset " + url + ": " +
                            response.StatusCode + " (" + response.StatusDescription + ")");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                m_log.Warn("Failed to delete asset " + assetID + " from the asset service: " + ex.Message);
                return false;
            }
        }

        public bool TryGetAsset(UUID assetID, string contentType, out Asset asset)
        {
            if (TryLocalFetch(assetID, contentType, out asset))
                return true;

            if (TryRemoteFetch(assetID, out asset))
                return true;

            m_log.Debug("Failed to fetch asset " + assetID + " (" + contentType + ")");
            return false;
        }

        public bool TryGetAssetMetadata(UUID assetID, string contentType, out Asset asset)
        {
            asset = null;

            // Try a local metadata fetch
            if (m_dataStore != null)
            {
                byte[] metadata;
                if (m_dataStore.TryGetAsset(assetID, METADATA_MIME_TYPE, out metadata))
                {
                    asset = CreateAsset(assetID, contentType, metadata, null);
                    return true;
                }
            }

            // Try a remote metadata fetch
            Uri url = new Uri(m_serverUrl + assetID);

            try
            {
                HttpWebRequest request = UntrustedHttpWebRequest.Create(url);
                request.Method = "HEAD";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        UUID creatorID;
                        UUID.TryParse(response.Headers.GetOne("X-Asset-Creator-Id"), out creatorID);

                        byte[] sha256 = Utils.HexStringToBytes(response.Headers.GetOne("ETag"), true);

                        // TODO: Only put unrecognized headers in ExtraHeaders
                        Dictionary<string, string> extraHeaders = new Dictionary<string, string>(response.Headers.Count);
                        foreach (string key in response.Headers.AllKeys)
                            extraHeaders[key] = response.Headers.GetOne(key);

                        // Create the metadata object
                        asset = new Asset();
                        asset.ContentType = response.ContentType;
                        asset.CreationDate = response.LastModified;
                        asset.CreatorID = creatorID;
                        asset.ExtraHeaders = extraHeaders;
                        asset.ID = assetID;
                        asset.Local = false;
                        asset.SHA256 = sha256;
                        asset.Temporary = false;
                    }
                }

                // Cache store
                if (asset != null && m_dataStore != null)
                {
                    byte[] metadata = CreateMetadata(asset.CreatorID, asset.ContentType, asset.Local, asset.Temporary, asset.Data, asset.ExtraHeaders);
                    m_dataStore.AddOrUpdateAsset(asset.ID, METADATA_MIME_TYPE, metadata, true);
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("Asset HEAD from " + url + " failed: " + ex.Message);
            }

            return (asset != null);
        }

        public bool TryGetCachedAsset(UUID assetID, string contentType, out Asset asset)
        {
            return TryLocalFetch(assetID, contentType, out asset);
        }

        #endregion IAssetClient Members

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
                metadata["sha256"] = OSD.FromBinary(Utils.SHA256(data));

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
                    asset.SHA256 = map["sha256"].AsBinary();

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

        private bool TryLocalFetch(UUID assetID, string contentType, out Asset asset)
        {
            if (m_dataStore == null)
            {
                asset = null;
                return false;
            }

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
                    m_log.Info("Metadata missing for local asset " + assetID + " (" + contentType + "), removing local asset");
                    RemoveAsset(assetID, contentType);
                }
            }

            asset = null;
            return false;
        }

        private bool TryRemoteFetch(UUID id, out Asset asset)
        {
            asset = null;
            Uri url = new Uri(m_serverUrl + id);

            try
            {
                HttpWebRequest request = UntrustedHttpWebRequest.Create(url);

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        UUID creatorID;
                        UUID.TryParse(response.Headers.GetOne("X-Asset-Creator-Id"), out creatorID);

                        byte[] sha256 = Utils.HexStringToBytes(response.Headers.GetOne("ETag"), true);

                        // TODO: Only put unrecognized headers in ExtraHeaders
                        Dictionary<string, string> extraHeaders = new Dictionary<string, string>(response.Headers.Count);
                        foreach (string key in response.Headers.AllKeys)
                            extraHeaders[key] = response.Headers.GetOne(key);

                        // Create the asset object
                        asset = new Asset();
                        asset.ContentType = response.ContentType;
                        asset.CreationDate = response.LastModified;
                        asset.CreatorID = creatorID;
                        asset.ExtraHeaders = extraHeaders;
                        asset.ID = id;
                        asset.Local = false;
                        asset.SHA256 = sha256;
                        asset.Temporary = false;

                        // Grab the asset data from the response stream
                        using (MemoryStream stream = new MemoryStream())
                        {
                            responseStream.CopyTo(stream, Int32.MaxValue);
                            asset.Data = stream.ToArray();
                        }
                    }
                }

                // Cache store
                if (asset != null && m_dataStore != null)
                {
                    byte[] metadata = CreateMetadata(asset.CreatorID, asset.ContentType, asset.Local, asset.Temporary, asset.Data, asset.ExtraHeaders);
                    m_dataStore.AddOrUpdateAsset(asset.ID, METADATA_MIME_TYPE, metadata, true);
                    m_dataStore.AddOrUpdateAsset(asset.ID, asset.ContentType, asset.Data, true);
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("Asset GET from " + url + " failed: " + ex.Message);
            }

            return (asset != null);
        }
    }
}
