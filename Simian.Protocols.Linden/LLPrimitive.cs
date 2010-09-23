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
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    #region Enums

    /// <summary>
    /// Specifies that fields that have changed in a call to IScene.EntityAddOrUpdate
    /// </summary>
    [Flags]
    public enum LLUpdateFlags : uint
    {
        AttachmentPoint = 1 << 0,
        Material = 1 << 1,
        ClickAction = 1 << 2,
        PrimFlags = 1 << 3,
        MediaURL = 1 << 4,
        ScratchPad = 1 << 5,
        Textures = 1 << 6,
        TextureAnim = 1 << 7,
        NameValue = 1 << 8,
        Text = 1 << 9,
        Particles = 1 << 10,
        ExtraData = 1 << 11,
        Sound = 1 << 12,
        Joint = 1 << 13,
        NoCachedUpdate = 1 << 14
    }

    public static class LLUpdateFlagsExtensions
    {
        public static bool HasFlag(this LLUpdateFlags updateFlags, LLUpdateFlags flag)
        {
            return (updateFlags & flag) == flag;
        }
    }

    public static class UpdateFlagsExtensions
    {
        public static bool HasFlag(this UpdateFlags updateFlags, UpdateFlags flag)
        {
            return (updateFlags & flag) == flag;
        }
    }

    public static class PrimFlagsExtensions
    {
        public static bool HasFlag(this PrimFlags primFlags, PrimFlags flag)
        {
            return (primFlags & flag) == flag;
        }
    }

    #endregion Enums

    [System.Diagnostics.DebuggerDisplay("{Prim.Properties.Name} {Prim.ID} ({Prim.LocalID})")]
    public class LLPrimitive : ISceneEntity, ILinkable, IPhysical
    {
        const int UNDO_STEPS = 8;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        /// <summary>Reference to the primitive object this class wraps</summary>
        public Primitive Prim;
        /// <summary>Saved seat offset. Applied to avatars that sit on this object</summary>
        public Vector3 SitPosition;
        /// <summary>Saved seat rotation. Applied to avatars that sit on this object</summary>
        public Quaternion SitRotation = Quaternion.Identity;
        /// <summary>Saved attachment offset. Applied to this object when it is attached
        /// to an avatar</summary>
        public Vector3 AttachmentPosition;
        /// <summary>Saved attachment rotation. Applied to this object when it is attached
        /// to an avatar</summary>
        public Quaternion AttachmentRotation = Quaternion.Identity;
        /// <summary>Saved attachment point. Applied to this object when it is attached to
        /// an avatar</summary>
        public AttachmentPoint LastAttachmentPoint;
        /// <summary>Rotation that is saved when this object is attached to an avatar.
        /// Will be applied to the object when it is dropped. This is always the world
        /// rotation, since it is only applicable to parent objects</summary>
        public Quaternion BeforeAttachmentRotation = Quaternion.Identity;
        /// <summary>Inventory items inside this prim, aka task inventory</summary>
        public PrimInventory Inventory;
        /// <summary>Represents STATUS_ROTATE_X. If false, this axis is locked</summary>
        public bool AllowRotateX = true;
        /// <summary>Represents STATUS_ROTATE_Y. If false, this axis is locked</summary>
        public bool AllowRotateY = true;
        /// <summary>Represents STATUS_ROTATE_Z. If false, this axis is locked</summary>
        public bool AllowRotateZ = true;
        /// <summary>Represents STATUS_BLOCK_GRAB. If true, a physical object can not be manually stopped or dragged around</summary>
        public bool BlockGrab;
        /// <summary>Personal Identification Number to allow scripts in other prims to load scripts into this prim</summary>
        public int RemoteScriptAccessPIN;
        /// <summary>Default amount set by llSetPayPrice</summary>
        public int PayPrice;
        /// <summary>Four button amounts set by llSetPayPrice (Default values are 1, 5, 10, and 20)</summary>
        public readonly int[] PayPriceButtons = new int[4] { 1, 5, 10, 20 };

        private IScene m_scene;
        private IPrimMesher m_mesher;
        private Vector3 m_rotationAxis = Vector3.UnitZ;
        private ILinkable m_parent;
        private int m_linkNumber;
        private MapAndArray<UUID, ILinkable> m_children;

        private CircularQueue<Primitive> m_undoSteps = new CircularQueue<Primitive>(UNDO_STEPS);
        private CircularQueue<Primitive> m_redoSteps = new CircularQueue<Primitive>(UNDO_STEPS);
        private DateTime m_lastUpdated;

        private Vector3 m_lastPosition;
        private Quaternion m_lastRotation = Quaternion.Identity;
        private Vector3 m_lastAngularVelocity;
        private Vector3 m_lastAcceleration;
        private Vector3 m_lastVelocity;
        private int m_fallMS;
        private bool m_frozen;
        private float m_volume;
        private object m_syncRoot = new object();

        #region ISceneEntity Properties

        public UUID ID { get { return Prim.ID; } }
        public uint LocalID { get { return Prim.LocalID; } }
        public IScene Scene { get { return m_scene; } }

        public string Name
        {
            get { return Prim.Properties.Name ?? String.Empty; }
            set { Prim.Properties.Name = value; }
        }
        public UUID OwnerID
        {
            get { return Prim.Properties.OwnerID; }
            set { Prim.Properties.OwnerID = value; }
        }
        public UUID CreatorID
        {
            get { return Prim.Properties.CreatorID; }
            set { Prim.Properties.CreatorID = value; }
        }
        public UUID GroupID
        {
            get { return Prim.Properties.GroupID; }
            set { Prim.Properties.GroupID = value; }
        }
        public Vector3 Scale
        {
            get { return Prim.Scale; }
            set { Prim.Scale = value; }
        }
        public Vector3 RelativePosition
        {
            get { return Prim.Position; }
            set { Prim.Position = value; }
        }
        public Quaternion RelativeRotation
        {
            get { return Prim.Rotation; }
            set { Prim.Rotation = value; }
        }
        public Vector3 ScenePosition
        {
            get
            {
                Vector3 position = RelativePosition;

                ILinkable parent = Parent;
                if (parent != null)
                    position = parent.ScenePosition + Vector3.Transform(position, Matrix4.CreateFromQuaternion(parent.SceneRotation));

                return position;
            }
        }
        public Quaternion SceneRotation
        {
            get
            {
                Quaternion rotation = RelativeRotation;

                ILinkable parent = Parent;
                if (parent != null)
                    rotation *= parent.SceneRotation;

                return rotation;
            }
        }
        public Vector3d GlobalPosition
        {
            get
            {
                return m_scene.MinPosition + new Vector3d(ScenePosition);
            }
        }
        public AABB SceneAABB
        {
            get
            {
                Vector3 center = ScenePosition;
                Vector3 halfExtent = Prim.Scale * 0.5f;

                Vector3 min = center - halfExtent;
                Vector3 max = center + halfExtent;

                // Rotate the min and max
                Matrix4 rotate = Matrix4.CreateFromQuaternion(SceneRotation);
                min *= rotate;
                max *= rotate;

                // Find the new min/max
                Vector3 newMin = new Vector3(
                    Math.Min(min.X, max.X),
                    Math.Min(min.Y, max.Y),
                    Math.Min(min.Z, max.Z)
                );
                Vector3 newMax = new Vector3(
                    Math.Max(min.X, max.X),
                    Math.Max(min.Y, max.Y),
                    Math.Max(min.Z, max.Z)
                );

                return new AABB(newMin, newMax);
            }
        }
        /// <summary>Link number, if this object is part of a linkset</summary>
        public int LinkNumber
        {
            get { return m_linkNumber; }
            set { m_linkNumber = value; }
        }
        public Vector3 LastRelativePosition
        {
            get { return m_lastPosition; }
            set { m_lastPosition = value; }
        }
        public Quaternion LastRelativeRotation
        {
            get { return m_lastRotation; }
            set { m_lastRotation = value; }
        }
        public Vector3 LastSignificantPosition { get; set; }

        public void MarkAsModified()
        {
            m_lastUpdated = DateTime.UtcNow;
        }

        #endregion ISceneEntity Properties

        #region ILinkable Properties

        public ILinkable Parent
        {
            get { return m_parent; }
        }

        public ILinkable[] GetChildren()
        {
            lock (m_syncRoot)
            {
                if (m_children == null)
                    return new ILinkable[0];
                return m_children.GetArray();
            }
        }

        #endregion ILinkable Properties

        #region IPhysical Properties

        public int FallStart
        {
            get { return m_fallMS; }
            set { m_fallMS = value; }
        }
        public Vector3 Velocity
        {
            get { return Prim.Velocity; }
            set { Prim.Velocity = value; }
        }
        public Vector3 Acceleration
        {
            get { return Prim.Acceleration; }
            set { Prim.Acceleration = value; }
        }
        public Vector3 AngularVelocity
        {
            get { return Prim.AngularVelocity; }
            set { Prim.AngularVelocity = value; }
        }
        public Vector3 RotationAxis
        {
            get { return m_rotationAxis; }
            set { m_rotationAxis = value; }
        }
        public bool CollisionsEnabled
        {
            get { return !(Prim.Flags.HasFlag(PrimFlags.Phantom)); }
            set
            {
                if (value)
                    Prim.Flags &= ~PrimFlags.Phantom;
                else
                    Prim.Flags |= PrimFlags.Phantom;
            }
        }
        public bool DynamicsEnabled
        {
            get { return (Prim.Flags.HasFlag(PrimFlags.Physics) && m_parent == null); }
            set
            {
                if (value)
                    Prim.Flags |= PrimFlags.Physics;
                else
                    Prim.Flags &= ~PrimFlags.Physics;
            }
        }
        public bool Frozen
        {
            get { return m_frozen; }
            set { m_frozen = value; }
        }
        public Vector3 LastAngularVelocity
        {
            get { return m_lastAngularVelocity; }
            set { m_lastAngularVelocity = value; }
        }
        public Vector3 LastAcceleration
        {
            get { return m_lastAcceleration; }
            set { m_lastAcceleration = value; }
        }
        public Vector3 LastVelocity
        {
            get { return m_lastVelocity; }
            set { m_lastVelocity = value; }
        }

        #endregion IPhysical Properties

        /// <summary>The last time this entity was modified</summary>
        public DateTime LastUpdated { get { return m_lastUpdated; } }

        /// <summary>
        /// Constructor
        /// </summary>
        public LLPrimitive(Primitive prim, IScene scene, IPrimMesher mesher)
        {
            Prim = prim;
            m_scene = scene;
            m_mesher = mesher;
            Inventory = new PrimInventory(this);

            if (prim.ID == UUID.Zero)
                prim.ID = UUID.Random();

            if (prim.LocalID == 0)
                prim.LocalID = m_scene.CreateLocalID();

            if (prim.ParentID != 0)
            {
                ISceneEntity parent;
                if (scene.TryGetEntity(prim.ParentID, out parent) && parent is ILinkable)
                    SetParent((ILinkable)parent, false, false);
            }

            m_lastUpdated = DateTime.UtcNow;
        }

        #region ILinkable Methods

        public void AddChild(ILinkable child)
        {
            lock (m_syncRoot)
            {
                if (m_children == null)
                    m_children = new MapAndArray<UUID, ILinkable>();
                m_children.Add(child.ID, child);
            }
        }

        public void SetParent(ILinkable parent, bool adjustPosRot, bool sendUpdate)
        {
            // If this is already the parent then nevermind
            if (parent == m_parent)
                return;

            // Delink from old parent if we are already linked
            if (m_parent != null)
            {
                // Transform orientation back from local to scene orientation
                this.RelativePosition = m_parent.RelativePosition + Vector3.Transform(this.RelativePosition,
                    Matrix4.CreateFromQuaternion(m_parent.RelativeRotation));

                this.RelativeRotation *= m_parent.RelativeRotation;

                // Remove us from the old parent
                m_parent.RemoveChild(this.ID);
            }

            // Link ourself to the new parent
            if (parent != null)
            {
                // Move children to new parent while we are still unlinked
                ILinkable[] children = GetChildren();
                for (int c = 0; c < children.Length; c++)
                    children[c].SetParent(parent, adjustPosRot, sendUpdate);

                // Assign new parent
                m_parent = parent;

                if (adjustPosRot)
                {
                    // Transform from scene orientation to new local orientation
                    this.RelativePosition = Vector3.Transform(this.RelativePosition - m_parent.RelativePosition,
                        Matrix4.CreateFromQuaternion(Quaternion.Identity / m_parent.RelativeRotation));
                    this.RelativeRotation /= m_parent.RelativeRotation;
                }

                // Add us as a child of the new parent
                m_parent.AddChild(this);
                Prim.ParentID = m_parent.LocalID;
            }
            else
            {
                // No new parent (not linked)
                m_parent = null;
                Prim.ParentID = 0;
            }

            if (sendUpdate)
            {
                // Linking/unlinking destroys our undo history
                ClearUndoHistory();

                m_scene.EntityAddOrUpdate(this, this, UpdateFlags.Position | UpdateFlags.Rotation | UpdateFlags.Parent, 0);
            }
        }

        public bool RemoveChild(UUID childID)
        {
            lock (m_syncRoot)
            {
                if (m_children != null)
                    return m_children.Remove(childID);
                return false;
            }
        }

        #endregion ILinkable Methods

        #region IPhysical Methods

        public float GetMass()
        {
            if (m_volume == 0f)
                UpdateVolume(null);

            return m_volume * GetDensity();
        }

        private float GetDensity()
        {
            const float PRIM_DENSITY = 1000f; //kg/m^3
            return PRIM_DENSITY;
        }

        public void ResetMass()
        {
            m_volume = 0f;
        }

        public PhysicsType GetPhysicsType()
        {
            const float MIN_MESH_SIZE_SQ = 0.2f * 0.2f;
            Primitive.ConstructionData shape = Prim.PrimData;

            // Anything smaller than MIN_MESH_SIZE will become a simple cube
            if (Prim.Scale.LengthSquared() < MIN_MESH_SIZE_SQ)
            {
                return PhysicsType.Box;
            }
            else if (shape.ProfileCurve == ProfileCurve.Square && shape.PathCurve == PathCurve.Line)
            {
                if (HasBasicShape(shape))
                    return PhysicsType.Box;
            }
            else if (shape.ProfileCurve == ProfileCurve.HalfCircle && shape.PathCurve == PathCurve.Circle)
            {
                if (HasBasicShape(shape))
                    return PhysicsType.Sphere;
            }
            // TODO: Figure out what the problem with cylinders is
            //else if (shape.ProfileCurve == ProfileCurve.Circle && shape.PathCurve == PathCurve.Line)
            //{
            //    if (HasBasicShape(shape))
            //        return PhysicsType.Cylinder;
            //}

            return PhysicsType.Mesh;
        }

        public ulong GetPhysicsKey()
        {
            ulong hash = 5381; // Nice prime to start with

            Primitive.ConstructionData data = Prim.PrimData;

            hash = Util.djb2(hash, (byte)data.PathCurve);
            hash = Util.djb2(hash, (byte)((byte)data.ProfileHole | (byte)data.ProfileCurve));
            hash = Util.djb2(hash, data.PathBegin);
            hash = Util.djb2(hash, data.PathEnd);
            hash = Util.djb2(hash, data.PathScaleX);
            hash = Util.djb2(hash, data.PathScaleY);
            hash = Util.djb2(hash, data.PathShearX);
            hash = Util.djb2(hash, data.PathShearY);
            hash = Util.djb2(hash, data.PathTwist);
            hash = Util.djb2(hash, data.PathTwistBegin);
            hash = Util.djb2(hash, data.PathRadiusOffset);
            hash = Util.djb2(hash, data.PathTaperX);
            hash = Util.djb2(hash, data.PathTaperY);
            hash = Util.djb2(hash, data.PathRevolutions);
            hash = Util.djb2(hash, data.PathSkew);
            hash = Util.djb2(hash, data.ProfileBegin);
            hash = Util.djb2(hash, data.ProfileEnd);
            hash = Util.djb2(hash, data.ProfileHollow);

            // Include sculpt data if it exists
            if (Prim.Sculpt != null && Prim.Sculpt.SculptTexture != UUID.Zero)
            {
                byte[] sculptBytes = Prim.Sculpt.GetBytes();
                for (int i = 0; i < sculptBytes.Length; i++)
                    hash = Util.djb2(hash, sculptBytes[i]);
            }

            return hash;
        }

        public PhysicsHull GetPhysicsHull()
        {
            PhysicsHull hull = m_mesher.GetPhysicsMesh(this);

            // Update the volume since we have a PhysicsProxy on hand
            if (m_volume == 0f)
                UpdateVolume(hull);

            return hull;
        }

        #endregion IPhysical Methods

        #region Undo/Redo Methods

        public void SaveUndoStep()
        {
            lock (m_syncRoot)
                m_undoSteps.Enqueue(new Primitive(Prim));
        }

        public void ClearUndoHistory()
        {
            lock (m_syncRoot)
            {
                m_undoSteps.Clear();
                m_redoSteps.Clear();
            }
        }

        public bool Undo()
        {
            lock (m_syncRoot)
            {
                Primitive undoState = m_undoSteps.DequeueLast();

                if (undoState != null)
                {
                    m_log.Debug("Performing undo on object " + ID);

                    m_redoSteps.Enqueue(new Primitive(Prim));
                    Prim = undoState;

                    return true;
                }
                else
                {
                    m_log.DebugFormat("Undo requested on object {0} with no remaining undo steps", ID);
                }
            }

            return false;
        }

        public bool Redo()
        {
            lock (m_syncRoot)
            {
                Primitive redoState = m_redoSteps.DequeueLast();

                if (redoState != null)
                {
                    m_log.Debug("Performing redo on object " + ID);

                    m_undoSteps.Enqueue(new Primitive(Prim));
                    Prim = redoState;

                    return true;
                }
                else
                {
                    m_log.DebugFormat("Redo requested on object {0} with no remaining redo steps", ID);
                }
            }

            return false;
        }

        #endregion Undo/Redo Methods

        #region Serialization Methods

        public OSDMap GetOSD()
        {
            Primitive.ConstructionData primData = Prim.PrimData;
            Primitive.ObjectProperties properties = Prim.Properties;

            OSDMap pathMap = new OSDMap();
            pathMap["begin"] = OSD.FromReal(primData.PathBegin);
            pathMap["curve"] = OSD.FromInteger((int)primData.PathCurve);
            pathMap["end"] = OSD.FromReal(primData.PathEnd);
            pathMap["radius_offset"] = OSD.FromReal(primData.PathRadiusOffset);
            pathMap["revolutions"] = OSD.FromReal(primData.PathRevolutions);
            pathMap["scale_x"] = OSD.FromReal(primData.PathScaleX);
            pathMap["scale_y"] = OSD.FromReal(primData.PathScaleY);
            pathMap["shear_x"] = OSD.FromReal(primData.PathShearX);
            pathMap["shear_y"] = OSD.FromReal(primData.PathShearY);
            pathMap["skew"] = OSD.FromReal(primData.PathSkew);
            pathMap["taper_x"] = OSD.FromReal(primData.PathTaperX);
            pathMap["taper_y"] = OSD.FromReal(primData.PathTaperY);
            pathMap["twist"] = OSD.FromReal(primData.PathTwist);
            pathMap["twist_begin"] = OSD.FromReal(primData.PathTwistBegin);

            OSDMap profileMap = new OSDMap();
            profileMap["begin"] = OSD.FromReal(primData.ProfileBegin);
            profileMap["curve"] = OSD.FromInteger((int)primData.ProfileCurve);
            profileMap["hole"] = OSD.FromInteger((int)primData.ProfileHole);
            profileMap["end"] = OSD.FromReal(primData.ProfileEnd);
            profileMap["hollow"] = OSD.FromReal(primData.ProfileHollow);

            OSDMap propertiesMap = new OSDMap();

            if (properties != null)
            {
                propertiesMap["aggregate_perms"] = OSD.FromInteger(properties.AggregatePerms);
                propertiesMap["aggregate_perms_textures"] = OSD.FromInteger(properties.AggregatePermTextures);
                propertiesMap["aggregate_perms_textures_owner"] = OSD.FromInteger(properties.AggregatePermTexturesOwner);
                propertiesMap["category"] = OSD.FromInteger((int)properties.Category);
                propertiesMap["creation_date"] = OSD.FromDate(properties.CreationDate);
                propertiesMap["creator_id"] = OSD.FromUUID(properties.CreatorID);
                propertiesMap["description"] = OSD.FromString(properties.Description);
                propertiesMap["folder_id"] = OSD.FromUUID(properties.FolderID);
                propertiesMap["from_task_id"] = OSD.FromUUID(properties.FromTaskID);
                // properties.GroupID is redundant
                propertiesMap["inventory_serial"] = OSD.FromInteger(properties.InventorySerial);
                propertiesMap["item_id"] = OSD.FromUUID(properties.ItemID);
                propertiesMap["last_owner_id"] = OSD.FromUUID(properties.LastOwnerID);
                propertiesMap["name"] = OSD.FromString(properties.Name);
                // properties.ObjectID is redundant
                // properties.OwnerID is redundant
                propertiesMap["ownership_cost"] = OSD.FromInteger(properties.OwnershipCost);
                propertiesMap["permissions"] = properties.Permissions.GetOSD();
                propertiesMap["sale_price"] = OSD.FromInteger(properties.SalePrice);
                propertiesMap["sale_type"] = OSD.FromInteger((int)properties.SaleType);
                propertiesMap["sit_name"] = OSD.FromString(properties.SitName);
                propertiesMap["touch_name"] = OSD.FromString(properties.TouchName);
            }

            OSDMap primMap = new OSDMap();
            primMap["path"] = pathMap;
            primMap["profile"] = profileMap;
            primMap["properties"] = propertiesMap;

            primMap["acceleration"] = OSD.FromVector3(Prim.Acceleration);
            primMap["ang_velocity"] = OSD.FromVector3(Prim.AngularVelocity);
            primMap["click_action"] = OSD.FromInteger((int)Prim.ClickAction);
            primMap["flags"] = OSD.FromInteger((uint)Prim.Flags);
            primMap["group_id"] = OSD.FromUUID(Prim.GroupID);
            primMap["id"] = OSD.FromUUID(Prim.ID);
            primMap["local_id"] = OSD.FromInteger(Prim.LocalID);
            primMap["media_url"] = OSD.FromString(Prim.MediaURL);
            primMap["owner_id"] = OSD.FromUUID(Prim.OwnerID);
            primMap["parent_id"] = OSD.FromInteger(Prim.ParentID);
            primMap["particles"] = Prim.ParticleSys.GetOSD();
            primMap["position"] = OSD.FromVector3(Prim.Position);
            primMap["rotation"] = OSD.FromQuaternion(Prim.Rotation);
            primMap["scale"] = OSD.FromVector3(Prim.Scale);
            primMap["scratch_pad"] = OSD.FromBinary(Prim.ScratchPad);
            primMap["sound"] = OSD.FromUUID(Prim.Sound);
            primMap["sound_flags"] = OSD.FromInteger((int)Prim.SoundFlags);
            primMap["sound_gain"] = OSD.FromReal(Prim.SoundGain);
            primMap["sound_radius"] = OSD.FromReal(Prim.SoundRadius);
            primMap["text"] = OSD.FromString(Prim.Text);
            primMap["text_color"] = OSD.FromColor4(Prim.TextColor);
            primMap["texture_anim"] = Prim.TextureAnim.GetOSD();
            primMap["tree_species"] = OSD.FromInteger((int)Prim.TreeSpecies);
            primMap["velocity"] = OSD.FromVector3(Prim.Velocity);

            primMap["material"] = OSD.FromInteger((int)primData.Material);
            primMap["state"] = OSD.FromInteger(primData.State);
            primMap["pcode"] = OSD.FromInteger((int)primData.PCode);

            if (Prim.NameValues != null)
                primMap["name_values"] = OSD.FromString(NameValue.NameValuesToString(Prim.NameValues));
            if (Prim.Textures != null)
                primMap["textures"] = Prim.Textures.GetOSD();
            if (Prim.Light != null)
                primMap["light"] = Prim.Light.GetOSD();
            if (Prim.Flexible != null)
                primMap["flex"] = Prim.Flexible.GetOSD();
            if (Prim.Sculpt != null)
                primMap["sculpt"] = Prim.Sculpt.GetOSD();

            OSDMap map = new OSDMap();
            map["prim"] = primMap;
            map["sit_position"] = OSD.FromVector3(SitPosition);
            map["sit_rotation"] = OSD.FromQuaternion(SitRotation);
            map["attachment_position"] = OSD.FromVector3(AttachmentPosition);
            map["attachment_rotation"] = OSD.FromQuaternion(AttachmentRotation);
            map["last_attachment_point"] = OSD.FromInteger((int)LastAttachmentPoint);
            map["before_attachment_rotation"] = OSD.FromQuaternion(BeforeAttachmentRotation);
            map["rotation_axis"] = OSD.FromVector3(m_rotationAxis);
            map["link_number"] = OSD.FromInteger(m_linkNumber);
            map["remote_script_access_pin"] = OSD.FromInteger(RemoteScriptAccessPIN);
            map["inventory"] = OSD.FromString(Inventory.GetTaskInventoryAsset());

            OSDArray buttons = new OSDArray { 
                OSD.FromInteger(PayPriceButtons[0]),
                OSD.FromInteger(PayPriceButtons[1]),
                OSD.FromInteger(PayPriceButtons[2]),
                OSD.FromInteger(PayPriceButtons[3])
            };

            map["pay_price_buttons"] = buttons;
            map["pay_price"] = OSD.FromInteger(PayPrice);

            if (Prim.FaceMedia != null)
            {
                OSDArray faceMedia = new OSDArray(Prim.FaceMedia.Length);
                for (int i = 0; i < Prim.FaceMedia.Length; i++)
                {
                    MediaEntry entry = Prim.FaceMedia[i];
                    if (entry != null)
                        faceMedia.Add(entry.GetOSD());
                    else
                        faceMedia.Add(new OSD());
                }
                map["face_media"] = faceMedia;
            }
            map["media_version"] = OSD.FromString(Prim.MediaVersion);

            map["last_updated"] = OSD.FromDate(m_lastUpdated);

            return map;
        }

        public static LLPrimitive FromOSD(OSDMap map, IScene scene, IPrimMesher mesher)
        {
            if (map == null)
                return null;

            OSDMap primMap = map["prim"] as OSDMap;
            Primitive prim = new Primitive();
            prim.Acceleration = primMap["acceleration"].AsVector3();
            prim.AngularVelocity = primMap["ang_velocity"].AsVector3();
            prim.ClickAction = (ClickAction)primMap["click_action"].AsInteger();
            prim.Flags = (PrimFlags)primMap["flags"].AsUInteger();
            prim.GroupID = primMap["group_id"].AsUUID();
            prim.ID = primMap["id"].AsUUID();
            prim.LocalID = primMap["local_id"].AsUInteger();
            prim.MediaURL = primMap["media_url"].AsString();
            prim.OwnerID = primMap["owner_id"].AsUUID();
            prim.ParentID = primMap["parent_id"].AsUInteger();
            prim.ParticleSys = Primitive.ParticleSystem.FromOSD(primMap["particles"]);
            prim.Position = primMap["position"].AsVector3();
            prim.Rotation = primMap["rotation"].AsQuaternion();
            prim.Scale = primMap["scale"].AsVector3();
            prim.ScratchPad = primMap["scratch_pad"].AsBinary();
            prim.Sound = primMap["sound"].AsUUID();
            prim.SoundFlags = (SoundFlags)primMap["sound_flags"].AsInteger();
            prim.SoundGain = (float)primMap["sound_gain"].AsReal();
            prim.SoundRadius = (float)primMap["sound_radius"].AsReal();
            prim.Text = primMap["text"].AsString();
            prim.TextColor = primMap["text_color"].AsColor4();
            prim.TextureAnim = Primitive.TextureAnimation.FromOSD(primMap["texture_anim"]);
            prim.TreeSpecies = (Tree)primMap["tree_species"].AsInteger();
            prim.Velocity = primMap["velocity"].AsVector3();

            prim.PrimData.Material = (Material)primMap["material"].AsInteger();
            prim.PrimData.State = (byte)primMap["state"].AsInteger();
            prim.PrimData.PCode = (PCode)primMap["pcode"].AsInteger();
            if (prim.PrimData.PCode == PCode.None)
                prim.PrimData.PCode = PCode.Prim;

            if (primMap.ContainsKey("name_values"))
            {
                string nameValue = primMap["name_values"].AsString();
                if (!String.IsNullOrEmpty(nameValue))
                {
                    string[] lines = nameValue.Split('\n');
                    prim.NameValues = new NameValue[lines.Length];

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (!String.IsNullOrEmpty(line))
                            prim.NameValues[i] = new NameValue(line);
                    }
                }
            }

            if (primMap.ContainsKey("textures"))
                prim.Textures = Primitive.TextureEntry.FromOSD(primMap["textures"]);
            if (primMap.ContainsKey("light"))
                prim.Light = Primitive.LightData.FromOSD(primMap["light"]);
            if (primMap.ContainsKey("flex"))
                prim.Flexible = Primitive.FlexibleData.FromOSD(primMap["flex"]);
            if (primMap.ContainsKey("sculpt"))
                prim.Sculpt = Primitive.SculptData.FromOSD(primMap["sculpt"]);

            OSDMap pathMap = (OSDMap)primMap["path"];
            prim.PrimData.PathBegin = (float)pathMap["begin"].AsReal();
            prim.PrimData.PathCurve = (PathCurve)pathMap["curve"].AsInteger();
            prim.PrimData.PathEnd = (float)pathMap["end"].AsReal();
            prim.PrimData.PathRadiusOffset = (float)pathMap["radius_offset"].AsReal();
            prim.PrimData.PathRevolutions = (float)pathMap["revolutions"].AsReal();
            prim.PrimData.PathScaleX = (float)pathMap["scale_x"].AsReal();
            prim.PrimData.PathScaleY = (float)pathMap["scale_y"].AsReal();
            prim.PrimData.PathShearX = (float)pathMap["shear_x"].AsReal();
            prim.PrimData.PathShearY = (float)pathMap["shear_y"].AsReal();
            prim.PrimData.PathSkew = (float)pathMap["skew"].AsReal();
            prim.PrimData.PathTaperX = (float)pathMap["taper_x"].AsReal();
            prim.PrimData.PathTaperY = (float)pathMap["taper_y"].AsReal();
            prim.PrimData.PathTwist = (float)pathMap["twist"].AsReal();
            prim.PrimData.PathTwistBegin = (float)pathMap["twist_begin"].AsReal();

            OSDMap profileMap = (OSDMap)primMap["profile"];
            prim.PrimData.ProfileBegin = (float)profileMap["begin"].AsReal();
            prim.PrimData.ProfileCurve = (ProfileCurve)profileMap["curve"].AsInteger();
            prim.PrimData.ProfileHole = (HoleType)profileMap["hole"].AsInteger();
            prim.PrimData.ProfileEnd = (float)profileMap["end"].AsReal();
            prim.PrimData.ProfileHollow = (float)profileMap["hollow"].AsReal();

            OSDMap propertiesMap = (OSDMap)primMap["properties"];
            prim.Properties = new Primitive.ObjectProperties();
            prim.Properties.AggregatePerms = (byte)propertiesMap["aggregate_perms"].AsInteger();
            prim.Properties.AggregatePermTextures = (byte)propertiesMap["aggregate_perms_textures"].AsInteger();
            prim.Properties.AggregatePermTexturesOwner = (byte)propertiesMap["aggregate_perms_textures_owner"].AsInteger();
            prim.Properties.Category = (ObjectCategory)propertiesMap["category"].AsInteger();
            prim.Properties.CreationDate = propertiesMap["creation_date"].AsDate();
            prim.Properties.CreatorID = propertiesMap["creator_id"].AsUUID();
            prim.Properties.Description = propertiesMap["description"].AsString();
            prim.Properties.FolderID = propertiesMap["folder_id"].AsUUID();
            prim.Properties.FromTaskID = propertiesMap["from_task_id"].AsUUID();
            prim.Properties.GroupID = prim.GroupID;
            prim.Properties.InventorySerial = (short)propertiesMap["inventory_serial"].AsInteger();
            prim.Properties.ItemID = propertiesMap["item_id"].AsUUID();
            prim.Properties.LastOwnerID = propertiesMap["last_owner_id"].AsUUID();
            prim.Properties.Name = propertiesMap["name"].AsString();
            prim.Properties.ObjectID = prim.ID;
            prim.Properties.OwnerID = prim.OwnerID;
            prim.Properties.OwnershipCost = propertiesMap["ownership_cost"].AsInteger();
            prim.Properties.Permissions = Permissions.FromOSD(propertiesMap["permissions"]);
            prim.Properties.SalePrice = propertiesMap["sale_price"].AsInteger();
            prim.Properties.SaleType = (SaleType)propertiesMap["sale_type"].AsInteger();
            prim.Properties.SitName = propertiesMap["sit_name"].AsString();
            prim.Properties.TouchName = propertiesMap["touch_name"].AsString();

            LLPrimitive obj = new LLPrimitive(prim, scene, mesher);

            obj.SitPosition = map["sit_position"].AsVector3();
            obj.SitRotation = map["sit_rotations"].AsQuaternion();
            obj.AttachmentPosition = map["attachment_position"].AsVector3();
            obj.AttachmentRotation = map["attachment_rotations"].AsQuaternion();
            obj.LastAttachmentPoint = (AttachmentPoint)map["last_attachment_point"].AsInteger();
            obj.BeforeAttachmentRotation = map["before_attachment_rotation"].AsQuaternion();
            obj.RotationAxis = map["rotation_axis"].AsVector3();
            obj.LinkNumber = map["link_number"].AsInteger();
            obj.RemoteScriptAccessPIN = map["remote_script_access_pin"].AsInteger();
            obj.Inventory.FromTaskInventoryAsset(map["inventory"].AsString());

            OSDArray buttons = map["pay_price_buttons"] as OSDArray;
            if (buttons != null)
            {
                obj.PayPriceButtons[0] = buttons[0].AsInteger();
                obj.PayPriceButtons[1] = buttons[1].AsInteger();
                obj.PayPriceButtons[2] = buttons[2].AsInteger();
                obj.PayPriceButtons[3] = buttons[3].AsInteger();
            }

            obj.PayPrice = map["pay_price"].AsInteger();

            OSDArray faceMedia = map["face_media"] as OSDArray;
            if (faceMedia != null)
            {
                obj.Prim.FaceMedia = new MediaEntry[faceMedia.Count];
                for (int i = 0; i < faceMedia.Count; i++)
                {
                    OSDMap entryMap = faceMedia[i] as OSDMap;
                    if (entryMap != null)
                        obj.Prim.FaceMedia[i] = MediaEntry.FromOSD(entryMap);
                }
            }
            obj.Prim.MediaVersion = map["media_version"].AsString();

            obj.m_lastUpdated = map["last_updated"].AsDate();
            if (obj.m_lastUpdated <= Utils.Epoch)
                obj.m_lastUpdated = DateTime.UtcNow;

            return obj;
        }

        public static OSDMap SerializeLinkset(LLPrimitive prim)
        {
            OSDMap linksetMap = new OSDMap();
            linksetMap[prim.LocalID.ToString()] = prim.GetOSD();

            ILinkable[] children = prim.GetChildren();
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    LLPrimitive child = children[i] as LLPrimitive;
                    if (child != null)
                        linksetMap[child.LocalID.ToString()] = child.GetOSD();
                }
            }

            return linksetMap;
        }

        public static IList<LLPrimitive> DeserializeLinkset(OSDMap linksetMap, IScene destinationScene, IPrimMesher mesher, bool forceNewIDs)
        {
            Dictionary<uint, LLPrimitive> prims = new Dictionary<uint, LLPrimitive>();
            Dictionary<uint, uint> oldToNewIDs = new Dictionary<uint, uint>();

            // Deserialize all of the prims and assign new IDs
            foreach (KeyValuePair<string, OSD> kvp in linksetMap)
            {
                uint localID;
                if (UInt32.TryParse(kvp.Key, out localID) && kvp.Value is OSDMap)
                {
                    OSDMap primMap = (OSDMap)kvp.Value;
                    LLPrimitive prim;
                    try
                    {
                        prim = LLPrimitive.FromOSD(primMap, destinationScene, mesher);

                        if (forceNewIDs || prim.Prim.ID == UUID.Zero || prim.Prim.LocalID == 0)
                        {
                            prim.Prim.ID = UUID.Random();
                            prim.Prim.LocalID = destinationScene.CreateLocalID();
                        }

                        // Clear any attachment point state data
                        prim.Prim.PrimData.AttachmentPoint = AttachmentPoint.Default;

                        prims[prim.LocalID] = prim;
                        oldToNewIDs[localID] = prim.Prim.LocalID;
                    }
                    catch (Exception ex)
                    {
                        m_log.WarnFormat("Invalid prim data in serialized linkset: {0}: {1}", ex.Message, primMap);
                    }
                }
                else
                {
                    m_log.WarnFormat("Invalid key/value pair in serialized linkset: \"{0}\":\"{1}\"", kvp.Key, kvp.Value);
                }
            }

            // Link all of the prims together and update the ParentIDs
            foreach (LLPrimitive prim in prims.Values)
            {
                if (prim.Prim.ParentID != 0)
                {
                    uint newLocalID;
                    if (oldToNewIDs.TryGetValue(prim.Prim.ParentID, out newLocalID))
                    {
                        prim.Prim.ParentID = newLocalID;
                        LLPrimitive parent = prims[newLocalID];
                        prim.SetParent(parent, false, false);
                    }
                    else
                    {
                        m_log.WarnFormat("Failed to locate parent prim {0} for child prim {1}, delinking child", prim.Prim.ParentID, prim.LocalID);
                        prim.Prim.ParentID = 0;
                    }
                }
            }

            return new List<LLPrimitive>(prims.Values);
        }

        #endregion Serialization Methods

        #region LLPrimitive Methods

        public uint GetCrc()
        {
            return Utils.DateTimeToUnixTime(m_lastUpdated);
        }

        public int GetNumberOfSides()
        {
            int ret = 0;
            bool hasCut;
            bool hasHollow;
            bool hasDimple;
            bool hasProfileCut;

            PrimType primType = this.Prim.Type;
            HasCutHollowDimpleProfileCut(primType, this.Prim, out hasCut, out hasHollow, out hasDimple, out hasProfileCut);

            switch (primType)
            {
                case PrimType.Box:
                    ret = 6;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.Cylinder:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.Prism:
                    ret = 5;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.Sphere:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasDimple) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.Torus:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.Tube:
                    ret = 4;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.Ring:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.Sculpt:
                    ret = 1;
                    break;
            }
            return ret;
        }

        private static void HasCutHollowDimpleProfileCut(PrimType primType, Primitive prim, out bool hasCut, out bool hasHollow,
            out bool hasDimple, out bool hasProfileCut)
        {
            if (primType == PrimType.Box || primType == PrimType.Cylinder || primType == PrimType.Prism)
                hasCut = (prim.PrimData.ProfileBegin > 0f) || (prim.PrimData.ProfileEnd < 1f);
            else
                hasCut = (prim.PrimData.PathBegin > 0f) || (prim.PrimData.PathEnd < 1f);

            hasHollow = prim.PrimData.ProfileHollow > 0f;
            hasDimple = (prim.PrimData.ProfileBegin > 0f) || (prim.PrimData.ProfileEnd < 1f); // taken from llSetPrimitiveParms
            hasProfileCut = hasDimple; // Is it the same thing?
        }

        #endregion LLPrimitive Methods

        private void UpdateVolume(PhysicsHull hull)
        {
            PhysicsType type = GetPhysicsType();
            Vector3 scale = Scale;

            switch (type)
            {
                case PhysicsType.Box:
                    m_volume = scale.X * scale.Y * scale.Z;
                    break;
                case PhysicsType.Sphere:
                    m_volume = 4.1887902f * scale.X * scale.Y * scale.Z;
                    break;
                case PhysicsType.Cylinder:
                    m_volume = Utils.PI * scale.X * scale.Y * scale.Z;
                    break;
                case PhysicsType.Mesh:
                    if (hull != null)
                        Util.GetMeshVolume((PhysicsMesh)hull, scale);
                    else
                        m_volume = scale.X * scale.Y * scale.Z;
                    break;
                case PhysicsType.ConvexHull:
                    // FIXME: Need to be able to build a convex hull from the point cloud to calculate 
                    // the volume
                    if (hull == null) hull = GetPhysicsHull();
                    m_volume = scale.X * scale.Y * scale.Z;
                    break;
                default:
                    m_log.Warn("LLPrimitive has an unhandled physics proxy type: " + type);
                    m_volume = scale.X * scale.Y * scale.Z;
                    break;
            }
        }

        private static bool HasBasicShape(Primitive.ConstructionData shape)
        {
            return
                shape.ProfileBegin == 0f && shape.ProfileEnd == 1f &&
                shape.ProfileHollow == 0f &&
                shape.PathTwistBegin == 0f && shape.PathTwist == 0f &&
                shape.PathBegin == 0f && shape.PathEnd == 1f &&
                shape.PathTaperX == 0f && shape.PathTaperY == 0f &&
                shape.PathScaleX == 1f && shape.PathScaleY == 1f &&
                shape.PathShearX == 0f && shape.PathShearY == 0f;
        }
    }
}
