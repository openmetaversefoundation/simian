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
using System.Text;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("Assets")]
    public class Assets : ISceneModule
    {
        const uint LAST_PACKET_MARKER = 0x80000000u;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;
        private IAssetClient m_assets;
        private LLPermissions m_permissions;
        private Dictionary<ulong, XferDownload> currentDownloads = new Dictionary<ulong, XferDownload>();
        private Dictionary<ulong, Asset> currentUploads = new Dictionary<ulong, Asset>();

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_assets = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assets == null)
            {
                m_log.Error("Can't initialize asset transfers without an IAssetClient");
                return;
            }

            m_permissions = m_scene.GetSceneModule<LLPermissions>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.RequestXfer, RequestXferHandler);
                m_udp.AddPacketHandler(PacketType.ConfirmXferPacket, ConfirmXferPacketHandler);
                m_udp.AddPacketHandler(PacketType.AssetUploadRequest, AssetUploadRequestHandler);
                m_udp.AddPacketHandler(PacketType.SendXferPacket, SendXferPacketHandler);
                m_udp.AddPacketHandler(PacketType.AbortXfer, AbortXferHandler);
                m_udp.AddPacketHandler(PacketType.TransferRequest, TransferRequestHandler);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.RequestXfer, RequestXferHandler);
                m_udp.RemovePacketHandler(PacketType.ConfirmXferPacket, ConfirmXferPacketHandler);
                m_udp.RemovePacketHandler(PacketType.AssetUploadRequest, AssetUploadRequestHandler);
                m_udp.RemovePacketHandler(PacketType.SendXferPacket, SendXferPacketHandler);
                m_udp.RemovePacketHandler(PacketType.AbortXfer, AbortXferHandler);
                m_udp.RemovePacketHandler(PacketType.TransferRequest, TransferRequestHandler);
            }
        }

        #region Xfer System

        private void RequestXferHandler(Packet packet, LLAgent agent)
        {
            RequestXferPacket request = (RequestXferPacket)packet;

            string filename = Utils.BytesToString(request.XferID.Filename);

            UUID taskInventoryID;
            if (filename.StartsWith("inventory_") && filename.EndsWith(".tmp") && UUID.TryParse(filename.Substring(10, 36), out taskInventoryID))
            {
                // This is a request for a task inventory listing, which we generate on demand
                ISceneEntity entity;
                if (m_scene.TryGetEntity(taskInventoryID, out entity) && entity is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)entity;
                    byte[] assetData = Encoding.UTF8.GetBytes(prim.Inventory.GetTaskInventoryAsset());

                    SendXferPacketPacket xfer = new SendXferPacketPacket();
                    xfer.XferID.ID = request.XferID.ID;

                    if (assetData.Length < 1000)
                    {
                        xfer.XferID.Packet = 0 | LAST_PACKET_MARKER;
                        xfer.DataPacket.Data = new byte[assetData.Length + 4];
                        Utils.IntToBytes(assetData.Length, xfer.DataPacket.Data, 0);
                        Buffer.BlockCopy(assetData, 0, xfer.DataPacket.Data, 4, assetData.Length);

                        m_udp.SendPacket(agent, xfer, ThrottleCategory.Asset, false);
                        m_log.Debug("Completed single packet xfer download of " + filename);
                    }
                    else
                    {
                        xfer.XferID.Packet = 0;
                        xfer.DataPacket.Data = new byte[1000 + 4];
                        Utils.IntToBytes(assetData.Length, xfer.DataPacket.Data, 0);
                        Buffer.BlockCopy(assetData, 0, xfer.DataPacket.Data, 4, 1000);

                        // We don't need the entire XferDownload class, just the asset data and the current packet number
                        XferDownload download = new XferDownload();
                        download.AssetData = assetData;
                        download.PacketNum = 1;
                        download.Filename = filename;
                        lock (currentDownloads)
                            currentDownloads[request.XferID.ID] = download;

                        m_udp.SendPacket(agent, xfer, ThrottleCategory.Asset, false);
                    }
                }
                else
                {
                    m_log.Warn("Could not find primitive " + taskInventoryID);
                }
            }
            else
            {
                m_log.Warn("Got a RequestXfer for an unknown file: " + filename);
            }
        }

        private void ConfirmXferPacketHandler(Packet packet, LLAgent agent)
        {
            ConfirmXferPacketPacket confirm = (ConfirmXferPacketPacket)packet;

            XferDownload download;
            if (currentDownloads.TryGetValue(confirm.XferID.ID, out download))
            {
                // Send the next packet
                SendXferPacketPacket xfer = new SendXferPacketPacket();
                xfer.XferID.ID = confirm.XferID.ID;

                int bytesRemaining = (int)(download.AssetData.Length - (download.PacketNum * 1000));

                if (bytesRemaining > 1000)
                {
                    xfer.DataPacket.Data = new byte[1000];
                    Buffer.BlockCopy(download.AssetData, (int)download.PacketNum * 1000, xfer.DataPacket.Data, 0, 1000);
                    xfer.XferID.Packet = download.PacketNum++;
                }
                else
                {
                    // Last packet
                    xfer.DataPacket.Data = new byte[bytesRemaining];
                    Buffer.BlockCopy(download.AssetData, (int)download.PacketNum * 1000, xfer.DataPacket.Data, 0, bytesRemaining);
                    xfer.XferID.Packet = download.PacketNum | LAST_PACKET_MARKER;

                    lock (currentDownloads)
                        currentDownloads.Remove(confirm.XferID.ID);
                    m_log.Debug("Completing xfer download for: " + download.Filename);
                }

                m_udp.SendPacket(agent, xfer, ThrottleCategory.Asset, false);
            }
        }

        private void AssetUploadRequestHandler(Packet packet, LLAgent agent)
        {
            AssetUploadRequestPacket request = (AssetUploadRequestPacket)packet;
            UUID assetID = UUID.Combine(request.AssetBlock.TransactionID, agent.SecureSessionID);
            AssetType type = (AssetType)request.AssetBlock.Type;

            // Check if the agent is allowed to upload an asset
            string uploadError;
            if (m_permissions != null && !m_permissions.TryUploadOneAsset(agent, out uploadError))
            {
                m_scene.PresenceAlert(this, agent, uploadError);
                return;
            }

            bool local = request.AssetBlock.StoreLocal | type == AssetType.LSLBytecode;
            bool temp = request.AssetBlock.Tempfile;

            // Check if the asset is small enough to fit in a single packet
            if (request.AssetBlock.AssetData.Length != 0)
            {
                // Create a new asset from the completed upload
                
                Asset asset = CreateAsset(type, assetID, DateTime.UtcNow, agent.ID, local, temp, request.AssetBlock.AssetData);

                if (asset == null)
                {
                    m_log.Warn("Failed to create asset from uploaded data");
                    return;
                }

                if (type != AssetType.LSLBytecode)
                {
                    // Store the asset
                    m_log.DebugFormat("Storing uploaded asset {0} ({1})", assetID, asset.ContentType);
                    if (!m_assets.StoreAsset(asset))
                        m_log.ErrorFormat("Failed to store uploaded asset {0} ({1})", assetID, asset.ContentType);
                }
                else
                {
                    m_log.Debug("Ignoring LSL bytecode upload " + assetID);
                }

                // Send a success response
                AssetUploadCompletePacket complete = new AssetUploadCompletePacket();
                complete.AssetBlock.Success = true;
                complete.AssetBlock.Type = request.AssetBlock.Type;
                complete.AssetBlock.UUID = assetID;
                m_udp.SendPacket(agent, complete, ThrottleCategory.Task, false);
            }
            else
            {
                // Create a new (empty) asset for the upload
                Asset asset = CreateAsset(type, assetID, DateTime.UtcNow, agent.ID, local, temp, null);

                if (asset == null)
                {
                    m_log.Warn("Failed to create asset from uploaded data");
                    return;
                }

                asset.Temporary = (request.AssetBlock.Tempfile | request.AssetBlock.StoreLocal);
                ulong transferID = request.AssetBlock.TransactionID.GetULong();
                
                // Prevent LSL bytecode transfers from colliding with LSL source transfers, which 
                // use a colliding UUID
                if (type == AssetType.LSLBytecode)
                    ++transferID;

                RequestXferPacket xfer = new RequestXferPacket();
                xfer.XferID.DeleteOnCompletion = request.AssetBlock.Tempfile;
                xfer.XferID.FilePath = 0;
                xfer.XferID.Filename = Utils.EmptyBytes;
                xfer.XferID.ID = transferID;
                xfer.XferID.UseBigPackets = false;
                xfer.XferID.VFileID = asset.ID;
                xfer.XferID.VFileType = request.AssetBlock.Type;

                m_log.DebugFormat("Starting upload for {0} / {1} ({2})", assetID, transferID, asset.ContentType);

                // Add this asset to the current upload list
                lock (currentUploads)
                    currentUploads[transferID] = asset;

                m_udp.SendPacket(agent, xfer, ThrottleCategory.Task, false);
            }
        }

        private void SendXferPacketHandler(Packet packet, LLAgent agent)
        {
            SendXferPacketPacket xfer = (SendXferPacketPacket)packet;

            uint packetID = xfer.XferID.Packet & ~LAST_PACKET_MARKER;
            bool lastPacket = (xfer.XferID.Packet & LAST_PACKET_MARKER) != 0;

            Asset asset;
            if (currentUploads.TryGetValue(xfer.XferID.ID, out asset))
            {
                if (packetID == 0)
                {
                    uint size = Utils.BytesToUInt(xfer.DataPacket.Data);
                    asset.Data = new byte[size];

                    Buffer.BlockCopy(xfer.DataPacket.Data, 4, asset.Data, 0, xfer.DataPacket.Data.Length - 4);

                    // Confirm the first upload packet
                    ConfirmXferPacketPacket confirm = new ConfirmXferPacketPacket();
                    confirm.XferID.ID = xfer.XferID.ID;
                    confirm.XferID.Packet = xfer.XferID.Packet;
                    m_udp.SendPacket(agent, confirm, ThrottleCategory.Asset, false);
                }
                else if (asset.Data != null)
                {
                    AssetType type = (AssetType)LLUtil.ContentTypeToLLAssetType(asset.ContentType);

                    Buffer.BlockCopy(xfer.DataPacket.Data, 0, asset.Data, (int)packetID * 1000, xfer.DataPacket.Data.Length);

                    // Confirm this upload packet
                    ConfirmXferPacketPacket confirm = new ConfirmXferPacketPacket();
                    confirm.XferID.ID = xfer.XferID.ID;
                    confirm.XferID.Packet = xfer.XferID.Packet;
                    m_udp.SendPacket(agent, confirm, ThrottleCategory.Asset, false);

                    if (lastPacket)
                    {
                        // Asset upload finished
                        lock (currentUploads)
                            currentUploads.Remove(xfer.XferID.ID);

                        if (type != AssetType.LSLBytecode)
                        {
                            // Store the uploaded asset
                            m_log.DebugFormat("Storing uploaded asset {0} ({1})", asset.ID, asset.ContentType);
                            if (!m_assets.StoreAsset(asset))
                                m_log.ErrorFormat("Failed to store uploaded asset {0} ({1})", asset.ID, asset.ContentType);
                        }
                        else
                        {
                            m_log.Debug("Ignoring LSL bytecode upload " + asset.ID);
                        }

                        AssetUploadCompletePacket complete = new AssetUploadCompletePacket();
                        complete.AssetBlock.Success = true;
                        complete.AssetBlock.Type = (sbyte)type;
                        complete.AssetBlock.UUID = asset.ID;
                        m_udp.SendPacket(agent, complete, ThrottleCategory.Asset, false);
                    }
                }
                else
                {
                    m_log.Error("Received SendXferPacket #" + xfer.XferID.Packet + " when asset.AssetData is still null");
                }
            }
            else
            {
                m_log.Debug("Received a SendXferPacket for an unknown upload");
            }
        }

        private void AbortXferHandler(Packet packet, LLAgent agent)
        {
            AbortXferPacket abort = (AbortXferPacket)packet;

            lock (currentUploads)
            {
                if (currentUploads.ContainsKey(abort.XferID.ID))
                {
                    m_log.Debug(String.Format("Aborting Xfer {0}, result: {1}", abort.XferID.ID,
                        (TransferError)abort.XferID.Result));

                    currentUploads.Remove(abort.XferID.ID);
                }
                else
                {
                    m_log.Debug(String.Format("Received an AbortXfer for an unknown xfer {0}",
                        abort.XferID.ID));
                }
            }
        }

        #endregion Xfer System

        #region Transfer System

        private void TransferDownload(LLAgent agent, UUID transferID, UUID assetID, AssetType type, Asset asset)
        {
            const int MAX_CHUNK_SIZE = 1000;

            string contentType = LLUtil.LLAssetTypeToContentType((int)type);

            if (contentType == asset.ContentType)
            {
                m_log.Debug(String.Format("Transferring asset {0} ({1})", asset.ID, asset.ContentType));

                TransferInfoPacket response = new TransferInfoPacket();
                response.TransferInfo = new TransferInfoPacket.TransferInfoBlock();
                response.TransferInfo.TransferID = transferID;

                // Set the response channel type
                response.TransferInfo.ChannelType = (int)ChannelType.Asset;

                // Params
                response.TransferInfo.Params = new byte[20];
                assetID.ToBytes(response.TransferInfo.Params, 0);
                Utils.IntToBytes((int)type, response.TransferInfo.Params, 16);

                response.TransferInfo.Size = asset.Data.Length;
                response.TransferInfo.Status = (int)StatusCode.OK;
                response.TransferInfo.TargetType = (int)TargetType.Unknown; // Doesn't seem to be used by the client

                m_udp.SendPacket(agent, response, ThrottleCategory.Asset, false);

                // Transfer system does not wait for ACKs, just sends all of the
                // packets for this transfer out
                int processedLength = 0;
                int packetNum = 0;
                while (processedLength < asset.Data.Length)
                {
                    TransferPacketPacket transfer = new TransferPacketPacket();
                    transfer.TransferData.ChannelType = (int)ChannelType.Asset;
                    transfer.TransferData.TransferID = transferID;
                    transfer.TransferData.Packet = packetNum++;

                    int chunkSize = Math.Min(asset.Data.Length - processedLength, MAX_CHUNK_SIZE);
                    transfer.TransferData.Data = new byte[chunkSize];
                    Buffer.BlockCopy(asset.Data, processedLength, transfer.TransferData.Data, 0, chunkSize);
                    processedLength += chunkSize;

                    if (processedLength >= asset.Data.Length)
                        transfer.TransferData.Status = (int)StatusCode.Done;
                    else
                        transfer.TransferData.Status = (int)StatusCode.OK;

                    m_udp.SendPacket(agent, transfer, ThrottleCategory.Asset, false);
                }
            }
            else
            {
                m_log.WarnFormat("Request for asset {0} with type {1} does not match actual asset type {2}",
                    assetID, type, asset.ContentType);

                TransferNotFound(agent, transferID, assetID, type);
            }
        }

        private void TransferNotFound(LLAgent agent, UUID transferID, UUID assetID, AssetType type)
        {
            m_log.Info("TransferNotFound for asset " + assetID + " with type " + type);

            TransferInfoPacket response = new TransferInfoPacket();
            response.TransferInfo = new TransferInfoPacket.TransferInfoBlock();
            response.TransferInfo.TransferID = transferID;

            // Set the response channel type
            response.TransferInfo.ChannelType = (int)ChannelType.Asset;

            // Params
            response.TransferInfo.Params = new byte[20];
            assetID.ToBytes(response.TransferInfo.Params, 0);
            Utils.IntToBytes((int)type, response.TransferInfo.Params, 16);

            response.TransferInfo.Size = 0;
            response.TransferInfo.Status = (int)StatusCode.UnknownSource;
            response.TransferInfo.TargetType = (int)TargetType.Unknown;

            m_udp.SendPacket(agent, response, ThrottleCategory.Asset, false);
        }

        private void TransferRequestHandler(Packet packet, LLAgent agent)
        {
            TransferRequestPacket request = (TransferRequestPacket)packet;

            ChannelType channel = (ChannelType)request.TransferInfo.ChannelType;
            SourceType source = (SourceType)request.TransferInfo.SourceType;

            if (channel == ChannelType.Asset)
            {
                if (source == SourceType.Asset)
                {
                    // Parse the request
                    UUID assetID = new UUID(request.TransferInfo.Params, 0);
                    AssetType type = (AssetType)(sbyte)Utils.BytesToInt(request.TransferInfo.Params, 16);
                    string contentType = LLUtil.LLAssetTypeToContentType((int)type);

                    // Permission check
                    if (!CanDownloadInventory(agent, type, assetID))
                    {
                        TransferNotFound(agent, request.TransferInfo.TransferID, assetID, type);
                        return;
                    }

                    // Check if we have this asset
                    Asset asset;
                    if (m_assets.TryGetAsset(assetID, contentType, out asset))
                        TransferDownload(agent, request.TransferInfo.TransferID, assetID, type, asset);
                    else
                        TransferNotFound(agent, request.TransferInfo.TransferID, assetID, type);
                }
                else if (source == SourceType.SimInventoryItem)
                {
                    //UUID agentID = new UUID(request.TransferInfo.Params, 0);
                    //UUID sessionID = new UUID(request.TransferInfo.Params, 16);
                    //UUID ownerID = new UUID(request.TransferInfo.Params, 32);
                    UUID taskID = new UUID(request.TransferInfo.Params, 48);
                    UUID itemID = new UUID(request.TransferInfo.Params, 64);
                    UUID assetID = new UUID(request.TransferInfo.Params, 80);
                    AssetType type = (AssetType)(sbyte)Utils.BytesToInt(request.TransferInfo.Params, 96);
                    string contentType = LLUtil.LLAssetTypeToContentType((int)type);

                    if (taskID != UUID.Zero)
                    {
                        // Task (prim) inventory request permission check
                        if (!CanDownloadTaskInventory(agent, type, taskID, itemID))
                        {
                            TransferNotFound(agent, request.TransferInfo.TransferID, assetID, type);
                            return;
                        }
                    }
                    else
                    {
                        // Agent inventory request permission check
                        if (!CanDownloadInventory(agent, type, assetID))
                        {
                            TransferNotFound(agent, request.TransferInfo.TransferID, assetID, type);
                            return;
                        }
                    }

                    // Check if we have this asset
                    Asset asset;
                    if (m_assets.TryGetAsset(assetID, contentType, out asset))
                        TransferDownload(agent, request.TransferInfo.TransferID, assetID, type, asset);
                    else
                        TransferNotFound(agent, request.TransferInfo.TransferID, assetID, type);
                }
                else if (source == SourceType.SimEstate)
                {
                    //UUID agentID = new UUID(request.TransferInfo.Params, 0);
                    //UUID sessionID = new UUID(request.TransferInfo.Params, 16);
                    //EstateAssetType type = (EstateAssetType)Utils.BytesToInt(request.TransferInfo.Params, 32);

                    m_log.Warn("Don't know what to do with an estate asset transfer request");
                }
                else
                {
                    m_log.WarnFormat(
                        "Received a TransferRequest that we don't know how to handle. Channel: {0}, Source: {1}",
                        channel, source);
                }
            }
            else
            {
                m_log.WarnFormat(
                    "Received a TransferRequest that we don't know how to handle. Channel: {0}, Source: {1}",
                    channel, source);
            }
        }

        #endregion Transfer System

        private Asset CreateAsset(AssetType type, UUID assetID, DateTime creationDate, UUID creatorID, bool local, bool temporary, byte[] data)
        {
            Asset asset = new Asset();
            asset.ID = assetID;
            asset.ContentType = LLUtil.LLAssetTypeToContentType((int)type);
            asset.CreationDate = creationDate;
            asset.CreatorID = creatorID;
            asset.Data = data;
            asset.Local = local;
            asset.Temporary = temporary;
            //asset.SHA1 is filled in later

            return asset;
        }

        private bool CanDownloadInventory(LLAgent agent, AssetType type, UUID assetID)
        {
            if (m_permissions != null && (type == AssetType.LSLText || type == AssetType.LSLBytecode || type == AssetType.Notecard || type == AssetType.Object))
            {
                PermissionMask perms = m_permissions.GetAssetPermissions(agent, assetID);
                if (!perms.HasPermission(PermissionMask.Modify))
                {
                    m_log.Warn("Denying inventory download from " + agent.Name + " for asset " + assetID + ", perms=" + perms);
                    return false;
                }
            }

            return true;
        }

        private bool CanDownloadTaskInventory(LLAgent agent, AssetType type, UUID taskID, UUID itemID)
        {
            ISceneEntity entity;
            if (m_scene.TryGetEntity(taskID, out entity))
            {
                if (entity is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)entity;

                    LLInventoryTaskItem item;
                    if (prim.Inventory.TryGetItem(itemID, out item))
                    {
                        bool success;

                        if (item.OwnerID == agent.ID)
                            success = item.Permissions.OwnerMask.HasPermission(PermissionMask.Modify);
                        else
                            success = item.Permissions.EveryoneMask.HasPermission(PermissionMask.Modify);

                        if (!success)
                            m_log.Warn("Denying task inventory download from " + agent.Name + " for item " + item.Name + " in task " + taskID);

                        return success;
                    }
                }
            }

            return false;
        }
    }
}
