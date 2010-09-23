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
using OpenMetaverse.Rendering;
using ConvexDecompositionDotNet;

using OpenMetaverseMesh = OpenMetaverse.Rendering.SimpleMesh;

namespace Simian.Protocols.Linden
{
    [SceneModule("PrimMesher")]
    public class PrimMesher : ISceneModule, IPrimMesher
    {
        private const DetailLevel BASIC_MESH_LOD = DetailLevel.High;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IAssetClient m_assetClient;
        private IRendering m_renderer;
        private MeshCache m_meshCache;

        public void Start(IScene scene)
        {
            m_assetClient = scene.Simian.GetAppModule<IAssetClient>();
            m_meshCache = scene.Simian.GetAppModule<MeshCache>();

            List<string> rendererNames = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (rendererNames.Count == 0)
            {
                m_log.Error("PrimMesher could not find a valid OpenMetaverse.Rendering.*.dll plugin");
                return;
            }

            // TODO: Add a config option to allow a preferred renderer to be selected
            m_renderer = RenderingLoader.LoadRenderer(rendererNames[0]);
            m_log.Debug(scene.Name + " is meshing prims with " + m_renderer);
        }

        public void Stop()
        {
        }

        public BasicMesh GetBasicMesh(LLPrimitive prim)
        {
            BasicMesh mesh;
            OpenMetaverseMesh omvMesh = null;
            ulong physicsKey = prim.GetPhysicsKey();

            // Try a cache lookup first
            if (m_meshCache != null && m_meshCache.TryGetBasicMesh(physicsKey, BASIC_MESH_LOD, out mesh))
                return mesh;

            // Can't go any further without a prim renderer
            if (m_renderer == null)
                return null;

            if (prim.Prim.Sculpt != null && prim.Prim.Sculpt.SculptTexture != UUID.Zero)
            {
                // Sculpty meshing
                Bitmap sculptTexture = GetSculptMap(prim.Prim.Sculpt.SculptTexture);
                if (sculptTexture != null)
                    omvMesh = m_renderer.GenerateSimpleSculptMesh(prim.Prim, sculptTexture, OpenMetaverse.Rendering.DetailLevel.Low);
            }
            else
            {
                // Basic prim meshing
                omvMesh = m_renderer.GenerateSimpleMesh(prim.Prim, OpenMetaverse.Rendering.DetailLevel.Medium);
            }

            if (omvMesh == null)
                return null;

#if DEBUG
            for (int i = 0; i < omvMesh.Indices.Count; i++)
                System.Diagnostics.Debug.Assert(omvMesh.Indices[i] < omvMesh.Vertices.Count, "Mesh index is out of range");
#endif

            // Convert the OpenMetaverse.Rendering mesh to a BasicMesh
            mesh = new BasicMesh();
            mesh.Vertices = new Vector3[omvMesh.Vertices.Count];
            for (int i = 0; i < omvMesh.Vertices.Count; i++)
                mesh.Vertices[i] = omvMesh.Vertices[i].Position;
            mesh.Indices = omvMesh.Indices.ToArray();

            mesh.Volume = Util.GetMeshVolume(mesh, Vector3.One);

            // Store the result in the mesh cache, if we have one
            if (m_meshCache != null)
                m_meshCache.StoreBasicMesh(physicsKey, BASIC_MESH_LOD, mesh);

            return mesh;
        }

        public ConvexHullSet GetConvexHulls(LLPrimitive prim)
        {
            ConvexHullSet hullSet;
            ulong physicsKey = prim.GetPhysicsKey();

            // Try a cache lookup first
            if (m_meshCache != null && m_meshCache.TryGetConvexHullSet(physicsKey, BASIC_MESH_LOD, out hullSet))
                return hullSet;

            // Get a mesh and convert it to a set of convex hulls
            BasicMesh mesh = GetBasicMesh(prim);
            if (mesh == null)
                return null;

            #region Convex Decomposition

            List<float3> vertices = new List<float3>(mesh.Vertices.Length);
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                Vector3 pos = mesh.Vertices[i];
                vertices.Add(new float3(pos.X, pos.Y, pos.Z));
            }
            List<int> indices = new List<int>(mesh.Indices.Length);
            for (int i = 0; i < mesh.Indices.Length; i++)
                indices.Add(mesh.Indices[i]);
            List<ConvexResult> results = new List<ConvexResult>();
            ConvexDecompositionCallback cb = delegate(ConvexResult cr)
            {
                results.Add(cr);
            };
            ConvexBuilder builder = new ConvexBuilder(cb);
            builder.process(new DecompDesc { mCallback = cb, mVertices = vertices, mIndices = indices });

            #endregion Convex Decomposition

            if (results.Count == 0)
                return null;

            #region Conversion to ConvexHullSet

            hullSet = new ConvexHullSet();
            hullSet.Volume = mesh.Volume;
            hullSet.Parts = new ConvexHullSet.HullPart[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                ConvexResult result = results[i];

                Vector3[] v3Vertices = new Vector3[result.HullVertices.Count];
                for (int j = 0; j < result.HullVertices.Count; j++)
                {
                    float3 pos = result.HullVertices[j];
                    v3Vertices[j] = new Vector3(pos.x, pos.y, pos.z);
                }

                hullSet.Parts[i] = new ConvexHullSet.HullPart { Offset = Vector3.Zero, Vertices = v3Vertices };
            }

            #endregion Conversion to ConvexHullSet

            // Store the result in the mesh cache, if we have one
            if (m_meshCache != null)
                m_meshCache.StoreConvexHullSet(physicsKey, BASIC_MESH_LOD, hullSet);

            return hullSet;
        }

        public RenderingMesh GetRenderingMesh(LLPrimitive prim, DetailLevel lod)
        {
            RenderingMesh mesh;
            ulong physicsKey = prim.GetPhysicsKey();

            // Try a cache lookup first
            if (m_meshCache != null && m_meshCache.TryGetRenderingMesh(physicsKey, lod, out mesh))
                return mesh;

            // Can't go any further without a prim renderer
            if (m_renderer == null)
                return null;

            // Convert our DetailLevel to the OpenMetaverse.Rendering DetailLevel
            OpenMetaverse.Rendering.DetailLevel detailLevel;
            switch (lod)
            {
                case DetailLevel.Low:
                    detailLevel = OpenMetaverse.Rendering.DetailLevel.Low;
                    break;
                case DetailLevel.Medium:
                    detailLevel = OpenMetaverse.Rendering.DetailLevel.Medium;
                    break;
                case DetailLevel.High:
                    detailLevel = OpenMetaverse.Rendering.DetailLevel.High;
                    break;
                case DetailLevel.Highest:
                default:
                    detailLevel = OpenMetaverse.Rendering.DetailLevel.Highest;
                    break;
            }

            FacetedMesh facetedMesh = null;

            if (prim.Prim.Sculpt != null && prim.Prim.Sculpt.SculptTexture != UUID.Zero)
            {
                // Sculpty meshing
                Bitmap sculptTexture = GetSculptMap(prim.Prim.Sculpt.SculptTexture);
                if (sculptTexture != null)
                    facetedMesh = m_renderer.GenerateFacetedSculptMesh(prim.Prim, sculptTexture, detailLevel);
            }
            else
            {
                // Basic prim meshing
                facetedMesh = m_renderer.GenerateFacetedMesh(prim.Prim, detailLevel);
            }

            if (facetedMesh != null)
            {
                #region FacetedMesh to RenderingMesh Conversion

                mesh = new RenderingMesh();
                mesh.Faces = new RenderingMesh.Face[facetedMesh.Faces.Count];
                for (int i = 0; i < facetedMesh.Faces.Count; i++)
                {
                    Face face = facetedMesh.Faces[i];

                    Vertex[] vertices = new Vertex[face.Vertices.Count];
                    for (int j = 0; j < face.Vertices.Count; j++)
                    {
                        OpenMetaverse.Rendering.Vertex omvrVertex = face.Vertices[j];
                        vertices[j] = new Vertex { Position = omvrVertex.Position, Normal = omvrVertex.Normal, TexCoord = omvrVertex.TexCoord };
                    }
                    ushort[] indices = face.Indices.ToArray();

                    mesh.Faces[i] = new RenderingMesh.Face { Vertices = vertices, Indices = indices };
                }

                #endregion FacetedMesh to RenderingMesh Conversion

                // Store the result in the mesh cache, if we have one
                if (m_meshCache != null)
                    m_meshCache.StoreRenderingMesh(physicsKey, lod, mesh);

                return mesh;
            }
            else
            {
                return null;
            }
        }

        private Bitmap GetSculptMap(UUID textureID)
        {
            Bitmap sculptTexture = null;

            if (m_assetClient != null)
            {
                Asset textureAsset;
                if (m_assetClient.TryGetAsset(textureID, "image/x-j2c", out textureAsset))
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
                    m_log.Warn("Failed to fetch sculpt texture asset " + textureID);
                }
            }

            return sculptTexture;
        }
    }
}
