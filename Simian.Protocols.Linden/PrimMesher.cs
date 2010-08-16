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

using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

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
            OMVR.FacetedMesh omvrMesh = null;

            if (prim.Prim.Sculpt != null && prim.Prim.Sculpt.SculptTexture != UUID.Zero)
            {
                SculptMesh mesh = GetSculptMesh(prim, lod, false);
                if (mesh != null)
                    omvrMesh = GenerateFacetedMesh(1, prim.Prim, mesh.viewerFaces);
            }
            else
            {
                PrimMesh mesh = GetPrimMesh(prim, lod, false);
                if (mesh != null)
                    omvrMesh = GenerateFacetedMesh(mesh.numPrimFaces, prim.Prim, mesh.viewerFaces);
            }

            if (omvrMesh != null)
            {
                // Copy the faces
                RenderingMesh.Face[] renderingFaces = new RenderingMesh.Face[omvrMesh.Faces.Count];

                for (int i = 0; i < omvrMesh.Faces.Count; i++)
                {
                    OMVR.Face face = omvrMesh.Faces[i];
                    RenderingMesh.Face renderingFace = new RenderingMesh.Face();

                    // Copy the vertices for this face
                    renderingFace.Vertices = new Vertex[face.Vertices.Count];
                    for (int j = 0; j < face.Vertices.Count; j++)
                    {
                        OMVR.Vertex vertex = face.Vertices[j];
                        renderingFace.Vertices[j] = new Vertex { Position = vertex.Position, Normal = vertex.Normal, TexCoord = vertex.TexCoord };
                    }

                    // Copy the indices for this face
                    renderingFace.Indices = face.Indices.ToArray();

                    renderingFaces[i] = renderingFace;
                }

                return new RenderingMesh { Faces = renderingFaces };
            }
            else
            {
                return null;
            }
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

            int mesherLod = 32; // number used in Idealist viewer
            switch (lod)
            {
                case DetailLevel.Highest:
                    break;
                case DetailLevel.High:
                    break;
                case DetailLevel.Medium:
                    mesherLod /= 2;
                    break;
                case DetailLevel.Low:
                    mesherLod /= 4;
                    break;
            }

            if (sculptTexture != null)
                return new SculptMesh(sculptTexture, sculptType, mesherLod, !isBasicMesh, prim.Sculpt.Mirror, prim.Sculpt.Invert);
            else
                return null;
        }

        public static PrimMesh GetPrimMesh(LLPrimitive llprim, DetailLevel lod, bool isBasicMesh)
        {
            OMV.Primitive.ConstructionData primData = llprim.Prim.PrimData;
            int sides = 4;
            int hollowsides = 4;

            float profileBegin = primData.ProfileBegin;
            float profileEnd = primData.ProfileEnd;
            bool isSphere = false;

            if ((OMV.ProfileCurve)(primData.profileCurve & 0x07) == OMV.ProfileCurve.Circle)
            {
                switch (lod)
                {
                    case DetailLevel.Low:
                        sides = 6;
                        break;
                    case DetailLevel.Medium:
                        sides = 12;
                        break;
                    default:
                        sides = 24;
                        break;
                }
            }
            else if ((OMV.ProfileCurve)(primData.profileCurve & 0x07) == OMV.ProfileCurve.EqualTriangle)
                sides = 3;
            else if ((OMV.ProfileCurve)(primData.profileCurve & 0x07) == OMV.ProfileCurve.HalfCircle)
            {
                // half circle, prim is a sphere
                isSphere = true;
                switch (lod)
                {
                    case DetailLevel.Low:
                        sides = 6;
                        break;
                    case DetailLevel.Medium:
                        sides = 12;
                        break;
                    default:
                        sides = 24;
                        break;
                }
                profileBegin = 0.5f * profileBegin + 0.5f;
                profileEnd = 0.5f * profileEnd + 0.5f;
            }

            if ((OMV.HoleType)primData.ProfileHole == OMV.HoleType.Same)
                hollowsides = sides;
            else if ((OMV.HoleType)primData.ProfileHole == OMV.HoleType.Circle)
            {
                switch (lod)
                {
                    case DetailLevel.Low:
                        hollowsides = 6;
                        break;
                    case DetailLevel.Medium:
                        hollowsides = 12;
                        break;
                    default:
                        hollowsides = 24;
                        break;
                }
            }
            else if ((OMV.HoleType)primData.ProfileHole == OMV.HoleType.Triangle)
                hollowsides = 3;

            PrimMesh newPrim = new PrimMesh(sides, profileBegin, profileEnd, (float)primData.ProfileHollow, hollowsides);
            newPrim.viewerMode = !isBasicMesh;
            newPrim.holeSizeX = primData.PathScaleX;
            newPrim.holeSizeY = primData.PathScaleY;
            newPrim.pathCutBegin = primData.PathBegin;
            newPrim.pathCutEnd = primData.PathEnd;
            newPrim.topShearX = primData.PathShearX;
            newPrim.topShearY = primData.PathShearY;
            newPrim.radius = primData.PathRadiusOffset;
            newPrim.revolutions = primData.PathRevolutions;
            newPrim.skew = primData.PathSkew;
            switch (lod)
            {
                case DetailLevel.Low:
                    newPrim.stepsPerRevolution = 6;
                    break;
                case DetailLevel.Medium:
                    newPrim.stepsPerRevolution = 12;
                    break;
                default:
                    newPrim.stepsPerRevolution = 24;
                    break;
            }

            if ((primData.PathCurve == OMV.PathCurve.Line) || (primData.PathCurve == OMV.PathCurve.Flexible))
            {
                newPrim.taperX = 1.0f - primData.PathScaleX;
                newPrim.taperY = 1.0f - primData.PathScaleY;
                newPrim.twistBegin = (int)(180 * primData.PathTwistBegin);
                newPrim.twistEnd = (int)(180 * primData.PathTwist);
                newPrim.ExtrudeLinear();
            }
            else
            {
                newPrim.taperX = primData.PathTaperX;
                newPrim.taperY = primData.PathTaperY;
                newPrim.twistBegin = (int)(360 * primData.PathTwistBegin);
                newPrim.twistEnd = (int)(360 * primData.PathTwist);
                newPrim.ExtrudeCircular();
            }

            int numViewerFaces = newPrim.viewerFaces.Count;
            int numPrimFaces = newPrim.numPrimFaces;

            for (uint i = 0; i < numViewerFaces; i++)
            {
                ViewerFace vf = newPrim.viewerFaces[(int)i];

                if (isSphere)
                {
                    vf.uv1.U = (vf.uv1.U - 0.5f) * 2.0f;
                    vf.uv2.U = (vf.uv2.U - 0.5f) * 2.0f;
                    vf.uv3.U = (vf.uv3.U - 0.5f) * 2.0f;
                }
            }

            return newPrim;
        }

        private static OMVR.FacetedMesh GenerateFacetedMesh(int numPrimFaces, OMV.Primitive prim, List<global::PrimMesher.ViewerFace> viewerFaces)
        {
            // copy the vertex information into OMVR.IRendering structures
            OMVR.FacetedMesh omvrmesh = new OMVR.FacetedMesh();
            omvrmesh.Faces = new List<OMVR.Face>();
            omvrmesh.Prim = prim;
            omvrmesh.Profile = new OMVR.Profile();
            omvrmesh.Profile.Faces = new List<OMVR.ProfileFace>();
            omvrmesh.Profile.Positions = new List<OMV.Vector3>();
            omvrmesh.Path = new OMVR.Path();
            omvrmesh.Path.Points = new List<OMVR.PathPoint>();

            Dictionary<OMV.Vector3, int> vertexAccount = new Dictionary<OMV.Vector3, int>();
            OMV.Vector3 pos;
            int indx;
            OMVR.Vertex vert;
            for (int ii = 0; ii < numPrimFaces; ii++)
            {
                OMVR.Face oface = new OMVR.Face();
                oface.Vertices = new List<OMVR.Vertex>();
                oface.Indices = new List<ushort>();
                oface.TextureFace = prim.Textures.GetFace((uint)ii);
                int faceVertices = 0;
                vertexAccount.Clear();
                foreach (global::PrimMesher.ViewerFace vface in viewerFaces)
                {
                    if (vface.primFaceNumber == ii)
                    {
                        faceVertices++;

                        pos = new OMV.Vector3(vface.v1.X, vface.v1.Y, vface.v1.Z);
                        if (vertexAccount.ContainsKey(pos))
                        {
                            oface.Indices.Add((ushort)vertexAccount[pos]);
                        }
                        else
                        {
                            vert = new OMVR.Vertex();
                            vert.Position = pos;
                            vert.TexCoord = new OMV.Vector2(vface.uv1.U, vface.uv1.V);
                            vert.Normal = new OMV.Vector3(vface.n1.X, vface.n1.Y, vface.n1.Z);
                            oface.Vertices.Add(vert);
                            indx = oface.Vertices.Count - 1;
                            vertexAccount.Add(pos, indx);
                            oface.Indices.Add((ushort)indx);
                        }

                        pos = new OMV.Vector3(vface.v2.X, vface.v2.Y, vface.v2.Z);
                        if (vertexAccount.ContainsKey(pos))
                        {
                            oface.Indices.Add((ushort)vertexAccount[pos]);
                        }
                        else
                        {
                            vert = new OMVR.Vertex();
                            vert.Position = pos;
                            vert.TexCoord = new OMV.Vector2(vface.uv2.U, vface.uv2.V);
                            vert.Normal = new OMV.Vector3(vface.n2.X, vface.n2.Y, vface.n2.Z);
                            oface.Vertices.Add(vert);
                            indx = oface.Vertices.Count - 1;
                            vertexAccount.Add(pos, indx);
                            oface.Indices.Add((ushort)indx);
                        }

                        pos = new OMV.Vector3(vface.v3.X, vface.v3.Y, vface.v3.Z);
                        if (vertexAccount.ContainsKey(pos))
                        {
                            oface.Indices.Add((ushort)vertexAccount[pos]);
                        }
                        else
                        {
                            vert = new OMVR.Vertex();
                            vert.Position = pos;
                            vert.TexCoord = new OMV.Vector2(vface.uv3.U, vface.uv3.V);
                            vert.Normal = new OMV.Vector3(vface.n3.X, vface.n3.Y, vface.n3.Z);
                            oface.Vertices.Add(vert);
                            indx = oface.Vertices.Count - 1;
                            vertexAccount.Add(pos, indx);
                            oface.Indices.Add((ushort)indx);
                        }
                    }
                }
                if (faceVertices > 0)
                {
                    oface.TextureFace = prim.Textures.FaceTextures[ii];
                    if (oface.TextureFace == null)
                    {
                        oface.TextureFace = prim.Textures.DefaultTexture;
                    }
                    oface.ID = ii;
                    omvrmesh.Faces.Add(oface);
                }
            }

            return omvrmesh;
        }
    }
}
