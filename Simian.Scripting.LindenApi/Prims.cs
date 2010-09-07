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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Simian.Protocols.Linden;

namespace Simian.Scripting.Linden
{
    public partial class LindenApi : ISceneModule, IScriptApi
    {
        [ScriptMethod]
        public void llDie(IScriptInstance script)
        {
            script.Host.Scene.EntityRemove(this, script.Host);
        }

        [ScriptMethod]
        public UUID llGetOwner(IScriptInstance script)
        {
            return script.Host.OwnerID;
        }

        [ScriptMethod]
        public double llGetAlpha(IScriptInstance script, int side)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive obj = (LLPrimitive)script.Host;
                if (side >= 0 && side < GetNumberOfSides(obj))
                {
                    Color4 color = obj.Prim.Textures.GetFace((uint)side).RGBA;
                    return color.A;
                }
            }

            return 0.0;
        }

        [ScriptMethod]
        public Vector3 llGetColor(IScriptInstance script, int side)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive obj = (LLPrimitive)script.Host;
                if (side >= 0 && side < GetNumberOfSides(obj))
                {
                    Color4 color = obj.Prim.Textures.GetFace((uint)side).RGBA;
                    return new Vector3(color.R, color.G, color.B);
                }
            }

            return Vector3.Zero;
        }

        [ScriptMethod]
        public UUID llGetKey(IScriptInstance script)
        {
            return script.Host.ID;
        }

        [ScriptMethod]
        public int llGetLinkNumber(IScriptInstance script)
        {
            int linkNum = 0; // Return 0 if unlinked

            if (script.Host is ILinkable)
            {
                ILinkable link = (ILinkable)script.Host;
                if (link.Parent != null) // This is a child prim
                    linkNum = 1 + (int)(link.LocalID - link.Parent.LocalID);
            }

            return linkNum;
        }

        [ScriptMethod]
        public int llGetObjectPermMask(IScriptInstance script, int mask)
        {
           if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;

                switch (mask)
                {
                    case LSLConstants.MASK_BASE:
                        return (int)prim.Prim.Properties.Permissions.BaseMask;

                    case LSLConstants.MASK_EVERYONE:
                        return (int)prim.Prim.Properties.Permissions.EveryoneMask;

                    case LSLConstants.MASK_GROUP:
                        return (int)prim.Prim.Properties.Permissions.GroupMask;

                    case LSLConstants.MASK_NEXT:
                        return (int)prim.Prim.Properties.Permissions.NextOwnerMask;

                    case LSLConstants.MASK_OWNER:
                        return (int)prim.Prim.Properties.Permissions.OwnerMask;
                }

            }

            return 0;
        }

        [ScriptMethod]
        public string llAvatarOnSitTarget(IScriptInstance script)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;

                if (prim.SitPosition != Vector3.Zero)
                {
                    ILinkable[] children = ((ILinkable)script.Host).GetChildren();
                    for (int i = 0, len = children.Length; i < len; i++)
                    {
                        if (children[i] is LLAgent)
                        {
                            LLAgent childAgent = (LLAgent)children[i];
                            if (childAgent.RelativePosition == LLUtil.GetSitTarget(prim.SitPosition, childAgent.Scale))
                                return childAgent.ID.ToString();
                        }

                    }
                }
            }
            else
            {
                // TODO: Warning
            }

            return UUID.Zero.ToString();
        }

        [ScriptMethod]
        public int llGetNumberOfPrims(IScriptInstance script)
        {
            int num = 0;

            if (script.Host is ILinkable)
            {
                ILinkable linkable = (ILinkable)script.Host;
                if (linkable.Parent == null)
                    num = linkable.GetChildren().Length + 1;
                else
                    num = linkable.Parent.GetChildren().Length + 1;
            }

            return num;
        }

        [ScriptMethod]
        public Vector3 llGetRootPosition(IScriptInstance script)
        {
            return GetRootEntity(script.Host).RelativePosition;
        }

        [ScriptMethod]
        public Quaternion llGetRootRotation(IScriptInstance script)
        {
            return GetRootEntity(script.Host).RelativeRotation;
        }

        [ScriptMethod]
        public string llGetTexture(IScriptInstance script, int side)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive obj = (LLPrimitive)script.Host;
                if (side >= 0 && side < GetNumberOfSides(obj))
                {
                    return obj.Prim.Textures.GetFace((uint)side).TextureID.ToString();
                }
            }

            return UUID.Zero.ToString();
        }

        [ScriptMethod]
        public Quaternion llGetLocalRot(IScriptInstance script)
        {
            return script.Host.RelativeRotation;
        }

        [ScriptMethod]
        public Vector3 llGetPos(IScriptInstance script)
        {
            return script.Host.ScenePosition;
        }

        [ScriptMethod]
        public Quaternion llGetRot(IScriptInstance script)
        {
            return script.Host.SceneRotation;
        }

        [ScriptMethod]
        public Vector3 llGetScale(IScriptInstance script)
        {
            return script.Host.Scale;
        }

        [ScriptMethod]
        public Vector3 llGetAccel(IScriptInstance script)
        {
            if (script.Host is IPhysical)
                return ((IPhysical)script.Host).Acceleration;
            else
                return Vector3.Zero;
        }

        [ScriptMethod]
        public Vector3 llGetVel(IScriptInstance script)
        {
            if (script.Host is IPhysical)
                return ((IPhysical)script.Host).Velocity;
            else
                return Vector3.Zero;
        }

        [ScriptMethod]
        public double llGetMass(IScriptInstance script)
        {
            double mass = 0.0f;

            ISceneEntity host = script.Host;
            if (host is IPhysical)
            {
                ISceneEntity[] linkset = GetFullLinkset(host);

                for (int i = 0; i < linkset.Length; i++)
                {
                    if (linkset[i] is IPhysical)
                        mass += ((IPhysical)linkset[i]).GetMass();
                }
            }

            return mass;
        }

        [ScriptMethod]
        public Vector3 llGetCenterOfMass(IScriptInstance script)
        {
            ISceneEntity host = script.Host;
            ISceneEntity[] linkset = GetFullLinkset(host);

            // Find the center of mass for all triangles in all linked entities
            Vector3 center = Vector3.Zero;
            double volume = 0.0f;

            // TODO: Does this formula work for multiple meshes?
            // TODO: This is a very expensive algorithm (generates a mesh for every prim in the 
            // linkset and iterates over every triangle). Doesn't the physics engine already have 
            // this information?
            foreach (ISceneEntity entity in linkset)
            {
                if (entity is LLPrimitive)
                {
                    PhysicsMesh mesh = m_primMesher.GetPhysicsMesh((LLPrimitive)entity);

                    // Formula adapted from Stan Melax's algorithm: <http://www.melax.com/volint.html>
                    for (int i = 0; i < mesh.Indices.Length; i += 3)
                    {
                        Vector3 v0 = mesh.Vertices[mesh.Indices[i + 0]];
                        Vector3 v1 = mesh.Vertices[mesh.Indices[i + 1]];
                        Vector3 v2 = mesh.Vertices[mesh.Indices[i + 2]];

                        float det = Determinant3x3(v0, v1, v2);

                        center += (v0 + v1 + v2) * det;
                        volume += det;
                    }
                }
            }

            center /= (float)(volume * 4.0);
            return center * GetSceneTransform(GetRootEntity(host));
        }

        [ScriptMethod]
        public Vector3 llGetGeometricCenter(IScriptInstance script)
        {
            ISceneEntity host = script.Host;
            ISceneEntity[] linkset = GetFullLinkset(host);

            // Find the average position of all of the entities
            Vector3 center = Vector3.Zero;
            for (int i = 0; i < linkset.Length; i++)
                center += linkset[i].ScenePosition;
            center /= linkset.Length;

            // Return the relative offset of the geometric center from the 
            // root entity position
            return center - GetRootEntity(host).ScenePosition;
        }

        [ScriptMethod]
        public int llGetAttached(IScriptInstance script)
        {
            int point = -1;

            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;
                if (prim.Parent != null)
                    point = (int)prim.Prim.PrimData.AttachmentPoint;
            }
            else
            {
                // TODO: Allow non LLPrimitive attachments
                m_log.Warn("llGetAttached called from non LLPrimitive in object \"" + script.Host.Name + "\" at " + script.Host.ScenePosition);
            }

            return point;
        }

        //[ScriptMethod]
        //public List<object> llGetObjectDetails(IScriptInstance script, string objectID, List<object> details)
        //{
        //    List<object> ret = new List<object>();
        //    UUID key;
        //    ISceneEntity obj;

        //    if (UUID.TryParse(objectID, out key) && script.Host.Scene.TryGetEntity(key, out obj))
        //    {
        //        for (int i = 0, len = details.Count; i < len; i++)
        //        {
        //            int item = details.GetIntegerItem(i);
        //            switch (item)
        //            {
        //                case LSLConstants.OBJECT_CREATOR:
        //                    ret.Add(obj.CreatorID.ToString());
        //                    break;
        //                case LSLConstants.OBJECT_DESC:
        //                    if (obj is LLPrimitive)
        //                        ret.Add(((LLPrimitive)obj).Prim.Properties.Description);
        //                    else
        //                        ret.Add(String.Empty);
        //                    break;
        //                case LSLConstants.OBJECT_GROUP:
        //                    ret.Add(obj.GroupID.ToString());
        //                    break;
        //                case LSLConstants.OBJECT_NAME:
        //                    ret.Add(obj.Name);
        //                    break;
        //                case LSLConstants.OBJECT_OWNER:
        //                    ret.Add(obj.OwnerID.ToString());
        //                    break;
        //                case LSLConstants.OBJECT_POS:
        //                    ret.Add(obj.ScenePosition); // TODO: Verify that this matches LSL
        //                    break;
        //                case LSLConstants.OBJECT_ROT:
        //                    ret.Add(obj.SceneRotation); // TODO: Verify that this matches LSL
        //                    break;
        //                case LSLConstants.OBJECT_VELOCITY:
        //                    if (obj is IPhysical)
        //                        ret.Add(((IPhysical)obj).Velocity);
        //                    else
        //                        ret.Add(Vector3.Zero);
        //                    break;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // TODO: Return status of non LLPrimitive objects
        //        m_log.Warn("llGetObjectDetails called for non LLPrimitive target in object \"" + script.Host.Name + "\" at " + script.Host.ScenePosition);
        //    }

        //    return ret;
        //}

        [ScriptMethod]
        public string llGetObjectName(IScriptInstance script)
        {
            return script.Host.Name;
        }

        [ScriptMethod]
        public string llGetObjectDesc(IScriptInstance script)
        {
            string desc = String.Empty;

            if (script.Host is LLPrimitive)
                desc = ((LLPrimitive)script.Host).Prim.Properties.Description;

            return desc;
        }

        [ScriptMethod]
        public int llGetStatus(IScriptInstance script, int flag)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;

                switch (flag)
                {
                    case LSLConstants.STATUS_ROTATE_X:
                        return prim.AllowRotateX ? 1 : 0;
                    case LSLConstants.STATUS_ROTATE_Y:
                        return prim.AllowRotateY ? 1 : 0;
                    case LSLConstants.STATUS_ROTATE_Z:
                        return prim.AllowRotateZ ? 1 : 0;
                    case LSLConstants.STATUS_BLOCK_GRAB:
                        return prim.BlockGrab ? 1 : 0;
                    default:
                        bool value = ((int)prim.Prim.Flags & flag) == flag;

                        if (flag == LSLConstants.STATUS_PHYSICS)
                            prim.DynamicsEnabled = value;

                        return value ? 1 : 0;
                }        
            }

            return 0;
        }

        [ScriptMethod]
        public int llSameGroup(IScriptInstance script, UUID agent)
        {
            ISceneEntity entity;
            if (script.Host.Scene.TryGetEntity(agent, out entity) && entity.GroupID == script.Host.GroupID)
                return 1;

            return 0;
        }

        [ScriptMethod]
        public void llSetAlpha(IScriptInstance script, double alpha, int side)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive obj = (LLPrimitive)script.Host;
                int sides = GetNumberOfSides(obj);
                float newAlpha = (float)Utils.Clamp(alpha, 0.0, 1.0);

                if (side >= 0 && side < sides)
                {
                    // Get or create the requested face and update the alpha
                    Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                    Color4 faceColor = face.RGBA;
                    faceColor.A = newAlpha;
                    face.RGBA = faceColor;

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                    script.AddSleepMS(200);

                    if (m_lslScriptEngine != null)
                        m_lslScriptEngine.PostObjectEvent(obj.ID, "changed", new object[] { LSLConstants.CHANGED_COLOR }, new DetectParams[0]);
                }
                else if (side == LSLConstants.ALL_SIDES)
                {
                    // Change all of the faces
                    for (uint i = 0; i < sides; i++)
                    {
                        Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                        if (face != null)
                        {
                            Color4 faceColor = face.RGBA;
                            faceColor.A = newAlpha;
                            face.RGBA = faceColor;
                        }
                    }

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                    script.AddSleepMS(200);

                    if (m_lslScriptEngine != null)
                        m_lslScriptEngine.PostObjectEvent(obj.ID, "changed", new object[] { LSLConstants.CHANGED_COLOR }, new DetectParams[0]);
                }
            }
        }

        [ScriptMethod]
        public void llSetColor(IScriptInstance script, Vector3 color, int side)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive obj = (LLPrimitive)script.Host;
                int sides = GetNumberOfSides(obj);

                // Sanitize the color input
                color.X = Utils.Clamp(color.X, 0.0f, 1.0f);
                color.Y = Utils.Clamp(color.Y, 0.0f, 1.0f);
                color.Z = Utils.Clamp(color.Z, 0.0f, 1.0f);

                if (side >= 0 && side < sides)
                {
                    // Get or create the requested face and update the color
                    Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                    face.RGBA = new Color4(color.X, color.Y, color.Z, face.RGBA.A);

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                    script.AddSleepMS(200);

                    if (m_lslScriptEngine != null)
                        m_lslScriptEngine.PostObjectEvent(obj.ID, "changed", new object[] { LSLConstants.CHANGED_COLOR }, new DetectParams[0]);
                }
                else if (side == LSLConstants.ALL_SIDES)
                {
                    // Change all of the faces
                    for (uint i = 0; i < sides; i++)
                    {
                        Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                        if (face != null)
                            face.RGBA = new Color4(color.X, color.Y, color.Z, face.RGBA.A);
                    }

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                    script.AddSleepMS(200);

                    if (m_lslScriptEngine != null)
                        m_lslScriptEngine.PostObjectEvent(obj.ID, "changed", new object[] { LSLConstants.CHANGED_COLOR }, new DetectParams[0]);
                }
            }
        }

        [ScriptMethod]
        public void llSetClickAction(IScriptInstance script, int action)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;

                ClickAction clickAction = (ClickAction)action;

                switch(clickAction)
                {
                    case ClickAction.Buy:
                    case ClickAction.OpenMedia:
                    case ClickAction.OpenTask:
                    case ClickAction.Pay:
                    case ClickAction.PlayMedia:
                    case ClickAction.Sit:
                    case ClickAction.Touch:
                        prim.Prim.ClickAction = ClickAction.Buy;
                        break;
                    default:
                        prim.Prim.ClickAction = ClickAction.Touch;
                        break;
                }               
            }
        }

        [ScriptMethod]
        public void llSetSoundRadius(IScriptInstance script, float radius)
        {
            if (script.Host is LLPrimitive)
                ((LLPrimitive)script.Host).Prim.SoundRadius = radius;
        }

        [ScriptMethod]
        public void llSetTexture(IScriptInstance script, string texture, int side)
        {
            if (script.Host is LLPrimitive)
                SetTexture(script, (LLPrimitive)script.Host, texture, side);
            script.AddSleepMS(200);

            if (m_lslScriptEngine != null)
                m_lslScriptEngine.PostObjectEvent(script.Host.ID, "changed", new object[] { LSLConstants.CHANGED_TEXTURE }, new DetectParams[0]);
        }

        [ScriptMethod]
        public void llSetObjectName(IScriptInstance script, string name)
        {
            script.Host.Name = name;
        }

        [ScriptMethod]
        public void llSettObjectDesc(IScriptInstance script, string desc)
        {
            if (script.Host is LLPrimitive)
                ((LLPrimitive)script.Host).Prim.Properties.Description = desc;
            else
            {
                // TODO: Warning
            }
        }

        //[ScriptMethod]
        //public void llSetPayPrice(IScriptInstance script, int defaultPrice, List<object> buttons)
        //{
        //    if (script.Host is LLPrimitive)
        //    {
        //        LLPrimitive prim = (LLPrimitive)script.Host;
        //        prim.PayPrice = defaultPrice;
        //        prim.PayPriceButtons[0] = buttons.Count > 0 ? buttons.GetIntegerItem(0) : 0;
        //        prim.PayPriceButtons[1] = buttons.Count > 1 ? buttons.GetIntegerItem(1) : 0;
        //        prim.PayPriceButtons[2] = buttons.Count > 2 ? buttons.GetIntegerItem(2) : 0;
        //        prim.PayPriceButtons[3] = buttons.Count > 3 ? buttons.GetIntegerItem(3) : 0;
        //    }
        //    else
        //    {
        //        // TODO: Warning
        //    }
        //}

        [ScriptMethod]
        public void llSetPos(IScriptInstance script, Vector3 pos)
        {
            SetPos(script.Host, pos);
            script.AddSleepMS(200);
        }

        [ScriptMethod]
        public void llSetRot(IScriptInstance script, Quaternion rot)
        {
            SetRot(script.Host, rot);
            script.AddSleepMS(200);
        }

        [ScriptMethod]
        public void llSetScale(IScriptInstance script, Vector3 scale)
        {
            script.Host.Scale = new Vector3(
                Utils.Clamp(scale.X, 0.01f, 10f),
                Utils.Clamp(scale.Y, 0.01f, 10f),
                Utils.Clamp(scale.Z, 0.01f, 10f));

            script.Host.Scene.EntityAddOrUpdate(this, script.Host, UpdateFlags.Scale, 0);
            script.AddSleepMS(200);
        }

        [ScriptMethod]
        public void llSetLocalRot(IScriptInstance script, Quaternion rot)
        {
            script.Host.RelativeRotation = rot;
            script.Host.Scene.EntityAddOrUpdate(this, script.Host, UpdateFlags.Rotation, 0);
            script.AddSleepMS(200);
        }

        [ScriptMethod]
        public void llSetStatus(IScriptInstance script, int flags, bool enabled)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;

                UpdateFlags updateFlags = 0;

                if ((flags & LSLConstants.STATUS_CAST_SHADOWS) == LSLConstants.STATUS_CAST_SHADOWS)
                {
                    if (enabled)
                        prim.Prim.Flags |= PrimFlags.CastShadows;
                    else
                        prim.Prim.Flags &= ~PrimFlags.CastShadows;

                    updateFlags |= UpdateFlags.FullUpdate;
                }

                if ((flags & LSLConstants.STATUS_PHANTOM) == LSLConstants.STATUS_PHANTOM)
                {
                    if (enabled)
                        prim.Prim.Flags |= PrimFlags.Phantom;
                    else
                        prim.Prim.Flags &= ~PrimFlags.Phantom;

                    updateFlags |= UpdateFlags.PhantomStatus;
                }

                if ((flags & LSLConstants.STATUS_BLOCK_GRAB) == LSLConstants.STATUS_BLOCK_GRAB)
                    prim.BlockGrab = enabled;

                if ((flags & LSLConstants.STATUS_DIE_AT_EDGE) == LSLConstants.STATUS_DIE_AT_EDGE)
                {
                    if (enabled)
                        prim.Prim.Flags |= PrimFlags.DieAtEdge;
                    else
                        prim.Prim.Flags &= ~PrimFlags.DieAtEdge;
                }

                if ((flags & LSLConstants.STATUS_PHYSICS) == LSLConstants.STATUS_PHANTOM)
                {
                    prim.DynamicsEnabled = enabled;

                    updateFlags |= UpdateFlags.PhysicalStatus;
                }

                if ((flags & LSLConstants.STATUS_RETURN_AT_EDGE) == LSLConstants.STATUS_RETURN_AT_EDGE)
                {
                    if (enabled)
                        prim.Prim.Flags |= PrimFlags.ReturnAtEdge;
                    else
                        prim.Prim.Flags &= ~PrimFlags.ReturnAtEdge;
                }

                if ((flags & LSLConstants.STATUS_ROTATE_X) == LSLConstants.STATUS_ROTATE_X)
                    prim.AllowRotateX = enabled;

                if ((flags & LSLConstants.STATUS_ROTATE_Y) == LSLConstants.STATUS_ROTATE_Y)
                    prim.AllowRotateY = enabled;

                if ((flags & LSLConstants.STATUS_ROTATE_Z) == LSLConstants.STATUS_ROTATE_Z)
                    prim.AllowRotateZ = enabled;

                if ((flags & LSLConstants.STATUS_SANDBOX) == LSLConstants.STATUS_SANDBOX)
                {
                    if (enabled)
                        prim.Prim.Flags |= PrimFlags.Sandbox;
                    else
                        prim.Prim.Flags &= ~PrimFlags.Sandbox;
                }

                script.Host.Scene.EntityAddOrUpdate(this, prim, updateFlags, 0);
            }
        }

        [ScriptMethod]
        public void llSetText(IScriptInstance script, string text, Vector3 color, float alpha)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;
                prim.Prim.Text = text;
                prim.Prim.TextColor = new Color4(1 - color.X, 1 - color.Y, 1 - color.Z, alpha);

                script.Host.Scene.EntityAddOrUpdate(this, script.Host, UpdateFlags.FullUpdate, 0);

                script.AddSleepMS(200);
            }
            else
            {
                // TODO: Warning/support non LLPrimitives
            }
        }

        [ScriptMethod]
        public void llSitTarget(IScriptInstance script, Vector3 position, Quaternion rotation)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;
                prim.SitPosition = position;
                prim.SitRotation = rotation;
            }
            else
            {
                // TODO: Warning/support non LLPrimitives
            }
        }

        [ScriptMethod]
        public void llRezAtRoot(IScriptInstance script, string inventory, Vector3 position, Vector3 vel, Quaternion rot, int param)
        {
            RezObject(script, inventory, position, vel, rot, param, true);
        }

        [ScriptMethod]
        public void llRezObject(IScriptInstance script, string inventory, Vector3 position, Vector3 vel, Quaternion rot, int param)
        {
            RezObject(script, inventory, position, vel, rot, param, false);
        }

        #region Helpers

        private ISceneEntity GetRootEntity(ISceneEntity entity)
        {
            if (entity is ILinkable && ((ILinkable)entity).Parent != null)
                return ((ILinkable)entity).Parent;

            return entity;
        }

        private ISceneEntity[] GetFullLinkset(ISceneEntity entity)
        {
            // Get the root of this linkset
            ISceneEntity parent = GetRootEntity(entity);
            if (parent is ILinkable)
            {
                // Get all of the children
                ILinkable[] children = ((ILinkable)parent).GetChildren();

                // Build a linkset array containing the root and the children
                ISceneEntity[] linkset = new ISceneEntity[children.Length + 1];
                linkset[0] = parent;
                for (int i = 0; i < children.Length; i++)
                    linkset[i + 1] = children[i];

                return linkset;
            }

            return new ISceneEntity[] { entity };
        }

        private void RezObject(IScriptInstance script, string inventory, Vector3 position, Vector3 vel, Quaternion rot, int param, bool atRoot)
        {
            // TODO: Test to make sure this actually rezzes from the root, and get the atRoot param working

            // Can't do this without an IAssetClient
            if (m_assetClient == null)
                return;

            // Sanity check the input rotation
            if (Double.IsNaN(rot.X) || Double.IsNaN(rot.Y) || Double.IsNaN(rot.Z) || Double.IsNaN(rot.W))
                return;

            // Sanity check the distance, silently fail at > 10m
            float dist = Vector3.Distance(script.Host.ScenePosition, position);
            if (dist > 10.0f)
                return;

            if (script.Host is LLPrimitive)
            {
                LLPrimitive obj = (LLPrimitive)script.Host;
                LLInventoryTaskItem item = obj.Inventory.FindItem(delegate(LLInventoryTaskItem match) { return match.Name == inventory; });

                if (item != null)
                {
                    // Make sure this is an object
                    if (item.InventoryType != InventoryType.Object)
                    {
                        llSay(script, 0, "Unable to create requested object. Object is missing from database.");
                        return;
                    }

                    // Fetch the serialized linkset asset
                    Asset linksetAsset;
                    if (!m_assetClient.TryGetAsset(item.AssetID, LLUtil.LLAssetTypeToContentType((int)AssetType.Object), out linksetAsset))
                    {
                        llSay(script, 0, "Unable to create requested object. Object is missing from database.");
                        return;
                    }

                    // Deserialize the asset to LLSD
                    OSDMap linksetMap = null;
                    try { linksetMap = OSDParser.Deserialize(linksetAsset.Data) as OSDMap; }
                    catch (Exception ex) { m_log.Error("Failed to deserialize linkset from asset " + linksetAsset.ID + ": " + ex.Message); }

                    if (linksetMap == null)
                    {
                        llSay(script, 0, "Unable to create requested object. Object is corrupted in database.");
                        return;
                    }

                    // Deserialize the linkset
                    IList<LLPrimitive> linkset = LLPrimitive.DeserializeLinkset(linksetMap, obj.Scene, m_primMesher, true);

                    Vector3 velocity = vel;
                    Quaternion rotation = rot;
                    float velMag = velocity.Length();
                    float mass = (float)llGetMass(script);

                    // Rez the parent(s) first
                    for (int i = 0; i < linkset.Count; i++)
                    {
                        LLPrimitive prim = linkset[i];
                        if (prim.Parent == null)
                        {
                            // Objects rezzed with this method are DieAtEdge by default
                            prim.Prim.Flags |= PrimFlags.DieAtEdge;

                            // Set the position, rotation and velocity of the root prim in the scene
                            prim.RelativePosition = position;
                            prim.RelativeRotation = rotation;
                            if (prim.Prim.Flags.HasFlag(PrimFlags.Physics))
                            {
                                prim.FallStart = Environment.TickCount;
                                prim.Velocity = velocity;
                            }

                            obj.Scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
                            m_log.Debug("Deserialized root prim " + prim.ID + " (" + prim.LocalID + ") from task inventory");
                        }
                    }

                    // Rez the children
                    for (int i = 0; i < linkset.Count; i++)
                    {
                        LLPrimitive prim = linkset[i];
                        if (prim.Parent != null)
                            obj.Scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
                    }

                    // FIXME: Post an object_rez event

                    if (obj.Prim.Flags.HasFlag(PrimFlags.Physics))
                    {
                        obj.FallStart = Environment.TickCount;
                        // FIXME: Recoil
                        //llApplyImpulse(script, new lsl_vector(velocity.X * mass, velocity.Y * mass, velocity.Z * mass), 0);
                    }

                    // Variable script delay (http://wiki.secondlife.com/wiki/LSL_Delay)
                    script.AddSleepMS((int)((mass * velMag) * 0.1f));
                    script.AddSleepMS(200);
                }
                else
                {
                    llSay(script, 0, "Could not find object " + inventory);
                }
            }
        }

        private void SetPos(ISceneEntity obj, Vector3 pos)
        {
            // Ignore if this obj is physical
            if (obj is IPhysical && ((IPhysical)obj).DynamicsEnabled)
                return;

            // Ignore if distance > 10m (http://wiki.secondlife.com/wiki/LlSetPos)
            if (Vector3.Distance(obj.RelativePosition, pos) > 10.0f)
                return;

            if (Vector3.Distance(obj.RelativePosition, pos) <= 10.0f)
            {
                // Test if this is a root prim
                if (!(obj is ILinkable) || ((ILinkable)obj).Parent == null)
                {
                    // Get the terrain height at the target position
                    float terrainHeight = 0.0f;
                    if (m_terrain != null)
                        terrainHeight = m_terrain.GetTerrainHeightAt(pos.X, pos.Y);

                    // Clamp the target position
                    pos.Z = Utils.Clamp(pos.Z, terrainHeight, 4096.0f);
                }
            }

            obj.RelativePosition = pos;
            obj.Scene.EntityAddOrUpdate(this, obj, UpdateFlags.Position, 0);
        }

        private void SetRot(ISceneEntity obj, Quaternion rot)
        {
            if (obj is ILinkable && ((ILinkable)obj).Parent != null)
            {
                // This is a child entity. Offset this rotation by the root rotation (http://wiki.secondlife.com/wiki/LlSetRot)
                ILinkable parent = ((ILinkable)obj).Parent;
                obj.RelativeRotation = parent.RelativeRotation * rot;
            }
            else
            {
                obj.RelativeRotation = rot;
            }

            obj.Scene.EntityAddOrUpdate(this, obj, UpdateFlags.Rotation, 0);
        }

        private void SetTexture(IScriptInstance script, LLPrimitive obj, string texture, int side)
        {
            // "texture" may be an AssetID or the name of a texture in this object's task inventory
            UUID textureID = KeyOrName(script, texture, AssetType.Texture);

            // Can't set texture to UUID.Zero
            if (textureID == UUID.Zero)
                return;

            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Change one face
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.TextureID = textureID;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                        face.TextureID = textureID;
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private static Matrix4 GetSceneTransform(ISceneEntity entity)
        {
            Matrix4 transform = Matrix4.Identity;

            transform *= Matrix4.CreateScale(entity.Scale);
            transform *= Matrix4.CreateFromQuaternion(entity.RelativeRotation);
            transform *= Matrix4.CreateTranslation(entity.RelativePosition);

            if (entity is ILinkable)
            {
                ILinkable parent = ((ILinkable)entity).Parent;
                if (parent != null)
                {
                    // Apply parent rotation and translation
                    transform *= Matrix4.CreateFromQuaternion(parent.RelativeRotation);
                    transform *= Matrix4.CreateTranslation(parent.RelativePosition);
                }
            }

            return transform;
        }

        private static int GetNumberOfSides(LLPrimitive obj)
        {
            int sides = obj.GetNumberOfSides();

            if (obj.Prim.Type == PrimType.Sphere && obj.Prim.PrimData.ProfileHollow > 0f)
            {
                // Account for an LSL bug where this reports four sides instead of two
                sides += 2;
            }

            return sides;
        }

        private static float Determinant3x3(Vector3 r0, Vector3 r1, Vector3 r2)
        {
            // Calculate the determinant of a 3x3 matrix using Sarrus' method
            return
                 r0.X * r1.Y * r2.Z
               + r0.Y * r1.Z * r2.X
               + r0.Z * r1.X * r2.Y
               - r0.Z * r1.Y * r2.X
               - r1.Z * r2.Y * r0.X
               - r2.Z * r0.Y * r1.X;
        }

        #endregion Helpers

        #region Inventory Helpers

        /// <summary>
        /// Lookup the given script's AssetID
        /// </summary>
        /// <param name="script">Calling script</param>
        /// <returns>AssetID, or UUID.Zero if something went wrong</returns>
        protected UUID InventorySelf(IScriptInstance script)
        {
            if (script.Host is LLPrimitive)
            {
                PrimInventory inventory = ((LLPrimitive)script.Host).Inventory;

                LLInventoryTaskItem item = inventory.FindItem(delegate(LLInventoryTaskItem match) { return match.ID == script.ID; });
                if (item != null)
                    return item.AssetID;
            }

            return UUID.Zero;
        }

        /// <summary>
        /// Lookup a task inventory item AssetID by item name, restricting
        /// lookups to the given asset type
        /// </summary>
        /// <param name="script">Calling script</param>
        /// <param name="name">Name of an inventory item</param>
        /// <param name="assetType">Asset type to restrict lookups to</param>
        /// <returns>AssetID, or UUID.Zero if the item was not found in prim
        /// inventory</returns>
        private UUID InventoryKey(IScriptInstance script, string name, AssetType assetType)
        {
            if (script.Host is LLPrimitive)
            {
                PrimInventory inventory = ((LLPrimitive)script.Host).Inventory;

                LLInventoryTaskItem item = inventory.FindItem(delegate(LLInventoryTaskItem match) { return match.Name == name; });
                if (item != null && item.AssetType == assetType)
                    return item.AssetID;
            }

            return UUID.Zero;
        }

        /// <summary>
        /// Lookup a task inventory item AssetID by item name
        /// </summary>
        /// <param name="script">Calling script</param>
        /// <param name="name">Name of an inventory item</param>
        /// <returns>AssetID, or UUID.Zero if the item was not found in prim
        /// inventory</returns>
        private UUID InventoryKey(IScriptInstance script, string name)
        {
            if (script.Host is LLPrimitive)
            {
                PrimInventory inventory = ((LLPrimitive)script.Host).Inventory;

                LLInventoryTaskItem item = inventory.FindItem(delegate(LLInventoryTaskItem match) { return match.Name == name; });
                if (item != null)
                    return item.AssetID;
            }

            return UUID.Zero;
        }

        /// <summary>
        /// Parse an AssetID string or lookup an item's AssetID by item name
        /// </summary>
        /// <param name="script">Calling script</param>
        /// <param name="k">A valid AssetID or the name of an inventory item</param>
        /// <returns>AssetID, or UUID.Zero if the key was invalid and the item 
        /// was not found in prim inventory</returns>
        private UUID KeyOrName(IScriptInstance script, string k)
        {
            UUID assetID;

            if (UUID.TryParse(k, out assetID))
                return assetID;
            else
                return InventoryKey(script, k);
        }

        /// <summary>
        /// Parse an AssetID string or lookup an item's AssetID by item name,
        /// restricting lookups to the given asset type
        /// </summary>
        /// <param name="script">Calling script</param>
        /// <param name="k">A valid AssetID or the name of an inventory item</param>
        /// <param name="assetType">Asset type to restrict lookups to</param>
        /// <returns>AssetID, or UUID.Zero if the key was invalid and the item 
        /// was not found in prim inventory</returns>
        private UUID KeyOrName(IScriptInstance script, string k, AssetType assetType)
        {
            UUID assetID;

            if (UUID.TryParse(k, out assetID))
                return assetID;
            else
                return InventoryKey(script, k, assetType);
        }

        #endregion Inventory Helpers
    }
}
