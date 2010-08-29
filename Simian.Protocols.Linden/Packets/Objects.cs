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
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden.Packets
{
    /// <summary>
    /// Reason for a cache miss, found in incoming RequestMultipleObject packets
    /// </summary>
    public enum CacheMissType
    {
        /// <summary>No matching object ID</summary>
        Full = 0,
        /// <summary>Object found, but with a different CRC</summary>
        CRC = 1
    }

    /// <summary>
    /// Handles logins, logouts, teleports, region crossings, bandwidth settings and more for agents
    /// </summary>
    [SceneModule("Objects")]
    public class Objects : ISceneModule
    {
        #region Constants

        // TODO: Enable this when the stable viewer release has a functional cache
        const bool CACHE_CHECK_ENABLED = false;

        const string OBJECT_UPDATE = "UpdateObject";
        const string OBJECT_REMOVE = "RemoveObject";

        const int AVATAR_TRACKING_COUNT = 500;

        // Scripting event flags
        const int AGENT = 1;
        const int ACTIVE = 2;
        const int PASSIVE = 4;
        const int SCRIPTED = 8;

        const int CHANGED_COLOR = 2;
        const int CHANGED_SHAPE = 4;
        const int CHANGED_SCALE = 8;
        const int CHANGED_TEXTURE = 16;
        const int CHANGED_LINK = 32;

        private static readonly UUID PLYWOOD_TEXTURE = new UUID("89556747-24cb-43ed-920b-47caed15465f");

        private static readonly Quaternion INVALID_ROT = new Quaternion(0f, 0f, 0f, 0f);

        #endregion Constants

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private LLUDP m_udp;
        private IPhysicsEngine m_physics;
        private IPrimMesher m_primMesher;
        private ILSLScriptEngine m_lslScriptEngine;
        private LLPermissions m_permissions;
        private IDataStore m_dataStore;
        private IStatsTracker m_statsTracker;
        private Inventory m_inventory;
        private Primitive m_proxyPrim;
        private Dictionary<UUID, DateTime> m_recentAvatars = new Dictionary<UUID, DateTime>(AVATAR_TRACKING_COUNT);

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_primMesher = m_scene.GetSceneModule<IPrimMesher>();
            if (m_primMesher == null)
            {
                m_log.Error("Objects requires an IPrimMesher");
                return;
            }

            m_permissions = m_scene.GetSceneModule<LLPermissions>();
            if (m_permissions == null)
            {
                m_log.Error("Objects requires LLPermissions");
                return;
            }

            // Optional modules
            m_physics = m_scene.GetSceneModule<IPhysicsEngine>();
            m_lslScriptEngine = m_scene.GetSceneModule<ILSLScriptEngine>();
            m_inventory = m_scene.GetSceneModule<Inventory>();
            m_dataStore = m_scene.Simian.GetAppModule<IDataStore>();
            m_statsTracker = m_scene.Simian.GetAppModule<IStatsTracker>();

            // Collision handler
            if (m_physics != null && m_lslScriptEngine != null)
            {
                m_physics.OnEntityCollision += EntityCollisionHandler;
            }

            m_udp = m_scene.GetSceneModule<LLUDP>();
            if (m_udp != null)
            {
                m_udp.AddPacketHandler(PacketType.ObjectAdd, ObjectAddHandler);
                m_udp.AddPacketHandler(PacketType.ObjectAttach, ObjectAttachHandler);
                m_udp.AddPacketHandler(PacketType.ObjectDrop, ObjectDropHandler);
                m_udp.AddPacketHandler(PacketType.ObjectDuplicate, ObjectDuplicateHandler);
                m_udp.AddPacketHandler(PacketType.ObjectName, ObjectNameHandler);
                m_udp.AddPacketHandler(PacketType.ObjectSelect, ObjectSelectHandler);
                m_udp.AddPacketHandler(PacketType.ObjectDeselect, ObjectDeselectHandler);
                m_udp.AddPacketHandler(PacketType.ObjectGrab, ObjectGrabHandler);
                m_udp.AddPacketHandler(PacketType.ObjectGrabUpdate, ObjectGrabUpdateHandler);
                m_udp.AddPacketHandler(PacketType.ObjectDeGrab, ObjectDeGrabHandler);
                m_udp.AddPacketHandler(PacketType.ObjectLink, ObjectLinkHandler);
                m_udp.AddPacketHandler(PacketType.ObjectDelink, ObjectDelinkHandler);
                m_udp.AddPacketHandler(PacketType.ObjectShape, ObjectShapeHandler);
                m_udp.AddPacketHandler(PacketType.ObjectFlagUpdate, ObjectFlagUpdateHandler);
                m_udp.AddPacketHandler(PacketType.ObjectExtraParams, ObjectExtraParamsHandler);
                m_udp.AddPacketHandler(PacketType.ObjectImage, ObjectImageHandler);
                m_udp.AddPacketHandler(PacketType.ObjectPermissions, ObjectPermissionsHandler);
                m_udp.AddPacketHandler(PacketType.Undo, UndoHandler);
                m_udp.AddPacketHandler(PacketType.Redo, RedoHandler);
                m_udp.AddPacketHandler(PacketType.MultipleObjectUpdate, MultipleObjectUpdateHandler);
                m_udp.AddPacketHandler(PacketType.RequestObjectPropertiesFamily, RequestObjectPropertiesFamilyHandler);
                m_udp.AddPacketHandler(PacketType.RequestMultipleObjects, RequestMultipleObjectsHandler);

                m_scene.AddInterestListHandler(OBJECT_UPDATE, new InterestListEventHandler { CombineCallback = ObjectUpdateCombiner, SendCallback = SendEntityPackets });
                m_scene.AddInterestListHandler(OBJECT_REMOVE, new InterestListEventHandler { CombineCallback = ObjectUpdateCombiner, SendCallback = SendKillPacket });

                m_scene.OnEntityAddOrUpdate += EntityAddOrUpdateHandler;
                m_scene.OnEntityRemove += EntityRemoveHandler;
                m_scene.OnPresenceAdd += PresenceAddHandler;
                m_scene.OnPresenceRemove += PresenceRemoveHandler;
            }

            m_proxyPrim = new Primitive();
            m_proxyPrim.PrimData = ObjectManager.BuildBasicShape(PrimType.Box);

            DeserializeRecentAvatars();
        }

        public void Stop()
        {
            if (m_physics != null && m_lslScriptEngine != null)
            {
                m_physics.OnEntityCollision -= EntityCollisionHandler;
            }

            if (m_udp != null)
            {
                m_udp.RemovePacketHandler(PacketType.ObjectAdd, ObjectAddHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectAttach, ObjectAttachHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectDrop, ObjectDropHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectDuplicate, ObjectDuplicateHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectName, ObjectNameHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectSelect, ObjectSelectHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectDeselect, ObjectDeselectHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectGrab, ObjectGrabHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectGrabUpdate, ObjectGrabUpdateHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectDeGrab, ObjectDeGrabHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectLink, ObjectLinkHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectDelink, ObjectDelinkHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectShape, ObjectShapeHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectFlagUpdate, ObjectFlagUpdateHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectExtraParams, ObjectExtraParamsHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectImage, ObjectImageHandler);
                m_udp.RemovePacketHandler(PacketType.ObjectPermissions, ObjectPermissionsHandler);
                m_udp.RemovePacketHandler(PacketType.Undo, UndoHandler);
                m_udp.RemovePacketHandler(PacketType.Redo, RedoHandler);
                m_udp.RemovePacketHandler(PacketType.MultipleObjectUpdate, MultipleObjectUpdateHandler);
                m_udp.RemovePacketHandler(PacketType.RequestObjectPropertiesFamily, RequestObjectPropertiesFamilyHandler);
                m_udp.RemovePacketHandler(PacketType.RequestMultipleObjects, RequestMultipleObjectsHandler);

                m_scene.OnEntityAddOrUpdate -= EntityAddOrUpdateHandler;
                m_scene.OnEntityRemove -= EntityRemoveHandler;
                m_scene.OnPresenceAdd -= PresenceAddHandler;
            }

            SerializeRecentAvatars();
        }

        #region Client Packet Handling

        void ObjectAddHandler(Packet packet, LLAgent agent)
        {
            if (!agent.IsVerified)
            {
                m_scene.PresenceAlert(this, agent, "You are logged in with an unverified account. Object creation is disabled.");
                return;
            }

            ObjectAddPacket add = (ObjectAddPacket)packet;

            Vector3 position = Vector3.Zero;
            Vector3 scale = add.ObjectData.Scale;
            PCode pcode = (PCode)add.ObjectData.PCode;
            PrimFlags flags = (PrimFlags)add.ObjectData.AddFlags;
            bool bypassRaycast = (add.ObjectData.BypassRaycast == 1);
            //bool rayEndIsIntersection = (add.ObjectData.RayEndIsIntersection == 1);

            #region Position Calculation

            if (bypassRaycast)
            {
                position = add.ObjectData.RayEnd;
            }
            else if (m_physics != null)
            {
                Vector3 direction = (add.ObjectData.RayEnd - add.ObjectData.RayStart);
                direction /= direction.Length();
                Ray ray = new Ray(add.ObjectData.RayStart, direction);

                ISceneEntity collisionObj;
                float collisionDist;
                if (m_physics.FullSceneCollisionTest(true, ray, null, out collisionObj, out collisionDist))
                {
                    position = ray.GetPoint(collisionDist);
                }
                else
                {
                    m_log.Warn("Full scene collision test for ray " + ray + " failed");
                    position = agent.ScenePosition + Vector3.UnitZ;
                }
            }

            position.Z += scale.Z * 0.5f;

            #endregion Position Calculation

            if (!CanAddPrims(agent, position, 1))
                return;

            #region Foliage Handling

            // Set all foliage to phantom
            if (pcode == PCode.Grass || pcode == PCode.Tree || pcode == PCode.NewTree)
            {
                flags |= PrimFlags.Phantom;

                if (pcode != PCode.Grass)
                {
                    // Resize based on the foliage type
                    Tree tree = (Tree)add.ObjectData.State;

                    switch (tree)
                    {
                        case Tree.Cypress1:
                        case Tree.Cypress2:
                            scale = new Vector3(4f, 4f, 10f);
                            break;
                        default:
                            scale = new Vector3(4f, 4f, 4f);
                            break;
                    }
                }
            }

            #endregion Foliage Handling

            #region Prim Creation

            // Create an object
            Primitive prim = new Primitive();

            prim.Flags = PrimFlags.CastShadows | PrimFlags.InventoryEmpty;
            prim.Flags |= (PrimFlags)add.ObjectData.AddFlags;

            // TODO: Security check
            prim.GroupID = add.AgentData.GroupID;
            prim.ID = UUID.Random();
            prim.MediaURL = String.Empty;
            prim.OwnerID = agent.ID;
            prim.Position = position;

            prim.PrimData.Material = (Material)add.ObjectData.Material;
            prim.PrimData.PathCurve = (PathCurve)add.ObjectData.PathCurve;
            prim.PrimData.ProfileCurve = (ProfileCurve)add.ObjectData.ProfileCurve;
            prim.PrimData.PathBegin = Primitive.UnpackBeginCut(add.ObjectData.PathBegin);
            prim.PrimData.PathEnd = Primitive.UnpackEndCut(add.ObjectData.PathEnd);
            prim.PrimData.PathScaleX = Primitive.UnpackPathScale(add.ObjectData.PathScaleX);
            prim.PrimData.PathScaleY = Primitive.UnpackPathScale(add.ObjectData.PathScaleY);
            prim.PrimData.PathShearX = Primitive.UnpackPathShear((sbyte)add.ObjectData.PathShearX);
            prim.PrimData.PathShearY = Primitive.UnpackPathShear((sbyte)add.ObjectData.PathShearY);
            prim.PrimData.PathTwist = Primitive.UnpackPathTwist(add.ObjectData.PathTwist);
            prim.PrimData.PathTwistBegin = Primitive.UnpackPathTwist(add.ObjectData.PathTwistBegin);
            prim.PrimData.PathRadiusOffset = Primitive.UnpackPathTwist(add.ObjectData.PathRadiusOffset);
            prim.PrimData.PathTaperX = Primitive.UnpackPathTaper(add.ObjectData.PathTaperX);
            prim.PrimData.PathTaperY = Primitive.UnpackPathTaper(add.ObjectData.PathTaperY);
            prim.PrimData.PathRevolutions = Primitive.UnpackPathRevolutions(add.ObjectData.PathRevolutions);
            prim.PrimData.PathSkew = Primitive.UnpackPathTwist(add.ObjectData.PathSkew);
            prim.PrimData.ProfileBegin = Primitive.UnpackBeginCut(add.ObjectData.ProfileBegin);
            prim.PrimData.ProfileEnd = Primitive.UnpackEndCut(add.ObjectData.ProfileEnd);
            prim.PrimData.ProfileHollow = Primitive.UnpackProfileHollow(add.ObjectData.ProfileHollow);
            prim.PrimData.PCode = pcode;

            prim.Properties = new Primitive.ObjectProperties();
            prim.Properties.CreationDate = DateTime.UtcNow;
            prim.Properties.CreatorID = agent.ID;
            prim.Properties.Description = String.Empty;
            prim.Properties.GroupID = add.AgentData.GroupID;
            prim.Properties.LastOwnerID = agent.ID;
            prim.Properties.Name = "Object";
            prim.Properties.ObjectID = prim.ID;
            prim.Properties.OwnerID = prim.OwnerID;
            prim.Properties.Permissions = m_permissions.GetDefaultPermissions();
            prim.Properties.SalePrice = 10;

            prim.Rotation = add.ObjectData.Rotation;
            prim.Scale = scale;
            prim.TextColor = Color4.Black;

            if (pcode == PCode.Prim)
            {
                prim.Textures = new Primitive.TextureEntry(PLYWOOD_TEXTURE);
            }

            #endregion Prim Creation

            // Add this prim to the scene
            ISceneEntity primObj = new LLPrimitive(prim, m_scene, m_primMesher);
            m_scene.EntityAddOrUpdate(this, primObj, UpdateFlags.FullUpdate, 0);
        }

        void ObjectAttachHandler(Packet packet, LLAgent agent)
        {
            ObjectAttachPacket attach = (ObjectAttachPacket)packet;

            for (int i = 0; i < attach.ObjectData.Length; i++)
            {
                ObjectAttachPacket.ObjectDataBlock block = attach.ObjectData[i];

                ISceneEntity entity;
                if (m_scene.TryGetEntity(block.ObjectLocalID, out entity) && entity is LLPrimitive)
                {
                    LLPrimitive obj = (LLPrimitive)entity;

                    // Permission check
                    if (obj.OwnerID != agent.ID)
                    {
                        m_log.Warn(agent.Name + " tried to attach " + obj.ID + " owned by " + obj.OwnerID);
                        continue;
                    }

                    // Sanity checks
                    if (obj.Parent != null)
                    {
                        m_log.Warn(agent.Name + " tried to attach child prim " + obj.ID);
                        continue;
                    }
                    ILinkable[] childObjs = obj.GetChildren();
                    for (int j = 0; j < childObjs.Length; j++)
                    {
                        if (childObjs[i] is IScenePresence)
                        {
                            m_log.Warn(agent.Name + " attempted to attach " + obj.ID + " with an avatar sitting on it");
                            continue;
                        }
                    }

                    // Determine what attach point to use
                    AttachmentPoint requestedAttachPoint = (AttachmentPoint)attach.AgentData.AttachmentPoint;
                    AttachmentPoint attachPoint = (requestedAttachPoint == AttachmentPoint.Default ? obj.LastAttachmentPoint : requestedAttachPoint);
                    if (attachPoint == AttachmentPoint.Default)
                        attachPoint = AttachmentPoint.RightHand;

                    // If we are attaching to a new attachment point, reset the attachment position and rotation for this object
                    if (attachPoint != obj.LastAttachmentPoint)
                    {
                        obj.AttachmentPosition = Vector3.Zero;
                        obj.AttachmentRotation = Quaternion.Identity;
                    }

                    // Check if something is already attached to the target attachment point
                    ILinkable[] attachments = agent.GetChildren();
                    for (int j = 0; j < attachments.Length; j++)
                    {
                        if (attachments[j] is LLPrimitive)
                        {
                            LLPrimitive primAttachment = (LLPrimitive)attachments[j];
                            if (primAttachment.Prim.PrimData.AttachmentPoint == attachPoint)
                            {
                                DetachObject(agent, primAttachment);
                                break;
                            }
                        }
                    }

                    // Attaching destroys undo history
                    obj.ClearUndoHistory();

                    // Set the attachment point
                    obj.Prim.PrimData.AttachmentPoint = attachPoint;
                    obj.LastAttachmentPoint = attachPoint;
                    obj.BeforeAttachmentRotation = block.Rotation;

                    // Create the inventory item for this attachment
                    if (m_inventory != null)
                    {
                        // Serialize this prim and any children
                        string contentType = LLUtil.LLAssetTypeToContentType((int)AssetType.Object);
                        byte[] assetData = System.Text.Encoding.UTF8.GetBytes(
                            OpenMetaverse.StructuredData.OSDParser.SerializeJsonString(LLPrimitive.SerializeLinkset(obj)));

                        LLInventoryItem item = m_inventory.ObjectToInventory(agent.ID, obj, assetData, contentType, (uint)attachPoint, true, UUID.Zero, UUID.Zero, false, 0);

                        if (item != null)
                        {
                            obj.Prim.Properties.ItemID = item.ID;
                            obj.Prim.Properties.FolderID = item.ParentID;
                        }
                        else
                        {
                            m_log.Warn(agent.Name + " failed to store attachment " + obj.ID + " to inventory");
                        }
                    }

                    // Do the actual attachment
                    obj.RelativePosition = obj.AttachmentPosition;
                    obj.RelativeRotation = obj.AttachmentRotation;
                    obj.SetParent(agent, false, false);

                    // Send an update out to everyone
                    m_scene.EntityAddOrUpdate(this, obj, UpdateFlags.Parent | UpdateFlags.Position | UpdateFlags.Rotation, (uint)LLUpdateFlags.AttachmentPoint);

                    if (m_lslScriptEngine != null)
                    {
                        m_lslScriptEngine.PostObjectEvent(entity.ID, "attach", new object[] { agent.ID.ToString() }, new DetectParams[0]);
                    }
                }
                else
                {
                    m_log.Warn("Trying to attach missing LLPrimitive " + attach.ObjectData[i].ObjectLocalID);
                }
            }
        }

        void ObjectDropHandler(Packet packet, LLAgent agent)
        {
            ObjectDropPacket drop = (ObjectDropPacket)packet;

            for (int i = 0; i < drop.ObjectData.Length; i++)
            {
                ISceneEntity entity;
                if (m_scene.TryGetEntity(drop.ObjectData[i].ObjectLocalID, out entity) && entity is LLPrimitive)
                {
                    LLPrimitive obj = (LLPrimitive)entity;

                    // FIXME: Implement this
                    m_log.Error("Implement object dropping");

                    if (m_lslScriptEngine != null)
                    {
                        m_lslScriptEngine.PostObjectEvent(entity.ID, "attach", new object[] { UUID.Zero.ToString() }, new DetectParams[0]);
                    }
                }
                else
                {
                    m_log.Warn("Trying to drop missing LLPrimitive " + drop.ObjectData[i].ObjectLocalID);
                }
            }
        }

        void ObjectDuplicateHandler(Packet packet, LLAgent agent)
        {
            ObjectDuplicatePacket duplicate = (ObjectDuplicatePacket)packet;

            PrimFlags flags = (PrimFlags)duplicate.SharedData.DuplicateFlags;
            Vector3 offset = duplicate.SharedData.Offset;
            List<LLPrimitive> duplicates = new List<LLPrimitive>(duplicate.ObjectData.Length);

            int count = 0;

            // Build the list of prims to duplicate
            for (int i = 0; i < duplicate.ObjectData.Length; i++)
            {
                uint dupeID = duplicate.ObjectData[i].ObjectLocalID;

                ISceneEntity entity;
                if (m_scene.TryGetEntity(dupeID, out entity) && entity is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)entity;
                    PermissionMask perms = m_permissions.GetPrimPermissions(agent, prim);

                    if (!perms.HasPermission(PermissionMask.Copy))
                    {
                        m_scene.PresenceAlert(this, agent, "You do not have permission to copy one " +
                            "or more selected objects");
                        return;
                    }

                    ILinkable[] children = prim.GetChildren();
                    count += 1 + children.Length;
                    duplicates.Add(prim);
                }
                else
                {
                    m_log.Warn("ObjectDuplicate sent for missing prim " + dupeID);
                    SendSingleKillPacket(agent, dupeID);
                }
            }

            if (duplicates.Count == 0)
                return;

            // NOTE: Right now we only permissions check on the first duplicated item, but take into 
            // account the total number of prims that will be duplicated. This should handle most 
            // cases, but may not be correct as compared to other LLUDP implementations
            Vector3 targetPosition = duplicates[0].ScenePosition + offset;
            if (!CanAddPrims(agent, targetPosition, count))
                return;

            for (int i = 0; i < duplicates.Count; i++)
            {
                LLPrimitive prim = duplicates[i];

                Primitive obj = new Primitive(prim.Prim);
                obj.LocalID = 0;
                obj.ID = UUID.Zero;

                LLPrimitive newRoot = new LLPrimitive(obj, m_scene, m_primMesher);

                newRoot.RelativePosition += offset;
                newRoot.Prim.Properties.CreationDate = DateTime.UtcNow;

                m_scene.EntityAddOrUpdate(this, newRoot, UpdateFlags.FullUpdate, 0);

                // Duplicate any child prims
                ILinkable[] children = prim.GetChildren();
                for (int p = 0, len = children.Length; p < len; p++)
                {
                    if (children[p] is LLPrimitive)
                    {
                        LLPrimitive child = (LLPrimitive)children[p];

                        Primitive childPrim = new Primitive(child.Prim);
                        childPrim.LocalID = 0;
                        childPrim.ID = UUID.Zero;

                        LLPrimitive newChild = new LLPrimitive(childPrim, m_scene, m_primMesher);
                        newChild.SetParent(newRoot, true, false);

                        m_scene.EntityAddOrUpdate(this, newChild, UpdateFlags.FullUpdate, 0);
                    }
                }
            }
        }

        void ObjectNameHandler(Packet packet, LLAgent agent)
        {
            ObjectNamePacket name = (ObjectNamePacket)packet;

            List<uint> localIDs = new List<uint>(name.ObjectData.Length);
            for (int i = 0; i < name.ObjectData.Length; i++)
                localIDs.Add(name.ObjectData[i].LocalID);

            List<LLPrimitive> prims;
            if (!CanModify(agent, localIDs, out prims))
                return;

            for (int i = 0; i < prims.Count; i++)
            {
                LLPrimitive prim = prims[i];

                prim.Name = Utils.BytesToString(name.ObjectData[i].Name);

                // Send an ObjectPropertiesReply to with the new name
                ObjectPropertiesPacket props = new ObjectPropertiesPacket();
                props.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
                props.ObjectData[0] = LLUtil.BuildEntityPropertiesBlock(prim);
                m_udp.SendPacket(agent, props, ThrottleCategory.Task, false);

                // Signal this entity for serialization
                m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.Serialize, 0);
            }
        }

        void ObjectSelectHandler(Packet packet, LLAgent agent)
        {
            ObjectSelectPacket select = (ObjectSelectPacket)packet;

            for (int i = 0; i < select.ObjectData.Length; i++)
            {
                ISceneEntity entity;
                if (m_scene.TryGetEntity(select.ObjectData[i].ObjectLocalID, out entity))
                {
                    if (entity is IPhysical)
                        ((IPhysical)entity).Frozen = true;

                    ObjectPropertiesPacket properties = new ObjectPropertiesPacket();
                    properties.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
                    properties.ObjectData[0] = LLUtil.BuildEntityPropertiesBlock(entity);
                    m_udp.SendPacket(agent, properties, ThrottleCategory.Task, false);
                }
                else
                {
                    m_log.Warn("ObjectSelect sent for missing object " + select.ObjectData[i].ObjectLocalID);
                    SendSingleKillPacket(agent, select.ObjectData[i].ObjectLocalID);
                }
            }
        }

        void ObjectDeselectHandler(Packet packet, LLAgent agent)
        {
            ObjectDeselectPacket deselect = (ObjectDeselectPacket)packet;

            for (int i = 0; i < deselect.ObjectData.Length; i++)
            {
                uint localID = deselect.ObjectData[i].ObjectLocalID;

                ISceneEntity entity;
                if (m_scene.TryGetEntity(localID, out entity))
                {
                    if (entity is IPhysical)
                    {
                        IPhysical physical = (IPhysical)entity;
                        physical.Frozen = false;
                        physical.FallStart = Environment.TickCount;
                    }

                    //m_log.Debug("Deselecting object " + obj.Prim.LocalID);
                }
            }

            // TODO: Do we need this at all?
        }

        void ObjectGrabHandler(Packet packet, LLAgent agent)
        {
            ObjectGrabPacket grab = (ObjectGrabPacket)packet;

            ISceneEntity entity;
            if (m_scene.TryGetEntity(grab.ObjectData.LocalID, out entity))
            {
                List<DetectParams> touches = new List<DetectParams>(grab.SurfaceInfo.Length);
                bool held = false;

                for (int i = 0; i < grab.SurfaceInfo.Length; i++)
                {
                    ObjectGrabPacket.SurfaceInfoBlock block = grab.SurfaceInfo[i];

                    DetectParams touch = new DetectParams();
                    touch.Group = entity.GroupID;
                    touch.Key = entity.ID;
                    if (entity is ILinkable) touch.LinkNum = ((ILinkable)entity).LinkNumber;
                    touch.Name = entity.Name;
                    //touch.Offset = Vector3.Zero;
                    touch.Owner = entity.OwnerID;
                    touch.Position = entity.RelativePosition;
                    touch.Rotation = entity.RelativeRotation;
                    touch.TouchBinormal = block.Binormal;
                    touch.TouchFace = block.FaceIndex;
                    touch.TouchNormal = block.Normal;
                    touch.TouchPos = block.Position;
                    touch.TouchST = block.STCoord;
                    touch.TouchUV = block.UVCoord;
                    if (entity is IPhysical) touch.Velocity = ((IPhysical)entity).Velocity;

                    // Set the active or passive flag based on the entity velocity
                    if (touch.Velocity != Vector3.Zero)
                    {
                        held = true;
                        touch.Type |= ACTIVE;
                    }
                    else
                    {
                        touch.Type |= PASSIVE;
                    }

                    // Is this an agent?
                    if (entity is IScenePresence)
                        touch.Type |= AGENT;

                    // Is this entity scripted?
                    if (entity is LLPrimitive)
                    {
                        LLPrimitive prim = (LLPrimitive)entity;
                        if (prim.Prim.Flags.HasFlag(PrimFlags.Scripted))
                            touch.Type |= SCRIPTED;
                    }

                    touches.Add(touch);
                }

                if (!held && m_lslScriptEngine != null)
                {
                    m_lslScriptEngine.PostObjectEvent(entity.ID, "touch_start", new object[] { 1 }, touches.ToArray());
                }
            }
        }

        void ObjectGrabUpdateHandler(Packet packet, LLAgent agent)
        {
            ObjectGrabUpdatePacket update = (ObjectGrabUpdatePacket)packet;

            ISceneEntity entity;
            if (m_scene.TryGetEntity(update.ObjectData.ObjectID, out entity))
            {
                List<DetectParams> touches = new List<DetectParams>(update.SurfaceInfo.Length);
                for (int i = 0; i < update.SurfaceInfo.Length; i++)
                {
                    ObjectGrabUpdatePacket.SurfaceInfoBlock block = update.SurfaceInfo[i];

                    DetectParams touch = new DetectParams();
                    touch.Group = entity.GroupID;
                    touch.Key = entity.ID;
                    if (entity is ILinkable) touch.LinkNum = ((ILinkable)entity).LinkNumber;
                    touch.Name = entity.Name;
                    touch.Offset = update.ObjectData.GrabOffsetInitial;
                    touch.Owner = entity.OwnerID;
                    touch.Position = entity.RelativePosition;
                    touch.Rotation = entity.RelativeRotation;
                    touch.TouchBinormal = block.Binormal;
                    touch.TouchFace = block.FaceIndex;
                    touch.TouchNormal = block.Normal;
                    touch.TouchPos = block.Position;
                    touch.TouchST = block.STCoord;
                    touch.TouchUV = block.UVCoord;
                    if (entity is IPhysical) touch.Velocity = ((IPhysical)entity).Velocity;

                    // Set the active or passive flag based on the entity velocity
                    if (touch.Velocity != Vector3.Zero)
                        touch.Type |= ACTIVE;
                    else
                        touch.Type |= PASSIVE;
                    // Is this an agent?
                    if (entity is IScenePresence)
                        touch.Type |= AGENT;
                    // Is this entity scripted?
                    if (entity is LLPrimitive && (((LLPrimitive)entity).Prim.Flags & PrimFlags.Scripted) == PrimFlags.Scripted)
                        touch.Type |= SCRIPTED;

                    touches.Add(touch);
                }

                if (m_lslScriptEngine != null)
                {
                    m_lslScriptEngine.PostObjectEvent(entity.ID, "touch", new object[] { 1 }, touches.ToArray());
                }
            }
        }

        void ObjectDeGrabHandler(Packet packet, LLAgent agent)
        {
            ObjectDeGrabPacket degrab = (ObjectDeGrabPacket)packet;

            ISceneEntity entity;
            if (m_scene.TryGetEntity(degrab.ObjectData.LocalID, out entity))
            {
                if (m_lslScriptEngine != null)
                {
                    //TODO: change 1 to real count
                    List<DetectParams> touches = new List<DetectParams>(1);
                    DetectParams detect = new DetectParams();
                    detect.Name = agent.Name;
                    detect.Key = agent.ID;
                    detect.Owner = agent.OwnerID;
                    detect.Group = agent.GroupID;
                    touches.Add(detect);

                    m_lslScriptEngine.PostObjectEvent(entity.ID, "touch_end", new object[] { 1 }, touches.ToArray());
                }
            }
        }

        void ObjectLinkHandler(Packet packet, LLAgent agent)
        {
            ObjectLinkPacket link = (ObjectLinkPacket)packet;

            List<ILinkable> linkParents = new List<ILinkable>();
            for (int i = 0; i < link.ObjectData.Length; i++)
            {
                ISceneEntity entity;
                if (!m_scene.TryGetEntity(link.ObjectData[i].ObjectLocalID, out entity) || !(entity is ILinkable))
                {
                    m_log.Warn(agent.Name + " tried to link unknown object " + link.ObjectData[i].ObjectLocalID);
                    return;
                }
                
                // Owner check
                if (entity.OwnerID != agent.ID)
                {
                    m_scene.PresenceAlert(this, agent, "Unable to link because not all of the objects have the same owner. " +
                        "Please make sure you own all of the selected objects.");
                    return;
                }

                // Permissions check
                if (entity is LLPrimitive && !m_permissions.GetPrimPermissions(agent, (LLPrimitive)entity).HasPermission(PermissionMask.Modify))
                {
                    m_scene.PresenceAlert(this, agent, "Unable to link because you do not have permission to modify all of the objects. " +
                        "Please make sure you have modify rights for all of the selected objects.");
                    return;
                }

                linkParents.Add((ILinkable)entity);
            }

            // Set the link number and parent for each object
            linkParents[0].LinkNumber = 1;
            for (int i = 1; i < linkParents.Count; i++)
            {
                linkParents[i].LinkNumber = i + 1;
                linkParents[i].SetParent(linkParents[0], true, true);
            }
        }

        void ObjectDelinkHandler(Packet packet, LLAgent agent)
        {
            ObjectDelinkPacket delink = (ObjectDelinkPacket)packet;

            List<ILinkable> children = new List<ILinkable>();
            List<ILinkable> parents = new List<ILinkable>();

            for (int i = 0, len = delink.ObjectData.Length; i < len; i++)
            {
                ISceneEntity entity;
                if (!m_scene.TryGetEntity(delink.ObjectData[i].ObjectLocalID, out entity))
                {
                    //TODO: Send an error message
                    return;
                }
                else if (entity.OwnerID != agent.ID)
                {
                    //TODO: Do a full permissions check
                    return;
                }
                else
                {
                    if (entity is ILinkable)
                    {
                        ILinkable linkable = (ILinkable)entity;

                        if (linkable.Parent == null)
                            parents.Add(linkable);
                        else
                            children.Add(linkable);
                    }
                }
            }

            for (int i = 0; i < children.Count; i++)
                children[i].SetParent(null, true, true);

            for (int i = 0; i < parents.Count; i++)
            {
                ILinkable[] orphans = parents[i].GetChildren();
                if (orphans.Length > 0)
                {
                    ILinkable newRoot = orphans[0];

                    newRoot.SetParent(null, true, true);

                    for (int o = 1; o < orphans.Length; o++)
                    {
                        orphans[o].SetParent(null, true, false);
                        orphans[o].SetParent(newRoot, true, true);
                    }
                }
            }

            if (m_lslScriptEngine != null)
            {
                List<ILinkable> changed = new List<ILinkable>();
                changed.AddRange(children);
                changed.AddRange(parents);

                for (int i = 0; i < changed.Count; i++)
                    m_lslScriptEngine.PostObjectEvent(changed[i].ID, "changed", new object[] { CHANGED_LINK }, new DetectParams[0]);
            }
        }

        void ObjectShapeHandler(Packet packet, LLAgent agent)
        {
            ObjectShapePacket shape = (ObjectShapePacket)packet;

            List<uint> localIDs = new List<uint>(shape.ObjectData.Length);
            for (int i = 0; i < shape.ObjectData.Length; i++)
                localIDs.Add(shape.ObjectData[i].ObjectLocalID);

            List<LLPrimitive> prims;
            if (!CanModify(agent, localIDs, out prims))
                return;

            for (int i = 0; i < prims.Count; i++)
            {
                LLPrimitive obj = prims[i];
                ObjectShapePacket.ObjectDataBlock block = shape.ObjectData[i];

                obj.SaveUndoStep();

                Primitive.ConstructionData data = obj.Prim.PrimData;

                data.PathBegin = Primitive.UnpackBeginCut(block.PathBegin);
                data.PathCurve = (PathCurve)block.PathCurve;
                data.PathEnd = Primitive.UnpackEndCut(block.PathEnd);
                data.PathRadiusOffset = Primitive.UnpackPathTwist(block.PathRadiusOffset);
                data.PathRevolutions = Primitive.UnpackPathRevolutions(block.PathRevolutions);
                data.PathScaleX = Primitive.UnpackPathScale(block.PathScaleX);
                data.PathScaleY = Primitive.UnpackPathScale(block.PathScaleY);
                data.PathShearX = Primitive.UnpackPathShear((sbyte)block.PathShearX);
                data.PathShearY = Primitive.UnpackPathShear((sbyte)block.PathShearY);
                data.PathSkew = Primitive.UnpackPathTwist(block.PathSkew);
                data.PathTaperX = Primitive.UnpackPathTaper(block.PathTaperX);
                data.PathTaperY = Primitive.UnpackPathTaper(block.PathTaperY);
                data.PathTwist = Primitive.UnpackPathTwist(block.PathTwist);
                data.PathTwistBegin = Primitive.UnpackPathTwist(block.PathTwistBegin);
                data.ProfileBegin = Primitive.UnpackBeginCut(block.ProfileBegin);
                data.profileCurve = block.ProfileCurve;
                data.ProfileEnd = Primitive.UnpackEndCut(block.ProfileEnd);
                data.ProfileHollow = Primitive.UnpackProfileHollow(block.ProfileHollow);

                obj.Prim.PrimData = data;
                m_scene.EntityAddOrUpdate(this, obj, UpdateFlags.Shape, 0);

                if (m_lslScriptEngine != null)
                    m_lslScriptEngine.PostObjectEvent(obj.ID, "changed", new object[] { CHANGED_SHAPE }, new DetectParams[0]);
            }
        }

        void ObjectFlagUpdateHandler(Packet packet, LLAgent agent)
        {
            ObjectFlagUpdatePacket update = (ObjectFlagUpdatePacket)packet;

            List<uint> localIDs = new List<uint> { update.AgentData.ObjectLocalID };

            List<LLPrimitive> prims;
            if (!CanModify(agent, localIDs, out prims) || prims.Count < 1)
                return;

            LLPrimitive prim = prims[0];
            PrimFlags flags = prim.Prim.Flags;
            UpdateFlags updateFlags = 0;

            prim.SaveUndoStep();

            if (update.AgentData.CastsShadows)
                flags |= PrimFlags.CastShadows;
            else
                flags &= ~PrimFlags.CastShadows;

            if (update.AgentData.IsTemporary)
                flags |= PrimFlags.Temporary;
            else
                flags &= ~PrimFlags.Temporary;

            if (update.AgentData.IsPhantom && (flags & PrimFlags.Phantom) == 0)
            {
                flags |= PrimFlags.Phantom;
                updateFlags |= UpdateFlags.PhantomStatus;
            }
            else if (!update.AgentData.IsPhantom && (flags & PrimFlags.Phantom) == PrimFlags.Phantom)
            {
                flags &= ~PrimFlags.Phantom;
                updateFlags |= UpdateFlags.PhantomStatus;
            }

            if (update.AgentData.UsePhysics && (flags & PrimFlags.Physics) == 0)
            {
                flags |= PrimFlags.Physics;
                updateFlags |= UpdateFlags.PhysicalStatus;
            }
            else if (!update.AgentData.UsePhysics && (flags & PrimFlags.Physics) == PrimFlags.Physics)
            {
                flags &= ~PrimFlags.Physics;
                updateFlags |= UpdateFlags.PhysicalStatus;
            }

            prim.Prim.Flags = flags;
            m_scene.EntityAddOrUpdate(this, prim, updateFlags, (uint)LLUpdateFlags.PrimFlags);
        }

        void ObjectExtraParamsHandler(Packet packet, LLAgent agent)
        {
            ObjectExtraParamsPacket extra = (ObjectExtraParamsPacket)packet;

            List<uint> localIDs = new List<uint>(extra.ObjectData.Length);
            for (int i = 0; i < extra.ObjectData.Length; i++)
                localIDs.Add(extra.ObjectData[i].ObjectLocalID);

            List<LLPrimitive> prims;
            if (!CanModify(agent, localIDs, out prims))
                return;

            for (int i = 0; i < prims.Count; i++)
            {
                LLPrimitive prim = prims[i];
                ObjectExtraParamsPacket.ObjectDataBlock block = extra.ObjectData[i];
                ExtraParamType type = (ExtraParamType)block.ParamType;

                prim.SaveUndoStep();

                if (block.ParamInUse)
                {
                    switch (type)
                    {
                        case ExtraParamType.Flexible:
                            prim.Prim.Flexible = new Primitive.FlexibleData(block.ParamData, 0);
                            break;
                        case ExtraParamType.Light:
                            prim.Prim.Light = new Primitive.LightData(block.ParamData, 0);
                            break;
                        case ExtraParamType.Sculpt:
                            prim.Prim.Sculpt = new Primitive.SculptData(block.ParamData, 0);
                            break;
                    }
                }
                else
                {
                    switch (type)
                    {
                        case ExtraParamType.Flexible:
                            prim.Prim.Flexible = null;
                            break;
                        case ExtraParamType.Light:
                            prim.Prim.Light = null;
                            break;
                        case ExtraParamType.Sculpt:
                            prim.Prim.Sculpt = null;
                            break;
                    }
                }

                m_scene.EntityAddOrUpdate(this, prim, 0, (uint)LLUpdateFlags.ExtraData);
            }
        }

        void ObjectImageHandler(Packet packet, LLAgent agent)
        {
            ObjectImagePacket image = (ObjectImagePacket)packet;

            List<uint> localIDs = new List<uint>(image.ObjectData.Length);
            for (int i = 0; i < image.ObjectData.Length; i++)
                localIDs.Add(image.ObjectData[i].ObjectLocalID);

            List<LLPrimitive> prims;
            if (!CanModify(agent, localIDs, out prims))
                return;

            for (int i = 0; i < prims.Count; i++)
            {
                LLPrimitive prim = prims[i];
                PermissionMask perms = m_permissions.GetPrimPermissions(agent, prim);

                prim.SaveUndoStep();

                prim.Prim.MediaURL = Utils.BytesToString(image.ObjectData[i].MediaURL);
                prim.Prim.Textures = new Primitive.TextureEntry(image.ObjectData[i].TextureEntry, 0, image.ObjectData[i].TextureEntry.Length);
                m_scene.EntityAddOrUpdate(this, prim, 0, (uint)(LLUpdateFlags.MediaURL | LLUpdateFlags.Textures));

                // TODO: Do a before and after comparison to test if the texture actually changed, and if the color changed
                if (m_lslScriptEngine != null)
                    m_lslScriptEngine.PostObjectEvent(prim.ID, "changed", new object[] { CHANGED_TEXTURE }, new DetectParams[0]);
            }
        }

        void ObjectPermissionsHandler(Packet packet, LLAgent agent)
        {
            ObjectPermissionsPacket permissions = (ObjectPermissionsPacket)packet;

            for (int i = 0; i < permissions.ObjectData.Length; i++)
            {
                ISceneEntity obj;
                if (m_scene.TryGetEntity(permissions.ObjectData[i].ObjectLocalID, out obj) && obj is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)obj;
                    PermissionMask ownerPerms = m_permissions.GetPrimPermissions(agent, prim);

                    bool set = (permissions.ObjectData[i].Set != 0) ? true : false;
                    PermissionWho field = (PermissionWho)permissions.ObjectData[i].Field;
                    PermissionMask newPerms = (PermissionMask)permissions.ObjectData[i].Mask;

                    if (!set || ownerPerms.HasPermission(newPerms))
                    {
                        switch (field)
                        {
                            case PermissionWho.Group:
                                if (set)
                                    prim.Prim.Properties.Permissions.GroupMask |= newPerms;
                                else
                                    prim.Prim.Properties.Permissions.GroupMask &= ~newPerms;
                                break;
                            case PermissionWho.Everyone:
                                if (set)
                                    prim.Prim.Properties.Permissions.EveryoneMask |= newPerms;
                                else
                                    prim.Prim.Properties.Permissions.EveryoneMask &= ~newPerms;
                                break;
                            case PermissionWho.NextOwner:
                                if (set)
                                    prim.Prim.Properties.Permissions.NextOwnerMask |= newPerms;
                                else
                                    prim.Prim.Properties.Permissions.NextOwnerMask &= ~newPerms;
                                break;
                        }
                    }
                    else
                    {
                        m_log.Warn(agent.Name + " tried to set " + field + " permissions \"" + newPerms + "\" on " + prim.ID +
                            " without having proper permission");
                    }

                    // If the next owner doesn't have copy permission force the transfer permission on
                    if (!prim.Prim.Properties.Permissions.NextOwnerMask.HasPermission(PermissionMask.Copy))
                        prim.Prim.Properties.Permissions.NextOwnerMask |= PermissionMask.Transfer;

                    SendPropertiesPacket(agent, prim, ReportType.None);
                }
            }
        }

        void UndoHandler(Packet packet, LLAgent agent)
        {
            UndoPacket undo = (UndoPacket)packet;
            bool success = true;

            for (int i = 0; i < undo.ObjectData.Length; i++)
            {
                ISceneEntity entity;
                if (m_scene.TryGetEntity(undo.ObjectData[i].ObjectID, out entity) && entity is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)entity;
                    PermissionMask perms = m_permissions.GetPrimPermissions(agent, prim);

                    if (perms.HasPermission(PermissionMask.Modify) && prim.Undo())
                        m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
                    else
                        success = false;
                }
            }

            if (!success)
                m_scene.PresenceAlert(this, agent, "You do not have permission to edit one or more of the selected objects");
        }

        void RedoHandler(Packet packet, LLAgent agent)
        {
            RedoPacket redo = (RedoPacket)packet;
            bool success = true;

            for (int i = 0; i < redo.ObjectData.Length; i++)
            {
                ISceneEntity entity;
                if (m_scene.TryGetEntity(redo.ObjectData[i].ObjectID, out entity) && entity is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)entity;
                    PermissionMask perms = m_permissions.GetPrimPermissions(agent, prim);

                    if (perms.HasPermission(PermissionMask.Modify) && prim.Redo())
                        m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
                    else
                        success = false;
                }
            }

            if (!success)
                m_scene.PresenceAlert(this, agent, "You do not have permission to edit one or more of the selected objects");
        }

        void MultipleObjectUpdateHandler(Packet packet, LLAgent agent)
        {
            MultipleObjectUpdatePacket update = (MultipleObjectUpdatePacket)packet;

            List<UUID> movedObjects = new List<UUID>();
            List<UUID> scaledObjects = new List<UUID>();

            for (int i = 0; i < update.ObjectData.Length; i++)
            {
                bool scaled = false;
                MultipleObjectUpdatePacket.ObjectDataBlock block = update.ObjectData[i];

                ISceneEntity obj;
                if (m_scene.TryGetEntity(block.ObjectLocalID, out obj))
                {
                    bool isOwner = (obj.OwnerID == agent.ID);
                    bool canMove = isOwner || (obj is LLPrimitive && (((LLPrimitive)obj).Prim.Flags & PrimFlags.ObjectMove) == PrimFlags.ObjectMove);

                    if (obj is LLPrimitive)
                    {
                        LLPrimitive prim = (LLPrimitive)obj;
                        prim.SaveUndoStep();
                    }

                    UpdateType type = (UpdateType)block.Type;
                    //bool linked = ((type & UpdateType.Linked) != 0);
                    int pos = 0;
                    Vector3 position = obj.RelativePosition;
                    Quaternion rotation = obj.RelativeRotation;
                    Vector3 scale = obj.Scale;

                    UpdateFlags updateFlags = 0;

                    if ((type & UpdateType.Position) != 0 && canMove)
                    {
                        updateFlags |= UpdateFlags.Position;
                        position = new Vector3(block.Data, pos);

                        // HACK: Show avatar move when seat is moved
                        m_scene.ForEachPresence(delegate(IScenePresence presence)
                        {
                            if (presence is ILinkable && ((ILinkable)presence).Parent == obj)
                                m_scene.EntityAddOrUpdate(this, presence, UpdateFlags.FullUpdate, 0);
                        });

                        pos += 12;

                        if (!movedObjects.Contains(obj.ID))
                            movedObjects.Add(obj.ID);
                    }
                    if ((type & UpdateType.Rotation) != 0 && canMove)
                    {
                        updateFlags |= UpdateFlags.Rotation;
                        rotation = new Quaternion(block.Data, pos, true);
                        pos += 12;

                        if (!movedObjects.Contains(obj.ID))
                            movedObjects.Add(obj.ID);
                    }
                    if ((type & UpdateType.Scale) != 0)
                    {
                        updateFlags |= UpdateFlags.Scale;
                        scaled = true;
                        scale = new Vector3(block.Data, pos);
                        pos += 12;

                        // FIXME: Use this in linksets
                        //bool uniform = ((type & UpdateType.Uniform) != 0);

                        if (!scaledObjects.Contains(obj.ID))
                            scaledObjects.Add(obj.ID);
                    }

                    obj.RelativePosition = position;
                    obj.RelativeRotation = rotation;

                    if (scaled)
                        obj.Scale = scale;

                    if (updateFlags != 0)
                    {
                        m_scene.EntityAddOrUpdate(this, obj, updateFlags, 0);
                    }
                    else
                    {
                        // Update failed (probably due to permissions), update client's state for 
                        // this entity
                        SendEntityTo(agent, obj, false);
                    }
                }
                else
                {
                    m_log.Warn("MultipleObjectUpdate received for missing entity " + block.ObjectLocalID);
                    SendSingleKillPacket(agent, block.ObjectLocalID);
                }
            }

            if (m_lslScriptEngine != null)
            {
                for (int i = 0, len = movedObjects.Count; i < len; i++)
                    m_lslScriptEngine.PostObjectEvent(movedObjects[i], "moving_end", new object[0], new DetectParams[0]);

                for (int i = 0, len = scaledObjects.Count; i < len; i++)
                    m_lslScriptEngine.PostObjectEvent(scaledObjects[i], "changed", new object[] { CHANGED_SCALE }, new DetectParams[0]);
            }
        }

        void RequestObjectPropertiesFamilyHandler(Packet packet, LLAgent agent)
        {
            RequestObjectPropertiesFamilyPacket request = (RequestObjectPropertiesFamilyPacket)packet;
            ReportType type = (ReportType)request.ObjectData.RequestFlags;

            ISceneEntity obj;
            LLPrimitive prim;
            if (m_scene.TryGetEntity(request.ObjectData.ObjectID, out obj) && obj is LLPrimitive)
            {
                prim = (LLPrimitive)obj;

                SendPropertiesPacket(agent, prim, type);
            }
            else
            {
                m_log.Warn("RequestObjectPropertiesFamily sent for unknown object " + request.ObjectData.ObjectID);
            }
        }

        void RequestMultipleObjectsHandler(Packet packet, LLAgent agent)
        {
            RequestMultipleObjectsPacket request = (RequestMultipleObjectsPacket)packet;
            int cacheMissFull = 0;
            int cacheMissCRC = 0;

            for (int i = 0; i < request.ObjectData.Length; i++)
            {
                RequestMultipleObjectsPacket.ObjectDataBlock block = request.ObjectData[i];
                CacheMissType missType = (CacheMissType)block.CacheMissType;

                if (missType == CacheMissType.Full)
                    ++cacheMissFull;
                else if (missType == CacheMissType.CRC)
                    ++cacheMissCRC;
                else
                    m_log.Warn("Unrecognized cache miss type " + missType);

                ISceneEntity entity;
                if (m_scene.TryGetEntity(block.ID, out entity))
                    SendEntityTo(agent, entity, false);
                else
                    m_log.Warn("Received a RequestMultipleObjects packet for unknown entity " + block.ID);
            }

            //m_log.Debug("Handling " + (cacheMissFull + cacheMissCRC) + " cache misses");

            if (m_statsTracker != null)
            {
                DateTime now = DateTime.UtcNow;

                if (cacheMissFull > 0)
                    m_statsTracker.LogEntry(now, agent.ID, "CacheMissFull", cacheMissFull);
                if (cacheMissCRC > 0)
                    m_statsTracker.LogEntry(now, agent.ID, "CacheMissCRC", cacheMissCRC);
            }
        }

        #endregion Client Packet Handling

        #region Event Handlers

        private void EntityAddOrUpdateHandler(object sender, EntityAddOrUpdateArgs e)
        {
            // Child agent updates are not sent out here
            if (e.Entity is LLAgent && ((LLAgent)e.Entity).IsChildPresence)
                return;

            // Ignore serialization-only signals
            if (e.UpdateFlags == UpdateFlags.Serialize)
                return;

            // Check for out of bounds objects
            const float LIMIT = 10000f;
            Vector3 pos = e.Entity.RelativePosition;
            if (pos.X < -LIMIT || pos.X > LIMIT ||
                pos.Y < -LIMIT || pos.Y > LIMIT ||
                pos.Z < -LIMIT || pos.Z > LIMIT)
            {
                // TODO: Return this object to the owner instead of destroying it
                m_log.Warn("Destroying out of bounds object " + e.Entity.ID + " at " + pos);
                m_scene.EntityRemove(this, e.Entity);
                return;
            }

            m_scene.CreateInterestListEvent(new InterestListEvent(e.Entity.ID, OBJECT_UPDATE, e.Entity.ScenePosition, e.Entity.Scale, e));
        }

        private void EntityRemoveHandler(object sender, EntityArgs e)
        {
            if (e.Entity is LLAgent && ((LLAgent)e.Entity).IsChildPresence)
                return;

            m_scene.CreateInterestListEvent(new InterestListEvent(e.Entity.ID, OBJECT_REMOVE, e.Entity.ScenePosition, e.Entity.Scale, e.Entity.LocalID));
        }

        private void PresenceAddHandler(object sender, PresenceArgs e)
        {
            IScenePresence newPresence = e.Presence;
            if (e.Presence is LLAgent)
            {
                LLAgent agent = (LLAgent)e.Presence;
                m_scene.ForEachEntity(
                    delegate(ISceneEntity entity)
                    {
                        if (entity.ID != agent.ID)
                            SendEntityTo(agent, entity, true);
                    }
                );
            }
        }

        private void PresenceRemoveHandler(object sender, PresenceArgs e)
        {
            // Mark the time this presence was last connected to the scene. This is used in the 
            // object update sending loop to determine if we should send a cache check or the full 
            // object update
            lock (m_recentAvatars)
                m_recentAvatars[e.Presence.ID] = DateTime.UtcNow;
        }

        private void EntityCollisionHandler(object sender, EntityCollisionArgs e)
        {
            // FIXME: Skipping this entirely until we can get some throttling in here
            // We have a ThrottledQueue class now, just need to use it
            return;

            //DetectParams collision = new DetectParams();
            //collision.Group = b.GroupID;
            //collision.Key = b.ID;
            //collision.Name = b.Name;
            //collision.Owner = b.OwnerID;
            //collision.Position = b.RelativePosition;
            //collision.Rotation = b.RelativeRotation;

            ////m_log.Debug(a.ID + " collided with " + b.ID);

            //m_lslScriptEngine.PostObjectEvent(a.ID, "collision", new object[] { 1 }, new DetectParams[] { collision });
        }

        #endregion Event Handlers

        #region Packet Sending Helpers

        private void SendEntityPackets(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            Lazy<List<ObjectUpdatePacket.ObjectDataBlock>> objectUpdateBlocks = new Lazy<List<ObjectUpdatePacket.ObjectDataBlock>>();
            Lazy<List<ObjectUpdateCompressedPacket.ObjectDataBlock>> compressedUpdateBlocks = new Lazy<List<ObjectUpdateCompressedPacket.ObjectDataBlock>>();
            Lazy<List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>> terseUpdateBlocks = new Lazy<List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>>();
            Lazy<List<ObjectUpdateCachedPacket.ObjectDataBlock>> cachedUpdateBlocks = new Lazy<List<ObjectUpdateCachedPacket.ObjectDataBlock>>();

            for (int i = 0; i < eventDatas.Length; i++)
            {
                EntityAddOrUpdateArgs e = (EntityAddOrUpdateArgs)eventDatas[i].Event.State;
                ISceneEntity entity = e.Entity;

                #region Determine packet type

                UpdateFlags updateFlags = e.UpdateFlags;
                LLUpdateFlags llUpdateFlags = (LLUpdateFlags)e.ExtraFlags;
                LLPrimitive prim = entity as LLPrimitive;

                bool canUseCached = false;
                bool canUseTerse = true;
                DateTime lastSeen;

                if (CACHE_CHECK_ENABLED &&
                    prim != null &&
                    updateFlags.HasFlag(UpdateFlags.FullUpdate) &&
                    !llUpdateFlags.HasFlag(LLUpdateFlags.NoCachedUpdate) &&
                    m_recentAvatars.TryGetValue(presence.ID, out lastSeen) &&
                    lastSeen > prim.LastUpdated)
                {
                    // This avatar was marked as leaving the same later than the last update 
                    // timestamp of this prim. Send a cache check
                    canUseCached = true;
                }
                else if (updateFlags.HasFlag(UpdateFlags.FullUpdate) ||
                    updateFlags.HasFlag(UpdateFlags.Parent) ||
                    updateFlags.HasFlag(UpdateFlags.Scale) ||
                    updateFlags.HasFlag(UpdateFlags.Shape) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.PrimFlags) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.Text) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.NameValue) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.ExtraData) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.TextureAnim) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.Sound) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.Particles) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.Material) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.ClickAction) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.MediaURL) ||
                    llUpdateFlags.HasFlag(LLUpdateFlags.Joint))
                {
                    canUseTerse = false;
                }

                #endregion Determine packet type

                #region Block Construction

                if (canUseCached && prim != null)
                {
                    cachedUpdateBlocks.Value.Add(new ObjectUpdateCachedPacket.ObjectDataBlock
                        { CRC = prim.GetCrc(), ID = prim.LocalID });
                }
                else if (!canUseTerse)
                {
                    if (entity is IScenePresence)
                    {
                        IScenePresence thisPresence = (IScenePresence)entity;
                        ObjectUpdatePacket.ObjectDataBlock block = CreateAvatarObjectUpdateBlock(thisPresence);
                        block.UpdateFlags = (uint)GetUpdateFlags(thisPresence, presence);
                        objectUpdateBlocks.Value.Add(block);
                    }
                    else if (prim != null)
                    {
                        ObjectUpdateCompressedPacket.ObjectDataBlock block = CreateCompressedObjectUpdateBlock(prim, prim.GetCrc());
                        block.UpdateFlags = (uint)GetUpdateFlags(prim, presence, m_permissions);
                        compressedUpdateBlocks.Value.Add(block);

                        // ObjectUpdateCompressed doesn't carry velocity or acceleration fields, so
                        // we need to send a separate terse packet if this prim has a non-zero 
                        // velocity or acceleration
                        if (prim.Velocity != Vector3.Zero || prim.Acceleration != Vector3.Zero)
                            terseUpdateBlocks.Value.Add(CreateTerseUpdateBlock(entity, false));

                        //ObjectUpdatePacket.ObjectDataBlock block = CreateObjectUpdateBlock(prim);
                        //block.UpdateFlags = (uint)GetUpdateFlags(prim, presence, m_permissions);
                        //block.CRC = prim.GetCrc();
                        //objectUpdateBlocks.Value.Add(block);
                    }
                    else
                    {
                        // TODO: Create a generic representation for non-LLPrimitive entities?
                        continue;
                    }
                }
                else
                {
                    terseUpdateBlocks.Value.Add(CreateTerseUpdateBlock(entity, llUpdateFlags.HasFlag(LLUpdateFlags.Textures)));
                }

                #endregion Block Construction

                // Unset CreateSelected after it has been sent once
                if (prim != null)
                    prim.Prim.Flags &= ~PrimFlags.CreateSelected;
            }

            #region Packet Sending

            ushort timeDilation = (m_physics != null) ?
                Utils.FloatToUInt16(m_physics.TimeDilation, 0.0f, 1.0f) :
                UInt16.MaxValue;

            if (objectUpdateBlocks.IsValueCreated)
            {
                List<ObjectUpdatePacket.ObjectDataBlock> blocks = objectUpdateBlocks.Value;

                ObjectUpdatePacket packet = new ObjectUpdatePacket();
                packet.RegionData.RegionHandle = Util.PositionToRegionHandle(m_scene.MinPosition);
                packet.RegionData.TimeDilation = timeDilation;
                packet.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[blocks.Count];

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData[i] = blocks[i];

                m_udp.SendPacket(agent, packet, ThrottleCategory.Task, true);
            }

            if (compressedUpdateBlocks.IsValueCreated)
            {
                List<ObjectUpdateCompressedPacket.ObjectDataBlock> blocks = compressedUpdateBlocks.Value;

                ObjectUpdateCompressedPacket packet = new ObjectUpdateCompressedPacket();
                packet.RegionData.RegionHandle = Util.PositionToRegionHandle(m_scene.MinPosition);
                packet.RegionData.TimeDilation = timeDilation;
                packet.ObjectData = new ObjectUpdateCompressedPacket.ObjectDataBlock[blocks.Count];

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData[i] = blocks[i];

                m_udp.SendPacket(agent, packet, ThrottleCategory.Task, true);
            }

            if (terseUpdateBlocks.IsValueCreated)
            {
                List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock> blocks = terseUpdateBlocks.Value;

                ImprovedTerseObjectUpdatePacket packet = new ImprovedTerseObjectUpdatePacket();
                packet.RegionData.RegionHandle = Util.PositionToRegionHandle(m_scene.MinPosition);
                packet.RegionData.TimeDilation = timeDilation;
                packet.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[blocks.Count];

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData[i] = blocks[i];

                m_udp.SendPacket(agent, packet, ThrottleCategory.Task, true);
            }

            if (cachedUpdateBlocks.IsValueCreated)
            {
                List<ObjectUpdateCachedPacket.ObjectDataBlock> blocks = cachedUpdateBlocks.Value;

                ObjectUpdateCachedPacket packet = new ObjectUpdateCachedPacket();
                packet.RegionData.RegionHandle = Util.PositionToRegionHandle(m_scene.MinPosition);
                packet.RegionData.TimeDilation = timeDilation;
                packet.ObjectData = new ObjectUpdateCachedPacket.ObjectDataBlock[blocks.Count];

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData[i] = blocks[i];

                m_udp.SendPacket(agent, packet, ThrottleCategory.Task, true);
            }

            #endregion Packet Sending
        }

        private void SendPropertiesPacket(LLAgent agent, LLPrimitive prim, ReportType type)
        {
            ObjectPropertiesFamilyPacket props = new ObjectPropertiesFamilyPacket();
            props.ObjectData.BaseMask = (uint)prim.Prim.Properties.Permissions.BaseMask;
            props.ObjectData.Category = (uint)prim.Prim.Properties.Category;
            props.ObjectData.Description = Utils.StringToBytes(prim.Prim.Properties.Description);
            props.ObjectData.EveryoneMask = (uint)prim.Prim.Properties.Permissions.EveryoneMask;
            props.ObjectData.GroupID = prim.Prim.Properties.GroupID;
            props.ObjectData.GroupMask = (uint)prim.Prim.Properties.Permissions.GroupMask;
            props.ObjectData.LastOwnerID = prim.Prim.Properties.LastOwnerID;
            props.ObjectData.Name = Utils.StringToBytes(prim.Prim.Properties.Name);
            props.ObjectData.NextOwnerMask = (uint)prim.Prim.Properties.Permissions.NextOwnerMask;
            props.ObjectData.ObjectID = prim.Prim.ID;
            props.ObjectData.OwnerID = prim.Prim.Properties.OwnerID;
            props.ObjectData.OwnerMask = (uint)prim.Prim.Properties.Permissions.OwnerMask;
            props.ObjectData.OwnershipCost = prim.Prim.Properties.OwnershipCost;
            props.ObjectData.RequestFlags = (uint)type;
            props.ObjectData.SalePrice = prim.Prim.Properties.SalePrice;
            props.ObjectData.SaleType = (byte)prim.Prim.Properties.SaleType;

            m_udp.SendPacket(agent, props, ThrottleCategory.Task, false);
        }

        private void SendKillPacket(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            if (!(presence is LLAgent) || presence.InterestList == null)
                return;
            LLAgent agent = (LLAgent)presence;

            KillObjectPacket kill = new KillObjectPacket();
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[eventDatas.Length];

            for (int i = 0; i < eventDatas.Length; i++)
            {
                kill.ObjectData[i] = new KillObjectPacket.ObjectDataBlock();
                kill.ObjectData[i].ID = (uint)eventDatas[i].Event.State;
            }

            m_udp.SendPacket(agent, kill, ThrottleCategory.Task, true);
        }

        private void SendSingleKillPacket(LLAgent agent, uint localID)
        {
            KillObjectPacket kill = new KillObjectPacket();
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            kill.ObjectData[0].ID = localID;

            m_udp.SendPacket(agent, kill, ThrottleCategory.Task, true);
        }

        private void SendEntityTo(LLAgent agent, ISceneEntity entity, bool allowCachedUpdate)
        {
            uint extraFlags = (allowCachedUpdate) ? 0 : (uint)LLUpdateFlags.NoCachedUpdate;

            m_scene.CreateInterestListEventFor(agent,
                new InterestListEvent
                (
                    entity.ID,
                    OBJECT_UPDATE,
                    entity.ScenePosition,
                    entity.Scale,
                    new EntityAddOrUpdateArgs { Entity = entity, UpdateFlags = UpdateFlags.FullUpdate, ExtraFlags = extraFlags, IsNew = false }
                )
            );
        }

        #endregion Packet Sending Helpers

        #region Object Update Creation

        public ObjectUpdatePacket.ObjectDataBlock CreateAvatarObjectUpdateBlock(IScenePresence presence)
        {
            byte[] objectData = new byte[76];

            Vector4 collisionPlane = (presence is IPhysicalPresence) ?
                ((IPhysicalPresence)presence).CollisionPlane :
                Vector4.UnitW;
            if (collisionPlane == Vector4.Zero)
                collisionPlane = Vector4.UnitW;

            if (presence.RelativeRotation == INVALID_ROT)
            {
                m_log.Warn("Correcting an invalid rotation for " + presence.Name + " (" + presence.ID + ")");
                presence.RelativeRotation = Quaternion.Identity;
            }

            collisionPlane.ToBytes(objectData, 0);
            presence.RelativePosition.ToBytes(objectData, 16);
            //data.Velocity.ToBytes(objectData, 28);
            //data.Acceleration.ToBytes(objectData, 40);
            presence.RelativeRotation.ToBytes(objectData, 52);
            //data.AngularVelocity.ToBytes(objectData, 64);

            string firstName, lastName;
            Util.GetFirstLastName(presence.Name, out firstName, out lastName);

            ObjectUpdatePacket.ObjectDataBlock update = new ObjectUpdatePacket.ObjectDataBlock();
            update.Data = Utils.EmptyBytes;
            update.ExtraParams = new byte[1];
            update.FullID = presence.ID;
            update.ID = presence.LocalID;
            update.Material = (byte)Material.Flesh;
            update.MediaURL = Utils.EmptyBytes;
            update.NameValue = Utils.StringToBytes("FirstName STRING RW SV " + firstName + "\nLastName STRING RW SV " +
                lastName + "\nTitle STRING RW SV " + String.Empty); // TODO: Support group title
            update.ObjectData = objectData;

            if (presence is ILinkable)
            {
                ILinkable linkable = (ILinkable)presence;
                if (linkable.Parent != null)
                    update.ParentID = linkable.Parent.LocalID;
            }

            update.PathCurve = 16;
            update.PathScaleX = 100;
            update.PathScaleY = 100;
            update.PCode = (byte)PCode.Avatar;
            update.ProfileCurve = 1;
            update.PSBlock = Utils.EmptyBytes;
            update.Scale = presence.Scale;
            update.Text = Utils.EmptyBytes;
            update.TextColor = new byte[4];
            update.TextureAnim = Utils.EmptyBytes;
            update.TextureEntry = Utils.EmptyBytes; // TODO: TextureEntry support

            return update;
        }

        public static ObjectUpdateCompressedPacket.ObjectDataBlock CreateCompressedObjectUpdateBlock(LLPrimitive entity, uint crc)
        {
            Primitive prim = entity.Prim;

            #region Size calculation and field serialization

            CompressedFlags flags = 0;
            int size = 84;
            byte[] textBytes = null;
            byte[] mediaURLBytes = null;
            byte[] particleBytes = null;
            byte[] extraParamBytes = null;
            byte[] nameValueBytes = null;
            byte[] textureBytes = null;
            byte[] textureAnimBytes = null;

            flags |= CompressedFlags.HasAngularVelocity;
            size += 12;

            flags |= CompressedFlags.HasParent;
            size += 4;

            switch (prim.PrimData.PCode)
            {
                case PCode.Grass:
                case PCode.Tree:
                case PCode.NewTree:
                    flags |= CompressedFlags.Tree;
                    size += 2; // Size byte plus one byte
                    break;
                //default:
                //    flags |= CompressedFlags.ScratchPad;
                //    size += 1 + prim.ScratchPad.Length; // Size byte plus length
                //    break;
            }

            flags |= CompressedFlags.HasText;
            textBytes = StringToBytesNullTerminated(prim.Text);
            size += textBytes.Length; // Null-terminated, no size byte
            size += 4; // Text color

            flags |= CompressedFlags.MediaURL;
            mediaURLBytes = StringToBytesNullTerminated(prim.MediaURL);
            size += mediaURLBytes.Length; // Null-terminated, no size byte

            if (prim.ParticleSys.BurstPartCount > 0)
            {
                flags |= CompressedFlags.HasParticles;
                particleBytes = prim.ParticleSys.GetBytes();
                size += particleBytes.Length; // Should be exactly 86 bytes
            }

            // Extra Params
            extraParamBytes = prim.GetExtraParamsBytes();
            size += extraParamBytes.Length;

            if (prim.Sound != UUID.Zero)
            {
                flags |= CompressedFlags.HasSound;
                size += 25; // SoundID, SoundGain, SoundFlags, SoundRadius
            }

            if (prim.NameValues != null && prim.NameValues.Length > 0)
            {
                flags |= CompressedFlags.HasNameValues;
                nameValueBytes = StringToBytesNullTerminated(NameValue.NameValuesToString(prim.NameValues));
                size += nameValueBytes.Length;
            }

            size += 23; // PrimData
            size += 4; // Texture Length
            textureBytes = prim.Textures.GetBytes();
            size += textureBytes.Length; // Texture Entry

            flags |= CompressedFlags.TextureAnimation;
            size += 4; // TextureAnim Length
            textureAnimBytes = prim.TextureAnim.GetBytes();
            size += textureAnimBytes.Length; // TextureAnim

            #endregion Size calculation and field serialization

            #region Packet serialization

            int pos = 0;
            byte[] data = new byte[size];

            prim.ID.ToBytes(data, 0); // UUID
            pos += 16;
            Utils.UIntToBytes(prim.LocalID, data, pos); // LocalID
            pos += 4;
            data[pos++] = (byte)prim.PrimData.PCode; // PCode
            data[pos++] = prim.PrimData.State; // State
            Utils.UIntToBytes(crc, data, pos); // CRC
            pos += 4;
            data[pos++] = (byte)prim.PrimData.Material; // Material
            data[pos++] = (byte)prim.ClickAction; // ClickAction
            prim.Scale.ToBytes(data, pos); // Scale
            pos += 12;
            prim.Position.ToBytes(data, pos); // Position
            pos += 12;
            prim.Rotation.ToBytes(data, pos); // Rotation
            pos += 12;
            Utils.UIntToBytes((uint)flags, data, pos); // Compressed flags
            pos += 4;
            prim.OwnerID.ToBytes(data, pos); // OwnerID
            pos += 16;
            prim.AngularVelocity.ToBytes(data, pos); // Angular velocity
            pos += 12;
            Utils.UIntToBytes(prim.ParentID, data, pos); // ParentID
            pos += 4;

            if ((flags & CompressedFlags.Tree) != 0)
            {
                data[pos++] = 1;
                data[pos++] = (byte)prim.TreeSpecies;
            }
            //else if ((flags & CompressedFlags.ScratchPad) != 0)
            //{
            //    data[pos++] = (byte)prim.ScratchPad.Length;
            //    Buffer.BlockCopy(prim.ScratchPad, 0, data, pos, prim.ScratchPad.Length);
            //    pos += prim.ScratchPad.Length;
            //}

            Buffer.BlockCopy(textBytes, 0, data, pos, textBytes.Length);
            pos += textBytes.Length;
            prim.TextColor.ToBytes(data, pos, false);
            pos += 4;

            Buffer.BlockCopy(mediaURLBytes, 0, data, pos, mediaURLBytes.Length);
            pos += mediaURLBytes.Length;

            if (particleBytes != null)
            {
                Buffer.BlockCopy(particleBytes, 0, data, pos, particleBytes.Length);
                pos += particleBytes.Length;
            }

            // Extra Params
            Buffer.BlockCopy(extraParamBytes, 0, data, pos, extraParamBytes.Length);
            pos += extraParamBytes.Length;

            if ((flags & CompressedFlags.HasSound) != 0)
            {
                prim.Sound.ToBytes(data, pos);
                pos += 16;
                Utils.FloatToBytes(prim.SoundGain, data, pos);
                pos += 4;
                data[pos++] = (byte)prim.SoundFlags;
                Utils.FloatToBytes(prim.SoundRadius, data, pos);
                pos += 4;
            }

            if (nameValueBytes != null)
            {
                Buffer.BlockCopy(nameValueBytes, 0, data, pos, nameValueBytes.Length);
                pos += nameValueBytes.Length;
            }

            // Path PrimData
            data[pos++] = (byte)prim.PrimData.PathCurve;
            Utils.UInt16ToBytes(Primitive.PackBeginCut(prim.PrimData.PathBegin), data, pos); pos += 2;
            Utils.UInt16ToBytes(Primitive.PackEndCut(prim.PrimData.PathEnd), data, pos); pos += 2;
            data[pos++] = Primitive.PackPathScale(prim.PrimData.PathScaleX);
            data[pos++] = Primitive.PackPathScale(prim.PrimData.PathScaleY);
            data[pos++] = (byte)Primitive.PackPathShear(prim.PrimData.PathShearX);
            data[pos++] = (byte)Primitive.PackPathShear(prim.PrimData.PathShearY);
            data[pos++] = (byte)Primitive.PackPathTwist(prim.PrimData.PathTwist);
            data[pos++] = (byte)Primitive.PackPathTwist(prim.PrimData.PathTwistBegin);
            data[pos++] = (byte)Primitive.PackPathTwist(prim.PrimData.PathRadiusOffset);
            data[pos++] = (byte)Primitive.PackPathTaper(prim.PrimData.PathTaperX);
            data[pos++] = (byte)Primitive.PackPathTaper(prim.PrimData.PathTaperY);
            data[pos++] = Primitive.PackPathRevolutions(prim.PrimData.PathRevolutions);
            data[pos++] = (byte)Primitive.PackPathTwist(prim.PrimData.PathSkew);
            // Profile PrimData
            data[pos++] = prim.PrimData.profileCurve;
            Utils.UInt16ToBytes(Primitive.PackBeginCut(prim.PrimData.ProfileBegin), data, pos); pos += 2;
            Utils.UInt16ToBytes(Primitive.PackEndCut(prim.PrimData.ProfileEnd), data, pos); pos += 2;
            Utils.UInt16ToBytes(Primitive.PackProfileHollow(prim.PrimData.ProfileHollow), data, pos); pos += 2;

            // Texture Length
            Utils.UIntToBytes((uint)textureBytes.Length, data, pos);
            pos += 4;
            // Texture Entry
            Buffer.BlockCopy(textureBytes, 0, data, pos, textureBytes.Length);
            pos += textureBytes.Length;

            Utils.UIntToBytes((uint)textureAnimBytes.Length, data, pos);
            pos += 4;
            Buffer.BlockCopy(textureAnimBytes, 0, data, pos, textureAnimBytes.Length);
            pos += textureAnimBytes.Length;

            System.Diagnostics.Debug.Assert(pos == size, "Got a pos of " + pos + " instead of " + size);

            #endregion Packet serialization

            return new ObjectUpdateCompressedPacket.ObjectDataBlock { Data = data };
        }

        private ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateTerseUpdateBlock(ISceneEntity entity, bool withTexture)
        {
            bool isAvatar = (entity is IScenePresence);
            LLPrimitive prim = entity as LLPrimitive;
            Quaternion rotation = entity.RelativeRotation;
            Vector3 velocity;
            Vector3 acceleration;
            Vector3 angularVelocity;

            int pos = 0;
            byte[] data = new byte[isAvatar ? 60 : 44];

            // LocalID
            Utils.UIntToBytes(entity.LocalID, data, pos);
            pos += 4;

            // State
            byte state = 0;
            if (prim != null)
                state = prim.Prim.PrimData.State;
            data[pos++] = state;

            // Avatar/CollisionPlane
            if (isAvatar)
            {
                Vector4 collisionPlane = (entity is IPhysicalPresence) ?
                    ((IPhysicalPresence)entity).CollisionPlane :
                    Vector4.UnitW;
                if (collisionPlane == Vector4.Zero)
                    collisionPlane = Vector4.UnitW;

                data[pos++] = 1;
                collisionPlane.ToBytes(data, pos);
                pos += 16;
            }
            else
            {
                ++pos;
            }

            if (entity is IPhysical)
            {
                IPhysical physical = (IPhysical)entity;

                velocity = physical.Velocity;
                acceleration = physical.Acceleration;
                angularVelocity = physical.AngularVelocity;
            }
            else
            {
                velocity = Vector3.Zero;
                acceleration = Vector3.Zero;
                angularVelocity = Vector3.Zero;
            }

            // Damp small values
            if (velocity.ApproxEquals(Vector3.Zero, 2f))
                velocity = Vector3.Zero;
            if (angularVelocity.ApproxEquals(Vector3.Zero, 2f))
                angularVelocity = Vector3.Zero;

            // Position
            entity.RelativePosition.ToBytes(data, pos);
            pos += 12;

            // Velocity
            Utils.UInt16ToBytes(Utils.FloatToUInt16(velocity.X, -128.0f, 128.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(velocity.Y, -128.0f, 128.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(velocity.Z, -128.0f, 128.0f), data, pos); pos += 2;

            // Acceleration
            Utils.UInt16ToBytes(Utils.FloatToUInt16(acceleration.X, -64.0f, 64.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(acceleration.Y, -64.0f, 64.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(acceleration.Z, -64.0f, 64.0f), data, pos); pos += 2;

            // Rotation
            Utils.UInt16ToBytes(Utils.FloatToUInt16(rotation.X, -1.0f, 1.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(rotation.Y, -1.0f, 1.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(rotation.Z, -1.0f, 1.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(rotation.W, -1.0f, 1.0f), data, pos); pos += 2;

            // Angular Velocity
            Utils.UInt16ToBytes(Utils.FloatToUInt16(angularVelocity.X, -64.0f, 64.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(angularVelocity.Y, -64.0f, 64.0f), data, pos); pos += 2;
            Utils.UInt16ToBytes(Utils.FloatToUInt16(angularVelocity.Z, -64.0f, 64.0f), data, pos); pos += 2;

            ImprovedTerseObjectUpdatePacket.ObjectDataBlock block = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
            block.Data = data;

            if (withTexture && prim != null)
            {
                byte[] textureBytes = prim.Prim.Textures.GetBytes();
                byte[] textureEntry = new byte[textureBytes.Length + 4];

                // Texture Length
                Utils.IntToBytes(textureBytes.Length, textureEntry, 0);
                // Texture
                Buffer.BlockCopy(textureBytes, 0, textureEntry, 4, textureBytes.Length);

                block.TextureEntry = textureEntry;
            }
            else
            {
                block.TextureEntry = Utils.EmptyBytes;
            }

            return block;
        }

        private static PrimFlags GetUpdateFlags(IScenePresence presence, IScenePresence sendingTo)
        {
            PrimFlags flags = PrimFlags.CastShadows | PrimFlags.InventoryEmpty | PrimFlags.Money | PrimFlags.Physics;

            if (presence == sendingTo)
                flags |= PrimFlags.ObjectYouOwner;

            return flags;
        }

        private static PrimFlags GetUpdateFlags(LLPrimitive prim, IScenePresence sendingTo, LLPermissions m_permissions)
        {
            PrimFlags flags;

            if (m_permissions != null && sendingTo != null)
            {
                flags = m_permissions.GetFlagsFor(sendingTo, prim);
            }
            else
            {
                flags = PrimFlags.CastShadows | PrimFlags.ObjectCopy | PrimFlags.ObjectTransfer |
                    PrimFlags.ObjectYouOwner | PrimFlags.ObjectModify | PrimFlags.ObjectMove |
                    PrimFlags.ObjectOwnerModify;
            }

            return flags;
        }

        #endregion Object Update Creation

        #region Avatar Tracking

        private void SerializeRecentAvatars()
        {
            if (m_dataStore != null)
            {
                OSDMap map = new OSDMap();

                lock (m_recentAvatars)
                {
                    int i = 0;
                    foreach (KeyValuePair<UUID, DateTime> kvp in m_recentAvatars)
                    {
                        if (i++ >= AVATAR_TRACKING_COUNT)
                            break;
                        map[kvp.Key.ToString()] = kvp.Value;
                    }
                }

                m_dataStore.BeginSerialize(new SerializedData
                {
                    StoreID = m_scene.ID,
                    Section = "avatarhistory",
                    Name = "avatarhistory",
                    Data = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(map)),
                    ContentType = "application/llsd+json",
                    Version = 1,
                });
            }
        }

        private void DeserializeRecentAvatars()
        {
            if (m_dataStore != null)
            {
                SerializedData item = m_dataStore.DeserializeOne(m_scene.ID, "avatarhistory");
                if (item != null)
                {
                    using (System.IO.MemoryStream stream = new System.IO.MemoryStream(item.Data))
                    {
                        OSDMap map = OSDParser.DeserializeJson(stream) as OSDMap;
                        if (map != null)
                        {
                            foreach (KeyValuePair<string, OSD> kvp in map)
                                m_recentAvatars[UUID.Parse(kvp.Key)] = kvp.Value;
                        }
                    }
                }
            }
        }

        #endregion Avatar Tracking

        private QueuedInterestListEvent ObjectUpdateCombiner(QueuedInterestListEvent currentData, QueuedInterestListEvent newData)
        {
            // If this event and the previous event are both updates, combine the UpdateFlags together
            if (currentData.Event.Type == OBJECT_UPDATE && newData.Event.Type == OBJECT_UPDATE)
            {
                EntityAddOrUpdateArgs currentArgs = (EntityAddOrUpdateArgs)currentData.Event.State;
                EntityAddOrUpdateArgs newArgs = (EntityAddOrUpdateArgs)newData.Event.State;

                System.Diagnostics.Debug.Assert(currentArgs.Entity.ID == newArgs.Entity.ID,
                    "Attempting to combine two different entities");

                // Combine the update flags on these two updates
                newArgs.UpdateFlags |= currentArgs.UpdateFlags;
                newArgs.ExtraFlags |= currentArgs.ExtraFlags;
            }

            // Otherwise, the new event overrides the previous event
            return newData;
        }

        private void DetachObject(LLAgent agent, LLPrimitive prim)
        {
            // FIXME: Implement
        }

        private bool CanModify(LLAgent agent, List<uint> localIDs, out List<LLPrimitive> prims)
        {
            bool success = true;
            prims = new List<LLPrimitive>(localIDs.Count);

            for (int i = 0; i < localIDs.Count; i++)
            {
                ISceneEntity entity;
                if (m_scene.TryGetEntity(localIDs[i], out entity) && entity is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)entity;
                    PermissionMask perms = m_permissions.GetPrimPermissions(agent, prim);

                    if (perms.HasPermission(PermissionMask.Modify))
                    {
                        prims.Add(prim);
                    }
                    else
                    {
                        m_log.Warn(agent.Name + " unauthorized to modify prim " + localIDs[i] + ", sending reset packet");

                        SendEntityTo(agent, prim, false);
                        success = false;
                    }
                }
                else
                {
                    m_log.Warn(agent.Name + " tried to modify missing prim " + localIDs[i] + ", sending kill packet");

                    SendSingleKillPacket(agent, localIDs[i]);
                    success = false;
                }
            }

            if (!success)
                m_scene.PresenceAlert(this, agent, "You do not have permission to edit one or more selected objects");

            return success;
        }

        private bool CanAddPrims(LLAgent agent, Vector3 position, int count)
        {
            // Access check
            if (!m_permissions.CanCreateAt(agent, position))
            {
                m_scene.PresenceAlert(this, agent, "You cannot create objects here. The owner of this land does not allow it. " +
                    "Use the land tool to see land ownership.");
                return false;
            }

            // Prim usage check
            int used, capacity;
            m_permissions.GetRegionPrimUsage(agent, position, out used, out capacity);
            if (used + count > capacity)
            {
                m_scene.PresenceAlert(this, agent, "Can't create object because the parcel is full.");
                return false;
            }

            return true;
        }

        private static byte[] StringToBytesNullTerminated(string str)
        {
            byte[] data = Utils.StringToBytes(str);
            if (data.Length > 0)
                return data;

            return new byte[1];
        }
    }
}
