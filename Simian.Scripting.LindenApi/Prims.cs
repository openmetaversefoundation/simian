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
        public float llGetAlpha(IScriptInstance script, int side)
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

            return 0.0f;
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
        public Vector3 llGetPos(IScriptInstance script)
        {
            return script.Host.ScenePosition;
        }

        [ScriptMethod]
        public Vector3 llGetLocalPos(IScriptInstance script)
        {
            return script.Host.RelativePosition;
        }

        [ScriptMethod]
        public Quaternion llGetRot(IScriptInstance script)
        {
            return script.Host.SceneRotation;
        }

        [ScriptMethod]
        public Quaternion llGetLocalRot(IScriptInstance script)
        {
            return script.Host.RelativeRotation;
        }

        [ScriptMethod]
        public string llGetScriptName(IScriptInstance script)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return String.Empty;

            LLInventoryTaskItem scriptItem = prim.Inventory.FindItem(item => item.ID == script.ID);
            if (scriptItem != null)
                return scriptItem.Name;

            return String.Empty;
        }

        [ScriptMethod]
        public string llGetLinkKey(IScriptInstance script, int linknumber)
        {
            if (linknumber == LSLConstants.LINK_ROOT || linknumber == 0)
                return GetRootEntity(script.Host).ID.ToString();

            ISceneEntity[] children = GetLinkParts(script.Host, LSLConstants.LINK_ALL_CHILDREN);
            for (int i = 0; i < children.Length; i++)
            {
                ISceneEntity child = children[i];
                if (child is LLPrimitive && ((LLPrimitive)child).LinkNumber == linknumber)
                    return child.ID.ToString();
            }

            return UUID.Zero.ToString();
        }

        public string llGetLinkName(IScriptInstance script, int linknumber)
        {
            ILinkable entity = script.Host as ILinkable;
            if (entity == null)
                return UUID.Zero.ToString();

            // Simplest case, this prims link number
            if (entity.LinkNumber == linknumber)
                return entity.Name;

            // Single prim
            if (entity.LinkNumber == 0)
            {
                if (linknumber == 0)
                    return entity.Name;
                else
                    return UUID.Zero.ToString();
            }
            
            if (entity == GetRootEntity(entity))
            {
                // Special behavior for when we are the root prim
                if (linknumber == 0)
                    return UUID.Zero.ToString();
                else if (linknumber < 0)
                    linknumber = 2;
            }
            else
            {
                // If we're a child prim and link number 0, 1, or any negative number was requested
                // return the root prim name
                if (linknumber <= 1)
                    return GetRootEntity(entity).Name;
            }

            System.Diagnostics.Debug.Assert(linknumber > 0);

            ISceneEntity[] linkset = GetLinkParts(script.Host, LSLConstants.LINK_SET);
            for (int i = 0; i < linkset.Length; i++)
            {
                ISceneEntity linksetEntity = linkset[i];
                if (linksetEntity is ILinkable && ((ILinkable)linksetEntity).LinkNumber == linknumber)
                    return linksetEntity.Name;
            }

            return UUID.Zero.ToString();
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
        public float llGetMass(IScriptInstance script)
        {
            float mass = 0.0f;

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
            float volume = 0.0f;

            foreach (ISceneEntity entity in linkset)
            {
                if (entity is LLPrimitive)
                {
                    LLPrimitive prim = (LLPrimitive)entity;

                    BasicMesh mesh = prim.GetBasicMesh();

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

            return point;
        }

        [ScriptMethod]
        public Vector3 llGetTextureOffset(IScriptInstance script, int face)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return Vector3.Zero;

            Primitive.TextureEntryFace teFace = prim.Prim.Textures.GetFace((uint)face);
            return new Vector3(teFace.OffsetU, teFace.OffsetV, 0f);
        }

        [ScriptMethod]
        public float llGetTextureRot(IScriptInstance script, int face)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return 0f;

            Primitive.TextureEntryFace teFace = prim.Prim.Textures.GetFace((uint)face);
            return teFace.Rotation;
        }

        [ScriptMethod]
        public Vector3 llGetTextureScale(IScriptInstance script, int face)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return Vector3.Zero;

            Primitive.TextureEntryFace teFace = prim.Prim.Textures.GetFace((uint)face);
            return new Vector3(teFace.RepeatU, teFace.RepeatV, 0f);
        }

        [ScriptMethod]
        public int llGetNumberOfSides(IScriptInstance script)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return 0;
            return GetNumberOfSides(prim);
        }

        [ScriptMethod]
        public object[] llGetObjectDetails(IScriptInstance script, string objectID, object[] details)
        {
            List<object> ret = new List<object>();
            UUID key;
            ISceneEntity obj;

            if (UUID.TryParse(objectID, out key) && script.Host.Scene.TryGetEntity(key, out obj))
            {
                for (int i = 0, len = details.Length; i < len; i++)
                {
                    int item = llList2Integer(script, details, i);
                    switch (item)
                    {
                        case LSLConstants.OBJECT_CREATOR:
                            ret.Add(obj.CreatorID.ToString());
                            break;
                        case LSLConstants.OBJECT_DESC:
                            if (obj is LLPrimitive)
                                ret.Add(((LLPrimitive)obj).Prim.Properties.Description);
                            else
                                ret.Add(String.Empty);
                            break;
                        case LSLConstants.OBJECT_GROUP:
                            ret.Add(obj.GroupID.ToString());
                            break;
                        case LSLConstants.OBJECT_NAME:
                            ret.Add(obj.Name);
                            break;
                        case LSLConstants.OBJECT_OWNER:
                            ret.Add(obj.OwnerID.ToString());
                            break;
                        case LSLConstants.OBJECT_POS:
                            ret.Add(obj.ScenePosition); // TODO: Verify that this matches LSL
                            break;
                        case LSLConstants.OBJECT_ROT:
                            ret.Add(obj.SceneRotation); // TODO: Verify that this matches LSL
                            break;
                        case LSLConstants.OBJECT_VELOCITY:
                            if (obj is IPhysical)
                                ret.Add(((IPhysical)obj).Velocity);
                            else
                                ret.Add(Vector3.Zero);
                            break;
                    }
                }
            }

            return ret.ToArray();
        }

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
        public void llSetAlpha(IScriptInstance script, float alpha, int side)
        {
            script.AddSleepMS(200);

            if (script.Host is LLPrimitive)
            {
                SetAlpha((LLPrimitive)script.Host, alpha, side);
                if (m_lslScriptEngine != null)
                    m_lslScriptEngine.PostObjectEvent(script.Host.ID, "changed", new object[] { LSLConstants.CHANGED_COLOR }, null);
            }
        }

        [ScriptMethod]
        public void llSetColor(IScriptInstance script, Vector3 color, int side)
        {
            script.AddSleepMS(200);

            if (script.Host is LLPrimitive)
            {
                SetColor((LLPrimitive)script.Host, color, side);
                if (m_lslScriptEngine != null)
                    m_lslScriptEngine.PostObjectEvent(script.Host.ID, "changed", new object[] { LSLConstants.CHANGED_COLOR }, null);
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
                m_lslScriptEngine.PostObjectEvent(script.Host.ID, "changed", new object[] { LSLConstants.CHANGED_TEXTURE }, null);
        }

        [ScriptMethod]
        public void llScaleTexture(IScriptInstance script, float u, float v, int side)
        {
            if (script.Host is LLPrimitive)
                ScaleTexture(script, (LLPrimitive)script.Host, u, v, side);
            script.AddSleepMS(200);

            if (m_lslScriptEngine != null)
                m_lslScriptEngine.PostObjectEvent(script.Host.ID, "changed", new object[] { LSLConstants.CHANGED_TEXTURE }, null);
        }

        [ScriptMethod]
        public void llOffsetTexture(IScriptInstance script, float u, float v, int side)
        {
            if (script.Host is LLPrimitive)
                OffsetTexture(script, (LLPrimitive)script.Host, u, v, side);
            script.AddSleepMS(200);

            if (m_lslScriptEngine != null)
                m_lslScriptEngine.PostObjectEvent(script.Host.ID, "changed", new object[] { LSLConstants.CHANGED_TEXTURE }, null);
        }

        [ScriptMethod]
        public void llRotateTexture(IScriptInstance script, float angle, int side)
        {
            if (script.Host is LLPrimitive)
                RotateTexture(script, (LLPrimitive)script.Host, angle, side);
            script.AddSleepMS(200);

            if (m_lslScriptEngine != null)
                m_lslScriptEngine.PostObjectEvent(script.Host.ID, "changed", new object[] { LSLConstants.CHANGED_TEXTURE }, null);
        }

        [ScriptMethod]
        public void llSetObjectName(IScriptInstance script, string name)
        {
            SetObjectName(script.Host, name);
        }

        [ScriptMethod]
        public void llSetObjectDesc(IScriptInstance script, string desc)
        {
            SetObjectDesc(script.Host, desc);
        }

        [ScriptMethod]
        public void llSetPayPrice(IScriptInstance script, int defaultPrice, object[] buttons)
        {
            if (script.Host is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)script.Host;
                prim.PayPrice = defaultPrice;
                prim.PayPriceButtons[0] = llList2Integer(script, buttons, 0);
                prim.PayPriceButtons[1] = llList2Integer(script, buttons, 1);
                prim.PayPriceButtons[2] = llList2Integer(script, buttons, 2);
                prim.PayPriceButtons[3] = llList2Integer(script, buttons, 3);
            }
        }

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
            SetScale(script.Host, scale);
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
        public void llSetRemoteScriptAccessPin(IScriptInstance script, int pin)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            prim.RemoteScriptAccessPIN = pin;
            script.AddSleepMS(200);
        }

        [ScriptMethod]
        public void llSetStatus(IScriptInstance script, int flags, int enabledInt)
        {
            SetStatus(script.Host, flags, enabledInt);
        }

        [ScriptMethod]
        public void llSetText(IScriptInstance script, string text, Vector3 color, float alpha)
        {
            SetText(script.Host, text, color, alpha);
            script.AddSleepMS(200);
        }

        [ScriptMethod]
        public void llSetTouchText(IScriptInstance script, string text)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            text = text.Replace("\t", "    ");
            if (text.Length > 9)
                text = text.Substring(0, 9);

            prim.Prim.Properties.TouchName = text;
            prim.Scene.EntityAddOrUpdate(this, prim, UpdateFlags.Serialize, 0);
        }

        [ScriptMethod]
        public void llSetSitText(IScriptInstance script, string text)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            text = text.Replace("\t", "    ");
            if (text.Length > 9)
                text = text.Substring(0, 9);

            prim.Prim.Properties.SitName = text;
            prim.Scene.EntityAddOrUpdate(this, prim, UpdateFlags.Serialize, 0);
        }

        [ScriptMethod]
        public void llSitTarget(IScriptInstance script, Vector3 position, Quaternion rotation)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            prim.SitPosition = position;
            prim.SitRotation = rotation;
        }

        [ScriptMethod]
        public void llSetTextureAnim(IScriptInstance script, int mode, int face, int sizex, int sizey, float start, float length, float rate)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            Primitive.TextureAnimMode animMode = (Primitive.TextureAnimMode)mode;

            if (face == LSLConstants.ALL_SIDES)
                face = 255;

            Primitive.TextureAnimation animation = new Primitive.TextureAnimation();
            animation.Flags = animMode;
            animation.Face = (uint)face;
            animation.Length = length;
            animation.Rate = rate;
            animation.SizeX = (uint)sizex;
            animation.SizeY = (uint)sizey;
            animation.Start = start;

            prim.Prim.TextureAnim = animation;
            prim.Scene.EntityAddOrUpdate(this, prim, 0, (uint)LLUpdateFlags.TextureAnim);
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

        [ScriptMethod]
        public void llMessageLinked(IScriptInstance script, int linknum, int num, string str, string id)
        {
            if (m_lslScriptEngine != null)
            {
                ISceneEntity[] recipients = GetLinkParts(script.Host, linknum);

                int sender = (script.Host is ILinkable) ? ((ILinkable)script.Host).LinkNumber : 0;
                object[] eventParams = new object[] { sender, num, str, id };

                for (int i = 0; i < recipients.Length; i++)
                    m_lslScriptEngine.PostObjectEvent(recipients[i].ID, "link_message", eventParams, null);
            }
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

        private ISceneEntity[] GetLinkParts(ISceneEntity entity, int linknum)
        {
            switch (linknum)
            {
                case LSLConstants.LINK_SET:
                    return GetFullLinkset(entity);
                case LSLConstants.LINK_ROOT:
                    return new ISceneEntity[] { GetRootEntity(entity) };
                case LSLConstants.LINK_ALL_OTHERS:
                {
                    ISceneEntity[] all = GetFullLinkset(entity);
                    ISceneEntity[] others = new ISceneEntity[all.Length - 1];
                    int j = 0;
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] != entity)
                            others[j++] = all[i];
                    }
                    return others;
                }
                case LSLConstants.LINK_ALL_CHILDREN:
                {
                    ISceneEntity parent = GetRootEntity(entity);
                    if (parent is ILinkable)
                        return ((ILinkable)parent).GetChildren();
                    return new ISceneEntity[0];
                }
                case LSLConstants.LINK_THIS:
                    return new ISceneEntity[] { entity };
                default:
                {
                    ISceneEntity parent = GetRootEntity(entity);
                    if (parent is ILinkable)
                    {
                        ILinkable linkableParent = (ILinkable)parent;
                        if (linknum == linkableParent.LinkNumber)
                        {
                            return new ISceneEntity[] { parent };
                        }
                        else
                        {
                            ILinkable[] children = linkableParent.GetChildren();
                            for (int i = 0; i < children.Length; i++)
                            {
                                if (linknum == children[i].LinkNumber)
                                    return new ISceneEntity[] { children[i] };
                            }
                        }
                    }
                    else if (linknum == 0)
                    {
                        return new ISceneEntity[] { entity };
                    }
                    return new ISceneEntity[0];
                }
            }
        }

        private void RezObject(IScriptInstance script, string inventory, Vector3 position, Vector3 vel, Quaternion rot, int param, bool atRoot)
        {
            // TODO: Test to make sure this actually rezzes from the root, and get the atRoot param working

            // Can't do this without an IAssetClient
            if (m_assetClient == null)
                return;

            // Sanity check the input rotation
            if (Single.IsNaN(rot.X) || Single.IsNaN(rot.Y) || Single.IsNaN(rot.Z) || Single.IsNaN(rot.W))
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
                                prim.FallStart = Util.TickCount();
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
                        obj.FallStart = Util.TickCount();
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
