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
using System.ComponentModel.Composition;
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("NewFileAgentInventory")]
    public class NewFileAgentInventory : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IHttpServer m_httpServer;
        private IAssetClient m_assetClient;
        private IInventoryClient m_inventoryClient;
        private LLPermissions m_permissions;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_httpServer = m_scene.Simian.GetAppModule<IHttpServer>();
            if (m_httpServer == null)
            {
                m_log.Warn("NewFileAgentInventory requires an IHttpServer");
                return;
            }

            m_assetClient = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Warn("NewFileAgentInventory requires an IAssetClient");
                return;
            }

            m_inventoryClient = m_scene.Simian.GetAppModule<IInventoryClient>();
            if (m_inventoryClient == null)
            {
                m_log.Warn("NewFileAgentInventory requires an IInventoryClient");
            }

            m_permissions = m_scene.GetSceneModule<LLPermissions>();

            //m_scene.Capabilities.AddProtectedResource(m_scene.ID, "NewFileAgentInventory", NewFileAgentInventoryHandler);
            m_scene.Capabilities.AddProtectedResource(m_scene.ID, "NewFileAgentInventoryVariablePrice", NewFileAgentInventoryVariablePriceHandler);
        }

        public void Stop()
        {
            if (m_httpServer != null)
            {
                //m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "NewFileAgentInventory");
                m_scene.Capabilities.RemoveProtectedResource(m_scene.ID, "NewFileAgentInventoryVariablePrice");
            }
        }

        private void NewFileAgentInventoryHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            NewFileAgentInventoryMessage message;
            if (LLUtil.TryGetMessage<NewFileAgentInventoryMessage>(request.Body, out message))
            {
                m_log.Error("Implement NewFileAgentInventory");
            }
            else
            {
                m_log.Warn("Received invalid data for NewFileAgentInventory");
                response.Status = System.Net.HttpStatusCode.BadRequest;
            }
        }

        private void NewFileAgentInventoryVariablePriceHandler(Capability cap, IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            NewFileAgentInventoryVariablePriceMessage message;
            if (LLUtil.TryGetMessage<NewFileAgentInventoryVariablePriceMessage>(request.Body, out message))
            {
                NewFileAgentInventoryVariablePriceReplyMessage reply = new NewFileAgentInventoryVariablePriceReplyMessage();
                reply.ResourceCost = 0;
                reply.UploadPrice = 0;

                // Create a one time upload capability
                reply.Rsvp = m_scene.Capabilities.AddOneTimeCapability(cap.OwnerID, true,
                    delegate(Capability _cap, IHttpClientContext _context, IHttpRequest _request, IHttpResponse _response)
                    { NewFileAgentInventoryVariablePriceUploadHandler(message, _cap, _context, _request, _response); }
                );

                LLUtil.SendLLSDXMLResponse(response, reply.Serialize());
            }
            else
            {
                m_log.Warn("Received invalid data for NewFileAgentInventoryVariablePrice");
                response.Status = System.Net.HttpStatusCode.BadRequest;
            }
        }

        private void NewFileAgentInventoryUploadHandler(NewFileAgentInventoryMessage message, Capability cap,
            IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            byte[] assetData = request.GetBody();
            UUID assetID = UUID.Zero;
            UUID itemID = UUID.Zero;

            m_log.Debug("Received inventory asset upload from " + cap.OwnerID + " (" + assetData.Length + " bytes)");

            if (assetData != null && assetData.Length > 0)
            {
                string contentType = LLUtil.LLAssetTypeToContentType((int)message.AssetType);

                // Create the asset
                if (m_assetClient.StoreAsset(contentType, false, false, assetData, cap.OwnerID, out assetID))
                {
                    // Create the inventory item
                    LLInventoryItem item = new LLInventoryItem();
                    item.AssetID = assetID;
                    item.ContentType = contentType;
                    item.CreationDate = DateTime.UtcNow;
                    item.CreatorID = cap.OwnerID;
                    item.Description = message.Description;
                    item.ID = UUID.Random();
                    item.Name = message.Name;
                    item.OwnerID = cap.OwnerID;
                    item.ParentID = message.FolderID;
                    item.Permissions = (m_permissions != null) ? m_permissions.GetDefaultPermissions() : Permissions.FullPermissions;
                    item.SalePrice = 10;
                    item.SaleType = SaleType.Not;

                    if (m_inventoryClient.TryCreateItem(cap.OwnerID, item))
                        itemID = item.ID;
                    else
                        m_log.Warn("Failed to create inventory item for uploaded asset " + assetID);
                }
                else
                {
                    m_log.WarnFormat("Failed to store uploaded inventory asset ({0} bytes)", assetData.Length);
                }
            }
            else
            {
                m_log.Warn("Inventory asset upload contained no data");
            }

            // Build the response message
            OSDMap reply = new OSDMap
            {
                { "state", OSD.FromString("complete") },
                { "new_asset", OSD.FromUUID(assetID) },
                { "new_inventory_item", OSD.FromUUID(itemID) }
            };

            LLUtil.SendLLSDXMLResponse(response, reply);
        }

        private void NewFileAgentInventoryVariablePriceUploadHandler(NewFileAgentInventoryVariablePriceMessage message, Capability cap,
            IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            byte[] assetData = request.GetBody();
            UUID assetID = UUID.Zero;
            LLInventoryItem item = null;

            m_log.Debug("Received inventory asset upload from " + cap.OwnerID + " (" + assetData.Length + " bytes)");

            if (assetData != null && assetData.Length > 0)
            {
                string contentType = LLUtil.LLAssetTypeToContentType((int)message.AssetType);

                // Create the asset
                if (m_assetClient.StoreAsset(contentType, false, false, assetData, cap.OwnerID, out assetID))
                {
                    // Create the inventory item
                    item = new LLInventoryItem();
                    item.AssetID = assetID;
                    item.ContentType = contentType;
                    item.CreationDate = DateTime.UtcNow;
                    item.CreatorID = cap.OwnerID;
                    item.Description = message.Description;
                    item.ID = UUID.Random();
                    item.Name = message.Name;
                    item.OwnerID = cap.OwnerID;
                    item.ParentID = message.FolderID;

                    Permissions perms = (m_permissions != null) ? m_permissions.GetDefaultPermissions() : Permissions.FullPermissions;
                    perms.EveryoneMask = message.EveryoneMask;
                    perms.GroupMask = message.GroupMask;
                    perms.NextOwnerMask = message.NextOwnerMask;
                    item.Permissions = perms;
                    
                    item.SalePrice = 10;
                    item.SaleType = SaleType.Not;

                    if (!m_inventoryClient.TryCreateItem(cap.OwnerID, item))
                        m_log.Warn("Failed to create inventory item for uploaded asset " + assetID);
                }
                else
                {
                    m_log.WarnFormat("Failed to store uploaded inventory asset ({0} bytes)", assetData.Length);
                }
            }
            else
            {
                m_log.Warn("Inventory asset upload contained no data");
            }

            // Build the response message
            NewFileAgentInventoryUploadReplyMessage reply = new NewFileAgentInventoryUploadReplyMessage();
            reply.NewAsset = assetID;
            if (item != null)
            {
                reply.NewInventoryItem = item.ID;
                reply.NewBaseMask = item.Permissions.BaseMask;
                reply.NewEveryoneMask = item.Permissions.EveryoneMask;
                reply.NewNextOwnerMask = item.Permissions.NextOwnerMask;
                reply.NewOwnerMask = item.Permissions.OwnerMask;
            }

            LLUtil.SendLLSDXMLResponse(response, reply.Serialize());
        }
    }
}
