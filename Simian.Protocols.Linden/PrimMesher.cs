/*
 * Copyright (c) OpenSimulator
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using log4net;
using OpenMetaverse;
using PrimMesher;

namespace Simian.Protocols.Linden
{
    [SceneModule("PrimMesher")]
    public class PrimMesher : ISceneModule, IPrimMesher
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IAssetClient m_assetClient;

        public void Start(IScene scene)
        {
            m_assetClient = scene.Simian.GetAppModule<IAssetClient>();
        }

        public void Stop()
        {
        }

        public PhysicsMesh GetPhysicsMesh(LLPrimitive prim)
        {
            List<Coord> coords;
            List<Face> faces;

            if (prim.Prim.Sculpt != null && prim.Prim.Sculpt.SculptTexture != UUID.Zero)
            {
                SculptMesh mesh = GetSculptMesh(prim, DetailLevel.Low, true);
                if (mesh == null)
                    return null;

                coords = mesh.coords;
                faces = mesh.faces;
            }
            else
            {
                PrimMesh mesh = GetPrimMesh(prim, DetailLevel.Low, true);
                if (mesh == null)
                    return null;

                coords = mesh.coords;
                faces = mesh.faces;
            }

            Vector3[] vertices = new Vector3[coords.Count];
            for (int i = 0; i < coords.Count; i++)
            {
                Coord c = coords[i];
                vertices[i] = new Vector3(c.X, c.Y, c.Z);
            }

            ushort[] indices = new ushort[faces.Count * 3];
            for (int i = 0; i < faces.Count; i++)
            {
                Face f = faces[i];
                indices[i * 3 + 0] = (ushort)f.v1;
                indices[i * 3 + 1] = (ushort)f.v2;
                indices[i * 3 + 2] = (ushort)f.v3;
            }

            return new PhysicsMesh
            {
                Vertices = vertices,
                Indices = indices
            };
        }

        public RenderingMesh GetRenderingMesh(LLPrimitive prim, DetailLevel lod)
        {
            return null;
        }

        public SculptMesh GetSculptMesh(LLPrimitive llprim, DetailLevel lod, bool isBasicMesh)
        {
            Primitive prim = llprim.Prim;

            SculptMesh.SculptType sculptType;
            switch (prim.Sculpt.Type)
            {
                case SculptType.Cylinder:
                    sculptType = SculptMesh.SculptType.cylinder;
                    break;
                case SculptType.Plane:
                    sculptType = SculptMesh.SculptType.plane;
                    break;
                case SculptType.Torus:
                    sculptType = SculptMesh.SculptType.torus;
                    break;
                case SculptType.Sphere:
                default:
                    sculptType = SculptMesh.SculptType.sphere;
                    break;
            }

            Bitmap sculptTexture = null;
            if (m_assetClient != null)
            {
                Asset textureAsset;
                if (m_assetClient.TryGetAsset(prim.Sculpt.SculptTexture, "image/x-j2c", out textureAsset))
                {
                    try
                    {
                        sculptTexture = (Bitmap)CSJ2K.J2kImage.FromBytes(textureAsset.Data);
                    }
                    catch (Exception ex)
                    {
                        m_log.Warn("Failed to decode sculpt texture " + textureAsset.ID + ": " + ex.Message);
                    }
                }
                else
                {
                    m_log.Warn("Failed to fetch sculpt texture asset " + prim.Sculpt.SculptTexture);
                }
            }

            if (sculptTexture != null)
                return new SculptMesh(sculptTexture, sculptType, 32, !isBasicMesh, prim.Sculpt.Mirror, prim.Sculpt.Invert);
            else
                return null;
        }

        public static PrimMesh GetPrimMesh(LLPrimitive llprim, DetailLevel lod, bool isBasicMesh)
        {
            Primitive prim = llprim.Prim;
            Primitive.ConstructionData primData = prim.PrimData;

            // Magic PrimMesher values taken from OpenSim
            float scale1 = 0.01f;
            float scale2 = 2.0e-5f;
            float scale3 = 1.8f;
            float scale4 = 3.2f;

            float pathShearX = prim.PrimData.PathShearX < 128 ? (float)prim.PrimData.PathShearX * scale1 : (float)(prim.PrimData.PathShearX - 256) * scale1;
            float pathShearY = prim.PrimData.PathShearY < 128 ? (float)prim.PrimData.PathShearY * scale1 : (float)(prim.PrimData.PathShearY - 256) * scale1;
            float pathBegin = (float)prim.PrimData.PathBegin * scale2;
            float pathEnd = 1.0f - (float)prim.PrimData.PathEnd * scale2;
            float pathScaleX = (float)(prim.PrimData.PathScaleX - 100) * scale1;
            float pathScaleY = (float)(prim.PrimData.PathScaleY - 100) * scale1;

            float profileBegin = (float)prim.PrimData.ProfileBegin * scale2;
            float profileEnd = 1.0f - (float)prim.PrimData.ProfileEnd * scale2;
            float profileHollow = (float)prim.PrimData.ProfileHollow * scale2;

            int sides = 4;
            if (prim.PrimData.ProfileCurve == ProfileCurve.EqualTriangle)
                sides = 3;
            else if (prim.PrimData.ProfileCurve == ProfileCurve.Circle)
                sides = 24;
            else if (prim.PrimData.ProfileCurve == ProfileCurve.HalfCircle)
                sides = 24;

            int hollowSides = sides;
            if (prim.PrimData.ProfileHole == HoleType.Circle)
                hollowSides = 24;
            else if (prim.PrimData.ProfileHole == HoleType.Square)
                hollowSides = 4;
            else if (prim.PrimData.ProfileHole == HoleType.Triangle)
                hollowSides = 3;

            PrimMesh primMesh = new PrimMesh(sides, profileBegin, profileEnd, profileHollow, hollowSides);
            primMesh.viewerMode = !isBasicMesh;

            primMesh.topShearX = pathShearX;
            primMesh.topShearY = pathShearY;
            primMesh.pathCutBegin = pathBegin;
            primMesh.pathCutEnd = pathEnd;

            if (prim.PrimData.PathCurve == PathCurve.Line)
            {
                primMesh.twistBegin = (int)(prim.PrimData.PathTwistBegin * scale3);
                primMesh.twistEnd = (int)(prim.PrimData.PathTwist * scale3);
                primMesh.taperX = pathScaleX;
                primMesh.taperY = pathScaleY;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    m_log.Warn("Corrupted shape for prim " + prim.ID);
                    if (profileBegin < 0.0f) profileBegin = 0.0f;
                    if (profileEnd > 1.0f) profileEnd = 1.0f;
                }

                try
                {
                    primMesh.ExtrudeLinear();
                }
                catch (Exception ex)
                {
                    m_log.Warn("Shape extrusion failure for prim " + prim.ID + ": " + ex);
                    return null;
                }
            }
            else
            {
                primMesh.holeSizeX = prim.PrimData.PathScaleX * scale1;
                primMesh.holeSizeY = prim.PrimData.PathScaleY * scale1;
                primMesh.radius = scale1 * prim.PrimData.PathRadiusOffset;
                primMesh.revolutions = prim.PrimData.PathRevolutions;
                primMesh.skew = scale1 * prim.PrimData.PathSkew;
                primMesh.twistBegin = (int)(prim.PrimData.PathTwistBegin * scale4);
                primMesh.twistEnd = (int)(prim.PrimData.PathTwist * scale4);
                primMesh.taperX = prim.PrimData.PathTaperX * scale1;
                primMesh.taperY = prim.PrimData.PathTaperY * scale1;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    m_log.Warn("Corrupted shape for prim " + prim.ID);
                    if (profileBegin < 0.0f) profileBegin = 0.0f;
                    if (profileEnd > 1.0f) profileEnd = 1.0f;
                }

                try
                {
                    primMesh.ExtrudeCircular();
                }
                catch (Exception ex)
                {
                    m_log.Warn("Shape extrusion failure for prim " + prim.ID + ": " + ex);
                    return null;
                }
            }

            return primMesh;
        }
    }
}
