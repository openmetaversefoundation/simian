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
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden.Packets
{
    [SceneModule("TaskInventory")]
    public class TaskInventory : ISceneModule
    {
        public static readonly UUID DEFAULT_SCRIPT = new UUID("a7f70b8e-b2ee-46bb-85c0-5d973137cd47");

        private const int REQUEST_TIMEOUT = 1000 * 30;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IInventoryClient m_inventory;
        private LLUDP m_udp;
        private ILSLScriptEngine m_scriptEngine;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_inventory = m_scene.Simian.GetAppModule<IInventoryClient>();
            m_scriptEngine = m_scene.GetSceneModule<ILSLScriptEngine>();

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.RequestTaskInventory, RequestTaskInventoryHandler);
                m_udp.AddPacketHandler(PacketType.UpdateTaskInventory, UpdateTaskInventoryHandler);
                m_udp.AddPacketHandler(PacketType.RezScript, RezScriptHandler);
                m_udp.AddPacketHandler(PacketType.RemoveTaskInventory, RemoveTaskInventoryHandler);
                m_udp.AddPacketHandler(PacketType.MoveTaskInventory, MoveTaskInventoryHandler);
                m_udp.AddPacketHandler(PacketType.GetScriptRunning, GetScriptRunningHandler);
            }
        }

        public void Stop()
        {
            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.RequestTaskInventory, RequestTaskInventoryHandler);
                m_udp.RemovePacketHandler(PacketType.UpdateTaskInventory, UpdateTaskInventoryHandler);
                m_udp.RemovePacketHandler(PacketType.RezScript, RezScriptHandler);
                m_udp.RemovePacketHandler(PacketType.RemoveTaskInventory, RemoveTaskInventoryHandler);
                m_udp.RemovePacketHandler(PacketType.MoveTaskInventory, MoveTaskInventoryHandler);
                m_udp.RemovePacketHandler(PacketType.GetScriptRunning, GetScriptRunningHandler);
            }
        }

        private void RequestTaskInventoryHandler(Packet packet, LLAgent agent)
        {
            RequestTaskInventoryPacket request = (RequestTaskInventoryPacket)packet;

            // Try to find this object in the scene
            ISceneEntity entity;
            if (m_scene.TryGetEntity(request.InventoryData.LocalID, out entity) && entity is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)entity;

                ReplyTaskInventoryPacket reply = new ReplyTaskInventoryPacket();
                reply.InventoryData.Filename = Utils.StringToBytes(prim.Inventory.GetInventoryFilename());
                reply.InventoryData.Serial = prim.Inventory.InventorySerial;
                reply.InventoryData.TaskID = prim.ID;

                m_udp.SendPacket(agent, reply, ThrottleCategory.Task, false);
            }
            else
            {
                m_log.Warn(agent.Name + " requested task inventory for prim " + request.InventoryData.LocalID +
                    " that does not exist in this scene");
            }
        }

        private void UpdateTaskInventoryHandler(Packet packet, LLAgent agent)
        {
            UpdateTaskInventoryPacket update = (UpdateTaskInventoryPacket)packet;

            LLInventoryTaskItem item;
            ISceneEntity targetObj;

            if (update.UpdateData.Key != 0)
            {
                m_log.Warn("Got an UpdateTaskInventory packet with a Key of " + update.UpdateData.Key);
                return;
            }

            if (m_scene.TryGetEntity(update.UpdateData.LocalID, out targetObj) && targetObj is LLPrimitive)
            {
                LLPrimitive targetPrim = (LLPrimitive)targetObj;

                // Updating an existing item in the task inventory
                if (targetPrim.Inventory.TryGetItem(update.InventoryData.ItemID, out item))
                {
                    if (update.InventoryData.TransactionID != UUID.Zero)
                        item.AssetID = UUID.Combine(update.InventoryData.TransactionID, agent.SecureSessionID);

                    item.Description = Utils.BytesToString(update.InventoryData.Description);
                    item.Flags = update.InventoryData.Flags;
                    item.GroupID = update.InventoryData.GroupID;
                    item.GroupOwned = update.InventoryData.GroupOwned;
                    item.Name = Utils.BytesToString(update.InventoryData.Name);
                    item.Permissions = new Permissions(update.InventoryData.BaseMask, update.InventoryData.EveryoneMask,
                        update.InventoryData.GroupMask, update.InventoryData.NextOwnerMask, update.InventoryData.OwnerMask);
                    item.SalePrice = update.InventoryData.SalePrice;
                    item.SaleType = (SaleType)update.InventoryData.SaleType;

                    targetPrim.Inventory.AddOrUpdateItem(item, true);
                    m_log.Debug(agent.Name + " updated task inventory item: " + item.Name);

                    SignalTaskInventoryChange(agent, targetPrim);
                }
                else if (m_inventory != null)
                {
                    // Copying from agent inventory to task inventory
                    InventoryBase obj;
                    if (m_inventory.TryGetInventory(agent.ID, update.InventoryData.ItemID, out obj))
                    {
                        if (obj is InventoryItem)
                        {
                            // Create a new item in the task inventory
                            LLInventoryItem fromItem = new LLInventoryItem((InventoryItem)obj);

                            item = new LLInventoryTaskItem();
                            //item.ID will be assigned in AddOrUpdateItem
                            item.AssetID = fromItem.AssetID;
                            item.ContentType = fromItem.ContentType;
                            item.CreationDate = fromItem.CreationDate;
                            item.CreatorID = fromItem.CreatorID;
                            item.Description = fromItem.Description;
                            item.Flags = fromItem.ExtraData["Flags"].AsUInteger();
                            item.GrantedPermissions = 0;
                            item.GroupID = fromItem.ExtraData["group_id"].AsUUID();
                            item.GroupOwned = fromItem.ExtraData["group_owned"].AsBoolean();
                            item.Name = fromItem.Name;
                            item.OwnerID = agent.ID;
                            item.ParentID = update.InventoryData.FolderID;
                            item.ParentObjectID = targetPrim.ID;
                            item.PermissionGranter = UUID.Zero;
                            item.Permissions = fromItem.Permissions;
                            item.SalePrice = fromItem.ExtraData["sale_price"].AsInteger();
                            item.SaleType = (SaleType)fromItem.ExtraData["sale_type"].AsInteger();

                            targetPrim.Inventory.AddOrUpdateItem(item, false);
                            m_log.Debug(agent.Name + " created new task inventory item: " + item.Name);

                            SignalTaskInventoryChange(agent, targetPrim);
                        }
                        else
                        {
                            m_log.Error("[TODO] Handle dropping folders in task inventory");
                        }
                    }
                    else
                    {
                        m_log.Warn(agent.Name + " sent an UpdateTaskInventory packet requesting unknown " +
                            "(or failed to fetch) inventory item " + update.InventoryData.ItemID);
                    }
                }
                else
                {
                    m_log.Warn(agent.Name + "attempted to copy inventory item " + update.InventoryData.ItemID +
                        " to task inventory, but we have no IInventoryClient");
                }
            }
            else
            {
                m_log.Warn(agent.Name + " attempted to update task inventory for prim " + update.UpdateData.LocalID +
                    " that does not exist in this scene");
            }
        }

        private void RezScriptHandler(Packet packet, LLAgent agent)
        {
            RezScriptPacket rez = (RezScriptPacket)packet;

            LLInventoryTaskItem scriptItem;
            ISceneEntity targetObj;

            if (m_scene.TryGetEntity(rez.UpdateBlock.ObjectLocalID, out targetObj) && targetObj is LLPrimitive)
            {
                LLPrimitive targetPrim = (LLPrimitive)targetObj;

                if (rez.InventoryBlock.ItemID != UUID.Zero)
                {
                    if (targetPrim.Inventory.TryGetItem(rez.InventoryBlock.ItemID, out scriptItem))
                    {
                        // Rezzing a script from task inventory
                        UUID assetID = UUID.Combine(rez.InventoryBlock.TransactionID, agent.SecureSessionID);

                        // Update task inventory with the new script source assetID
                        scriptItem.AssetID = assetID;
                        targetPrim.Inventory.AddOrUpdateItem(scriptItem, true);

                        // Run the script
                        if (m_scriptEngine != null)
                            m_scriptEngine.RezScript(scriptItem.ID, assetID, targetObj, 0);
                        else
                            m_log.Warn("Can't rez script in prim " + targetObj.ID + " without an ILSLScriptEngine");

                        SignalTaskInventoryChange(agent, targetPrim);
                    }
                    else if (m_inventory != null)
                    {
                        InventoryBase obj;
                        if (m_inventory.TryGetInventory(agent.ID, rez.InventoryBlock.ItemID, out obj) &&
                            obj is InventoryItem && ((InventoryItem)obj).ContentType == "application/vnd.ll.lsltext")
                        {
                            LLInventoryItem sourceItem = new LLInventoryItem((InventoryItem)obj);

                            // Rezzing a script from agent inventory
                            scriptItem = new LLInventoryTaskItem();
                            scriptItem.AssetID = sourceItem.AssetID;
                            scriptItem.ContentType = "application/vnd.ll.lsltext";
                            scriptItem.CreationDate = DateTime.UtcNow;
                            scriptItem.CreatorID = agent.ID;
                            scriptItem.Description = sourceItem.Description;
                            scriptItem.ID = UUID.Random();
                            scriptItem.Name = sourceItem.Name;
                            scriptItem.OwnerID = sourceItem.OwnerID;
                            scriptItem.ParentID = sourceItem.ParentID;
                            scriptItem.ParentObjectID = targetPrim.ID;

                            scriptItem.Flags = sourceItem.Flags;
                            scriptItem.GroupID = sourceItem.GroupID;
                            scriptItem.GroupOwned = sourceItem.GroupOwned;
                            scriptItem.Permissions = sourceItem.Permissions;
                            scriptItem.SalePrice = sourceItem.SalePrice;
                            scriptItem.SaleType = sourceItem.SaleType;

                            targetPrim.Inventory.AddOrUpdateItem(scriptItem, false);
                            m_log.Info(agent.Name + " copied agent inventory script to task inventory: " + scriptItem.Name);

                            // Run the script
                            if (m_scriptEngine != null)
                                m_scriptEngine.RezScript(scriptItem.ID, scriptItem.AssetID, targetObj, 0);
                            else
                                m_log.Warn("Can't rez script in prim " + targetObj.ID + " without an ILSLScriptEngine");

                            SignalTaskInventoryChange(agent, targetPrim);
                        }
                        else
                        {
                            m_log.Warn(agent.Name + " called RezScript for unknown inventory script " + rez.InventoryBlock.ItemID);
                        }
                    }
                    else
                    {
                        m_log.Warn(agent.Name + "attempted to copy (and rez) script " + rez.InventoryBlock.ItemID +
                            " to task inventory, but we have no IInventoryClient");
                    }
                }
                else
                {
                    // Rezzing a new script
                    scriptItem = new LLInventoryTaskItem();
                    scriptItem.AssetID = DEFAULT_SCRIPT;
                    scriptItem.ContentType = "application/vnd.ll.lsltext";
                    scriptItem.CreationDate = DateTime.UtcNow;
                    scriptItem.CreatorID = agent.ID;
                    scriptItem.Description = String.Empty;
                    scriptItem.ID = UUID.Random();
                    scriptItem.Name = "New script";
                    scriptItem.OwnerID = agent.ID;
                    scriptItem.ParentID = rez.InventoryBlock.FolderID;
                    scriptItem.ParentObjectID = targetPrim.ID;
                    scriptItem.Permissions = GetDefaultPermissions();
                    scriptItem.SalePrice = 10;
                    scriptItem.SaleType = SaleType.Not;

                    targetPrim.Inventory.AddOrUpdateItem(scriptItem, false);
                    m_log.Info(agent.Name + " created new task inventory script: " + scriptItem.Name);

                    // Run the script
                    if (m_scriptEngine != null)
                        m_scriptEngine.RezScript(scriptItem.ID, scriptItem.AssetID, targetObj, 0);
                    else
                        m_log.Warn("Can't rez script in prim " + targetObj.ID + " without an ILSLScriptEngine");

                    SignalTaskInventoryChange(agent, targetPrim);
                }
            }
            else
            {
                m_log.Warn(agent.Name + "sent a RezScript packet referencing unknown object " + rez.UpdateBlock.ObjectLocalID);
            }
        }

        private void RemoveTaskInventoryHandler(Packet packet, LLAgent agent)
        {
            RemoveTaskInventoryPacket remove = (RemoveTaskInventoryPacket)packet;

            ISceneEntity targetObj;
            if (m_scene.TryGetEntity(remove.InventoryData.LocalID, out targetObj) && targetObj is LLPrimitive)
            {
                RemoveTaskInventory(agent, (LLPrimitive)targetObj, remove.InventoryData.ItemID);
            }
            else
            {
                m_log.Warn(agent.Name + " attempted to remove task inventory item " + remove.InventoryData.ItemID +
                    " from unknown prim " + remove.InventoryData.LocalID);
            }
        }

        private void MoveTaskInventoryHandler(Packet packet, LLAgent agent)
        {
            MoveTaskInventoryPacket move = (MoveTaskInventoryPacket)packet;

            if (m_inventory != null)
            {
                LLInventoryTaskItem item;
                ISceneEntity sourceObj;

                if (m_scene.TryGetEntity(move.InventoryData.LocalID, out sourceObj) && sourceObj is LLPrimitive)
                {
                    LLPrimitive sourcePrim = (LLPrimitive)sourceObj;

                    if (sourcePrim.Inventory.TryGetItem(move.InventoryData.ItemID, out item))
                    {
                        InventoryBase obj;
                        if (m_inventory.TryGetInventory(agent.ID, move.AgentData.FolderID, out obj) && obj is InventoryFolder)
                        {
                            LLInventoryItem invItem = new LLInventoryItem
                            {
                                AssetID = item.AssetID,
                                ContentType = item.ContentType,
                                CreationDate = item.CreationDate,
                                CreatorID = item.CreatorID,
                                Description = item.Description,
                                ExtraData = item.ExtraData,
                                ID = UUID.Random(),
                                Name = item.Name,
                                OwnerID = agent.ID,
                                ParentID = move.AgentData.FolderID
                            };

                            if (m_inventory.TryCreateItem(agent.ID, invItem))
                            {
                                RemoveTaskInventory(agent, sourcePrim, item.ID);
                                SendItemCreatedPacket(agent, invItem, UUID.Zero, 0);
                                m_log.Debug(agent.Name + " moved task inventory item " + item.Name + " to agent inventory folder " + invItem.ParentID);
                            }
                            else
                            {
                                m_log.Warn(agent.Name + "attempted to move item " + move.InventoryData.ItemID +
                                    " to agent inventory folder " + move.AgentData.FolderID + " but item creation failed");
                            }
                        }
                        else
                        {
                            m_log.Warn(agent.Name + "attempted to move item " + move.InventoryData.ItemID +
                                " to unknown agent inventory folder " + move.AgentData.FolderID);
                        }
                    }
                }
            }
            else
            {
                m_log.Warn(agent.Name + "attempted to move item " + move.InventoryData.ItemID +
                    " to agent inventory, but we have no IInventoryClient");
            }
        }

        private void GetScriptRunningHandler(Packet packet, LLAgent agent)
        {
            GetScriptRunningPacket getRunning = (GetScriptRunningPacket)packet;

            ScriptRunningReplyMessage reply = new ScriptRunningReplyMessage();
            reply.ItemID = getRunning.Script.ItemID; ;
            reply.ObjectID = getRunning.Script.ObjectID;
            reply.Running = m_scriptEngine.IsScriptRunning(getRunning.Script.ItemID);
            reply.Mono = true;

            agent.EventQueue.QueueEvent("ScriptRunningReply", reply.Serialize());
        }

        private bool RemoveTaskInventory(LLAgent agent, LLPrimitive host, UUID itemID)
        {
            LLInventoryTaskItem item;

            if (host.Inventory.TryGetItem(itemID, out item))
            {
                host.Inventory.RemoveItem(itemID);
                m_log.Debug(agent.Name + " removed task inventory item: " + item.Name);

                if (item.AssetType == AssetType.LSLText && m_scriptEngine != null)
                    m_scriptEngine.StopScript(item.ID);

                SignalTaskInventoryChange(agent, host);
                return true;
            }
            else
            {
                m_log.Warn(agent.Name + " attempted to remove unknown task inventory item " + itemID +
                    " from prim " + host.LocalID);
                return false;
            }
        }

        private void SignalTaskInventoryChange(LLAgent agent, LLPrimitive prim)
        {
            // Send an ObjectPropertiesReply to inform the client that inventory has changed
            ObjectPropertiesPacket props = new ObjectPropertiesPacket();
            props.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
            props.ObjectData[0] = LLUtil.BuildEntityPropertiesBlock(prim);
            m_udp.SendPacket(agent, props, ThrottleCategory.Task, false);

            // Signal this prim for serialization
            m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.Serialize, 0);
        }

        private Permissions GetDefaultPermissions()
        {
            Permissions perms = Permissions.NoPermissions;
            perms.BaseMask = PermissionMask.All;
            perms.OwnerMask = PermissionMask.All;
            perms.NextOwnerMask = PermissionMask.All;
            return perms;
        }

        private void SendItemCreatedPacket(LLAgent agent, LLInventoryItem item, UUID transactionID, uint callbackID)
        {
            UpdateCreateInventoryItemPacket update = new UpdateCreateInventoryItemPacket();
            update.AgentData.AgentID = agent.ID;
            update.AgentData.SimApproved = true;
            if (transactionID != UUID.Zero)
                update.AgentData.TransactionID = transactionID;
            else
                update.AgentData.TransactionID = UUID.Random();

            Permissions perms = item.Permissions;

            UpdateCreateInventoryItemPacket.InventoryDataBlock invData = new UpdateCreateInventoryItemPacket.InventoryDataBlock();
            invData.AssetID = item.AssetID;
            invData.BaseMask = (uint)perms.BaseMask;
            invData.CallbackID = callbackID;
            invData.CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate);
            invData.CRC = item.CRC();
            invData.CreatorID = item.CreatorID;
            invData.Description = Utils.StringToBytes(item.Description);
            invData.EveryoneMask = (uint)perms.EveryoneMask;
            invData.Flags = item.Flags;
            invData.FolderID = item.ParentID;
            invData.GroupID = item.GroupID;
            invData.GroupMask = (uint)perms.GroupMask;
            invData.GroupOwned = item.GroupOwned;
            invData.InvType = (sbyte)item.InventoryType;
            invData.ItemID = item.ID;
            invData.Name = Utils.StringToBytes(item.Name);
            invData.NextOwnerMask = (uint)perms.NextOwnerMask;
            invData.OwnerID = item.OwnerID;
            invData.OwnerMask = (uint)perms.OwnerMask;
            invData.SalePrice = item.SalePrice;
            invData.SaleType = (byte)item.SaleType;
            invData.Type = (sbyte)item.AssetType;

            update.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[1];
            update.InventoryData[0] = invData;

            m_log.DebugFormat("Created inventory item {0}. ItemID: {1}, AssetID: {2}, ParentID: {3}, TransactionID: {4}, CallbackID: {5}",
                item.Name, item.ID, item.AssetID, item.ParentID, transactionID, callbackID);

            m_udp.SendPacket(agent, update, ThrottleCategory.Task, false);
        }
    }
}
