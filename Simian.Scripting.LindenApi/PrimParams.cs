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
        public void llSetPrimitiveParams(IScriptInstance script, object[] rules)
        {
            script.AddSleepMS(200);
            SetPrimParams(script, script.Host, rules);
        }

        [ScriptMethod]
        public void llSetLinkPrimitiveParams(IScriptInstance script, int linknumber, object[] rules)
        {
            ISceneEntity[] entities = GetLinkParts(script.Host, linknumber);
            for (int i = 0; i < entities.Length; i++)
                SetPrimParams(script, entities[i], rules);
        }

        private void SetPrimParams(IScriptInstance script, ISceneEntity entity, object[] rules)
        {
            LLPrimitive prim = entity as LLPrimitive;
            if (prim == null)
                return;

            bool changedShape = false, changedColor = false, changedTexture = false;

            int i = 0;
            while (i < rules.Length)
            {
                int code = llList2Integer(script, rules, i++);

                switch (code)
                {
                    case LSLConstants.PRIM_NAME:
                    {
                        SetObjectName(prim, llList2String(script, rules, i++));
                        break;
                    }
                    case LSLConstants.PRIM_DESC:
                    {
                        SetObjectDesc(prim, llList2String(script, rules, i++));
                        break;
                    }
                    case LSLConstants.PRIM_TYPE:
                    {
                        SetPrimType(script, rules, ref i);
                        prim.Scene.EntityAddOrUpdate(this, prim, UpdateFlags.Shape, 0);
                        changedShape = true;
                        break;
                    }
                    case LSLConstants.PRIM_MATERIAL:
                    {
                        prim.Prim.PrimData.Material = (Material)llList2Integer(script, rules, i++);
                        prim.Scene.EntityAddOrUpdate(this, prim, 0, (uint)LLUpdateFlags.Material);
                        break;
                    }
                    case LSLConstants.PRIM_PHYSICS:
                    {
                        SetStatus(prim, LSLConstants.STATUS_PHYSICS, llList2Integer(script, rules, i++));
                        break;
                    }
                    case LSLConstants.PRIM_TEMP_ON_REZ:
                    {
                        bool tempOnRez = (llList2Integer(script, rules, i++) != 0);
                        LLPrimitive parent = GetRootEntity(prim) as LLPrimitive;
                        if (parent != null)
                        {
                            if (tempOnRez)
                                parent.Prim.Flags |= PrimFlags.TemporaryOnRez;
                            else
                                parent.Prim.Flags &= ~PrimFlags.TemporaryOnRez;
                            parent.Scene.EntityAddOrUpdate(this, parent, 0, (uint)LLUpdateFlags.PrimFlags);
                        }
                        break;
                    }
                    case LSLConstants.PRIM_PHANTOM:
                    {
                        SetStatus(prim, LSLConstants.STATUS_PHANTOM, llList2Integer(script, rules, i++));
                        break;
                    }
                    case LSLConstants.PRIM_POSITION:
                    {
                        SetPos(prim, llList2Vector(script, rules, i++));
                        break;
                    }
                    case LSLConstants.PRIM_ROTATION:
                    {
                        SetRot(prim, llList2Rot(script, rules, i++));
                        break;
                    }
                    case LSLConstants.PRIM_SIZE:
                    {
                        SetScale(prim, llList2Vector(script, rules, i++));
                        break;
                    }
                    case LSLConstants.PRIM_TEXTURE:
                    {
                        int face = llList2Integer(script, rules, i++);
                        string texture = llList2String(script, rules, i++);
                        Vector3 repeats = llList2Vector(script, rules, i++);
                        Vector3 offsets = llList2Vector(script, rules, i++);
                        float rotRadians = llList2Float(script, rules, i++);

                        SetTexture(script, prim, texture, face);
                        ScaleTexture(script, prim, repeats.X, repeats.Y, face);
                        OffsetTexture(script, prim, offsets.X, offsets.Y, face);
                        RotateTexture(script, prim, rotRadians, face);

                        changedTexture = true;
                        break;
                    }
                    case LSLConstants.PRIM_TEXT:
                    {
                        string text = llList2String(script, rules, i++);
                        Vector3 color = llList2Vector(script, rules, i++);
                        float alpha = llList2Float(script, rules, i++);
                        
                        SetText(prim, text, color, alpha);
                        break;
                    }
                    case LSLConstants.PRIM_COLOR:
                    {
                        int face = llList2Integer(script, rules, i++);
                        Vector3 color = llList2Vector(script, rules, i++);
                        float alpha = llList2Float(script, rules, i++);

                        SetColor(prim, color, face);
                        SetAlpha(prim, alpha, face);

                        changedColor = true;
                        break;
                    }
                    case LSLConstants.PRIM_BUMP_SHINY:
                    {
                        int face = llList2Integer(script, rules, i++);
                        Shininess shiny = (Shininess)llList2Integer(script, rules, i++);
                        Bumpiness bump = (Bumpiness)llList2Integer(script, rules, i++);

                        SetShinyBump(prim, shiny, bump, face);

                        changedTexture = true;
                        break;
                    }
                    case LSLConstants.PRIM_POINT_LIGHT:
                    {
                        bool enabled = (llList2Integer(script, rules, i++) != 0);
                        Vector3 color = llList2Vector(script, rules, i++);
                        float intensity = llList2Float(script, rules, i++);
                        float radius = llList2Float(script, rules, i++);
                        float falloff = llList2Float(script, rules, i++);

                        if (enabled)
                        {
                            Primitive.LightData light = new Primitive.LightData();
                            light.Color = new Color4(
                                Utils.Clamp(color.X, 0f, 1f),
                                Utils.Clamp(color.Y, 0f, 1f),
                                Utils.Clamp(color.Z, 0f, 1f),
                                1f);
                            light.Intensity = intensity;
                            light.Radius = radius;
                            light.Falloff = falloff;

                            prim.Prim.Light = light;
                        }
                        else
                        {
                            prim.Prim.Light = null;
                        }

                        prim.Scene.EntityAddOrUpdate(this, prim, 0, (uint)LLUpdateFlags.ExtraData);
                        break;
                    }
                    case LSLConstants.PRIM_FULLBRIGHT:
                    {
                        int face = llList2Integer(script, rules, i++);
                        bool fullbright = (llList2Integer(script, rules, i++) != 0);
                        SetFullbright(prim, fullbright, face);

                        changedTexture = true;
                        break;
                    }
                    case LSLConstants.PRIM_FLEXIBLE:
                    {
                        bool enabled = (llList2Integer(script, rules, i++) != 0);
                        int softness = llList2Integer(script, rules, i++);
                        float gravity = llList2Float(script, rules, i++);
                        float friction = llList2Float(script, rules, i++);
                        float wind = llList2Float(script, rules, i++);
                        float tension = llList2Float(script, rules, i++);
                        Vector3 force = llList2Vector(script, rules, i++);

                        if (enabled)
                        {
                            Primitive.FlexibleData flex = new Primitive.FlexibleData();
                            flex.Softness = softness;
                            flex.Gravity = gravity;
                            flex.Drag = friction;
                            flex.Wind = wind;
                            flex.Tension = tension;
                            flex.Force = force;

                            prim.Prim.Flexible = flex;
                        }
                        else
                        {
                            prim.Prim.Flexible = null;
                        }

                        prim.Scene.EntityAddOrUpdate(this, prim, 0, (uint)LLUpdateFlags.ExtraData);
                        break;
                    }
                    case LSLConstants.PRIM_TEXGEN:
                    {
                        int face = llList2Integer(script, rules, i++);
                        MappingType texgen = (MappingType)llList2Integer(script, rules, i++);
                        SetTexgen(prim, texgen, face);

                        changedTexture = true;
                        break;
                    }
                    case LSLConstants.PRIM_GLOW:
                    {
                        int face = llList2Integer(script, rules, i++);
                        float intensity = llList2Float(script, rules, i++);
                        SetGlow(prim, intensity, face);

                        changedTexture = true;
                        break;
                    }
                }
            }

            if (m_lslScriptEngine != null)
            {
                if (changedShape)
                    m_lslScriptEngine.PostObjectEvent(prim.ID, "changed", new object[] { LSLConstants.CHANGED_SHAPE }, null);
                if (changedTexture)
                    m_lslScriptEngine.PostObjectEvent(prim.ID, "changed", new object[] { LSLConstants.CHANGED_TEXTURE }, null);
                if (changedColor)
                    m_lslScriptEngine.PostObjectEvent(prim.ID, "changed", new object[] { LSLConstants.CHANGED_COLOR }, null);
            }
        }

        private void SetPrimType(IScriptInstance script, object[] rules, ref int i)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return;

            int code = llList2Integer(script, rules, i++);

            switch (code)
            {
                case LSLConstants.PRIM_TYPE_BOX:
                {
                    int holeShape = llList2Integer(script, rules, i++);
                    Vector3 cut = llList2Vector(script, rules, i++);
                    float hollow = llList2Float(script, rules, i++);
                    Vector3 twist = llList2Vector(script, rules, i++);
                    Vector3 topSize = llList2Vector(script, rules, i++);
                    Vector3 topShear = llList2Vector(script, rules, i++);

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Box);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = null;

                    SetPrimShapeParams(prim, holeShape, cut, hollow, twist, topSize, topShear);
                    break;
                }
                case LSLConstants.PRIM_TYPE_CYLINDER:
                {
                    int holeShape = llList2Integer(script, rules, i++);
                    Vector3 cut = llList2Vector(script, rules, i++);
                    float hollow = llList2Float(script, rules, i++);
                    Vector3 twist = llList2Vector(script, rules, i++);
                    Vector3 topSize = llList2Vector(script, rules, i++);
                    Vector3 topShear = llList2Vector(script, rules, i++);

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Cylinder);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = null;

                    SetPrimShapeParams(prim, holeShape, cut, hollow, twist, topSize, topShear);
                    break;
                }
                case LSLConstants.PRIM_TYPE_PRISM:
                {
                    int holeShape = llList2Integer(script, rules, i++);
                    Vector3 cut = llList2Vector(script, rules, i++);
                    float hollow = llList2Float(script, rules, i++);
                    Vector3 twist = llList2Vector(script, rules, i++);
                    Vector3 topSize = llList2Vector(script, rules, i++);
                    Vector3 topShear = llList2Vector(script, rules, i++);

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Prism);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = null;

                    SetPrimShapeParams(prim, holeShape, cut, hollow, twist, topSize, topShear);
                    break;
                }
                case LSLConstants.PRIM_TYPE_SPHERE:
                {
                    int holeShape = llList2Integer(script, rules, i++);
                    Vector3 cut = llList2Vector(script, rules, i++);
                    float hollow = llList2Float(script, rules, i++);
                    Vector3 twist = llList2Vector(script, rules, i++);
                    Vector3 dimple = llList2Vector(script, rules, i++);

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Sphere);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = null;

                    SetPrimShapeParams(prim, holeShape, cut, hollow, twist, dimple);
                    break;
                }
                case LSLConstants.PRIM_TYPE_TORUS:
                {
                    int holeShape = llList2Integer(script, rules, i++);
                    Vector3 cut = llList2Vector(script, rules, i++);
                    float hollow = llList2Float(script, rules, i++);
                    Vector3 twist = llList2Vector(script, rules, i++);
                    Vector3 topSize = llList2Vector(script, rules, i++);
                    Vector3 topShear = llList2Vector(script, rules, i++);
                    Vector3 advCut = llList2Vector(script, rules, i++);
                    Vector3 taper = llList2Vector(script, rules, i++);
                    float revolutions = llList2Float(script, rules, i++);
                    float radiusOffset = llList2Float(script, rules, i++);
                    float skew = llList2Float(script, rules, i++);

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Torus);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = null;

                    SetPrimShapeParams(prim, holeShape, cut, hollow, twist, topSize, topShear, advCut, taper, revolutions, radiusOffset, skew);
                    break;
                }
                case LSLConstants.PRIM_TYPE_TUBE:
                {
                    int holeShape = llList2Integer(script, rules, i++);
                    Vector3 cut = llList2Vector(script, rules, i++);
                    float hollow = llList2Float(script, rules, i++);
                    Vector3 twist = llList2Vector(script, rules, i++);
                    Vector3 topSize = llList2Vector(script, rules, i++);
                    Vector3 topShear = llList2Vector(script, rules, i++);
                    Vector3 advCut = llList2Vector(script, rules, i++);
                    Vector3 taper = llList2Vector(script, rules, i++);
                    float revolutions = llList2Float(script, rules, i++);
                    float radiusOffset = llList2Float(script, rules, i++);
                    float skew = llList2Float(script, rules, i++);

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Tube);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = null;

                    SetPrimShapeParams(prim, holeShape, cut, hollow, twist, topSize, topShear, advCut, taper, revolutions, radiusOffset, skew);
                    break;
                }
                case LSLConstants.PRIM_TYPE_RING:
                {
                    int holeShape = llList2Integer(script, rules, i++);
                    Vector3 cut = llList2Vector(script, rules, i++);
                    float hollow = llList2Float(script, rules, i++);
                    Vector3 twist = llList2Vector(script, rules, i++);
                    Vector3 topSize = llList2Vector(script, rules, i++);
                    Vector3 topShear = llList2Vector(script, rules, i++);
                    Vector3 advCut = llList2Vector(script, rules, i++);
                    Vector3 taper = llList2Vector(script, rules, i++);
                    float revolutions = llList2Float(script, rules, i++);
                    float radiusOffset = llList2Float(script, rules, i++);
                    float skew = llList2Float(script, rules, i++);

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Ring);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = null;

                    SetPrimShapeParams(prim, holeShape, cut, hollow, twist, topSize, topShear, advCut, taper, revolutions, radiusOffset, skew);
                    break;
                }
                case LSLConstants.PRIM_TYPE_SCULPT:
                {
                    string sculptMap = llList2String(script, rules, i++);
                    SculptType type = (SculptType)llList2Integer(script, rules, i++);
                    UUID textureID = InventoryKey(script, sculptMap, AssetType.Texture);
                    if (textureID == UUID.Zero)
                        return;

                    Primitive.ConstructionData primData = ObjectManager.BuildBasicShape(PrimType.Sculpt);
                    primData.Material = prim.Prim.PrimData.Material;
                    primData.State = prim.Prim.PrimData.State;

                    Primitive.SculptData sculpt = new Primitive.SculptData();
                    sculpt.Type = type;
                    sculpt.SculptTexture = textureID;

                    prim.Prim.PrimData = primData;
                    prim.Prim.Sculpt = sculpt;
                    break;
                }
            }
        }

        private void SetPrimShapeParams(LLPrimitive prim, int holeShape, Vector3 cut, float hollow, Vector3 twist, Vector3 topSize, Vector3 topShear)
        {
            HoleType hole = (HoleType)holeShape;
            if (hole != HoleType.Same && hole != HoleType.Circle && hole != HoleType.Square && hole != HoleType.Triangle)
                hole = HoleType.Same;

            // Clamp the cut values
            cut.X = Utils.Clamp(cut.X, 0f, 1f);
            cut.Y = Utils.Clamp(cut.Y, 0f, 1f);
            if (cut.Y - cut.X < 0.05f)
            {
                cut.X = cut.Y - 0.05f;
                if (cut.X < 0f)
                {
                    cut.X = 0f;
                    cut.Y = 0.05f;
                }
            }

            // Clamp hollow
            hollow = Utils.Clamp(hollow, 0f, 0.95f);

            // Clamp the twist values
            twist.X = Utils.Clamp(twist.X, -1f, 1f);
            twist.Y = Utils.Clamp(twist.Y, -1f, 1f);

            // Clamp the taper values
            topSize.X = Utils.Clamp(topSize.X, 0f, 2f);
            topSize.Y = Utils.Clamp(topSize.Y, 0f, 2f);

            // Clamp the top shear values
            topShear.X = Utils.Clamp(topShear.X, -0.5f, 0.5f);
            topShear.Y = Utils.Clamp(topShear.Y, -0.5f, 0.5f);

            prim.Prim.PrimData.ProfileHole = hole;
            prim.Prim.PrimData.ProfileBegin = cut.X;
            prim.Prim.PrimData.ProfileEnd = cut.Y;
            prim.Prim.PrimData.ProfileHollow = hollow;
            prim.Prim.PrimData.PathTwistBegin = twist.X;
            prim.Prim.PrimData.PathTwist = twist.Y;
            prim.Prim.PrimData.PathScaleX = topSize.Y;
            prim.Prim.PrimData.PathScaleY = topSize.Y;
            prim.Prim.PrimData.PathShearX = topShear.X;
            prim.Prim.PrimData.PathShearY = topShear.Y;
        }

        private void SetPrimShapeParams(LLPrimitive prim, int holeShape, Vector3 cut, float hollow, Vector3 twist, Vector3 dimple)
        {
            HoleType hole = (HoleType)holeShape;
            if (hole != HoleType.Same && hole != HoleType.Circle && hole != HoleType.Square && hole != HoleType.Triangle)
                hole = HoleType.Same;

            // Clamp the cut values
            cut.X = Utils.Clamp(cut.X, 0f, 1f);
            cut.Y = Utils.Clamp(cut.Y, 0f, 1f);
            if (cut.Y - cut.X < 0.05f)
            {
                cut.X = cut.Y - 0.05f;
                if (cut.X < 0f)
                {
                    cut.X = 0f;
                    cut.Y = 0.05f;
                }
            }

            // Clamp hollow
            hollow = Utils.Clamp(hollow, 0f, 0.95f);

            // Clamp the twist values
            twist.X = Utils.Clamp(twist.X, -1f, 1f);
            twist.Y = Utils.Clamp(twist.Y, -1f, 1f);

            // Clamp the dimple values
            dimple.X = Utils.Clamp(dimple.X, 0f, 1f);
            dimple.Y = Utils.Clamp(dimple.Y, 0f, 1f);
            if (dimple.Y - cut.X < 0.05f)
                dimple.X = cut.Y - 0.05f;

            prim.Prim.PrimData.ProfileHole = hole;
            prim.Prim.PrimData.ProfileBegin = cut.X;
            prim.Prim.PrimData.ProfileEnd = cut.Y;
            prim.Prim.PrimData.ProfileHollow = hollow;
            prim.Prim.PrimData.PathTwistBegin = twist.X;
            prim.Prim.PrimData.PathTwist = twist.Y;
            // TODO: Is this right? If so, what is cut for?
            prim.Prim.PrimData.ProfileBegin = dimple.X;
            prim.Prim.PrimData.ProfileEnd = dimple.Y;
        }

        private void SetPrimShapeParams(LLPrimitive prim, int holeShape, Vector3 cut, float hollow, Vector3 twist, Vector3 topSize, Vector3 topShear,
            Vector3 advCut, Vector3 taper, float revolutions, float radiusOffset, float skew)
        {
            // FIXME: Implement this
            throw new NotImplementedException();
        }

        #region Helpers

        private void SetText(ISceneEntity obj, string text, Vector3 color, float alpha)
        {
            if (obj is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)obj;

                prim.Prim.Text = text;
                prim.Prim.TextColor = new Color4(
                    Utils.Clamp(color.X, 0f, 1f),
                    Utils.Clamp(color.Y, 0f, 1f),
                    Utils.Clamp(color.Z, 0f, 1f),
                    Utils.Clamp(alpha, 0f, 1f));

                prim.Scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
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

        private void SetScale(ISceneEntity obj, Vector3 scale)
        {
            obj.Scale = new Vector3(
                Utils.Clamp(scale.X, 0.01f, 10f),
                Utils.Clamp(scale.Y, 0.01f, 10f),
                Utils.Clamp(scale.Z, 0.01f, 10f));

            obj.Scene.EntityAddOrUpdate(this, obj, UpdateFlags.Scale, 0);
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

        private void ScaleTexture(IScriptInstance script, LLPrimitive obj, float u, float v, int side)
        {
            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Change one face
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.RepeatU = u;
                face.RepeatV = v;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                    {
                        face.RepeatU = u;
                        face.RepeatV = v;
                    }
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private void OffsetTexture(IScriptInstance script, LLPrimitive obj, float u, float v, int side)
        {
            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Change one face
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.OffsetU = u;
                face.OffsetV = v;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                    {
                        face.OffsetU = u;
                        face.OffsetV = v;
                    }
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private void RotateTexture(IScriptInstance script, LLPrimitive obj, float angle, int side)
        {
            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Change one face
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.Rotation = angle;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                        face.Rotation = angle;
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private void SetAlpha(LLPrimitive obj, float alpha, int side)
        {
            if (obj is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)obj;
                int sides = GetNumberOfSides(prim);
                float newAlpha = (float)Utils.Clamp(alpha, 0.0, 1.0);

                if (side >= 0 && side < sides)
                {
                    // Get or create the requested face and update
                    Primitive.TextureEntryFace face = prim.Prim.Textures.CreateFace((uint)side);
                    Color4 faceColor = face.RGBA;
                    faceColor.A = newAlpha;
                    face.RGBA = faceColor;

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                }
                else if (side == LSLConstants.ALL_SIDES)
                {
                    // Change all of the faces
                    for (uint i = 0; i < sides; i++)
                    {
                        Primitive.TextureEntryFace face = prim.Prim.Textures.GetFace(i);
                        if (face != null)
                        {
                            Color4 faceColor = face.RGBA;
                            faceColor.A = newAlpha;
                            face.RGBA = faceColor;
                        }
                    }

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                }
            }
        }

        private void SetColor(LLPrimitive obj, Vector3 color, int side)
        {
            if (obj is LLPrimitive)
            {
                LLPrimitive prim = (LLPrimitive)obj;
                int sides = GetNumberOfSides(prim);

                // Sanitize the color input
                color.X = Utils.Clamp(color.X, 0.0f, 1.0f);
                color.Y = Utils.Clamp(color.Y, 0.0f, 1.0f);
                color.Z = Utils.Clamp(color.Z, 0.0f, 1.0f);

                if (side >= 0 && side < sides)
                {
                    // Get or create the requested face and update
                    Primitive.TextureEntryFace face = prim.Prim.Textures.CreateFace((uint)side);
                    face.RGBA = new Color4(color.X, color.Y, color.Z, face.RGBA.A);

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                }
                else if (side == LSLConstants.ALL_SIDES)
                {
                    // Change all of the faces
                    for (uint i = 0; i < sides; i++)
                    {
                        Primitive.TextureEntryFace face = prim.Prim.Textures.GetFace(i);
                        if (face != null)
                            face.RGBA = new Color4(color.X, color.Y, color.Z, face.RGBA.A);
                    }

                    obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
                }
            }
        }

        private void SetShinyBump(LLPrimitive obj, Shininess shiny, Bumpiness bump, int side)
        {
            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Get or create the requested face and update
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.Shiny = shiny;
                face.Bump = bump;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                    {
                        face.Shiny = shiny;
                        face.Bump = bump;
                    }
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private void SetFullbright(LLPrimitive obj, bool fullbright, int side)
        {
            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Get or create the requested face and update
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.Fullbright = fullbright;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                        face.Fullbright = fullbright;
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private void SetTexgen(LLPrimitive obj, MappingType texgen, int side)
        {
            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Get or create the requested face and update
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.TexMapType = texgen;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                        face.TexMapType = texgen;
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private void SetGlow(LLPrimitive obj, float intensity, int side)
        {
            int sides = GetNumberOfSides(obj);
            if (side >= 0 && side < sides)
            {
                // Get or create the requested face and update
                Primitive.TextureEntryFace face = obj.Prim.Textures.CreateFace((uint)side);
                face.Glow = intensity;

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
            else if (side == LSLConstants.ALL_SIDES)
            {
                // Change all of the faces
                for (uint i = 0; i < sides; i++)
                {
                    Primitive.TextureEntryFace face = obj.Prim.Textures.GetFace(i);
                    if (face != null)
                        face.Glow = intensity;
                }

                obj.Scene.EntityAddOrUpdate(this, obj, 0, (uint)LLUpdateFlags.Textures);
            }
        }

        private void SetObjectName(ISceneEntity entity, string name)
        {
            if (name.Length > 63)
                name = name.Substring(0, 63);
            entity.Name = name;
            entity.Scene.EntityAddOrUpdate(this, entity, UpdateFlags.Serialize, 0);
        }

        private void SetObjectDesc(ISceneEntity entity, string desc)
        {
            if (desc.Length > 127)
                desc = desc.Substring(0, 127);
            if (entity is LLPrimitive)
            {
                ((LLPrimitive)entity).Prim.Properties.Description = desc;
                entity.Scene.EntityAddOrUpdate(this, entity, UpdateFlags.Serialize, 0);
            }
        }

        private void SetStatus(ISceneEntity entity, int flags, int enabledInt)
        {
            bool enabled = (enabledInt != 0);

            LLPrimitive prim = entity as LLPrimitive;
            if (prim == null)
                return;

            UpdateFlags updateFlags = 0;
            LLUpdateFlags llUpdateFlags = 0;

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
            {
                prim.BlockGrab = enabled;
            }

            if ((flags & LSLConstants.STATUS_DIE_AT_EDGE) == LSLConstants.STATUS_DIE_AT_EDGE)
            {
                if (enabled)
                    prim.Prim.Flags |= PrimFlags.DieAtEdge;
                else
                    prim.Prim.Flags &= ~PrimFlags.DieAtEdge;

                llUpdateFlags |= LLUpdateFlags.PrimFlags;
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

                llUpdateFlags |= LLUpdateFlags.PrimFlags;
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

                llUpdateFlags |= LLUpdateFlags.PrimFlags;
            }

            entity.Scene.EntityAddOrUpdate(this, prim, updateFlags, (uint)llUpdateFlags);
        }

        #endregion Helpers
    }
}
