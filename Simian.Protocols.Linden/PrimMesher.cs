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

namespace Simian.Protocols.Linden
{
    [SceneModule("PrimMesher")]
    public class PrimMesher : ISceneModule, IPrimMesher
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IAssetClient m_assetClient;
        private IRendering m_renderer;

        public void Start(IScene scene)
        {
            m_assetClient = scene.Simian.GetAppModule<IAssetClient>();

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

        public PhysicsMesh GetPhysicsMesh(LLPrimitive prim)
        {
            if (m_renderer == null)
                return null;

            SimpleMesh simpleMesh = null;

            if (prim.Prim.Sculpt != null && prim.Prim.Sculpt.SculptTexture != UUID.Zero)
            {
                Bitmap sculptTexture = GetSculptMap(prim.Prim.Sculpt.SculptTexture);
                if (sculptTexture != null)
                    simpleMesh = m_renderer.GenerateSimpleSculptMesh(prim.Prim, sculptTexture, OpenMetaverse.Rendering.DetailLevel.Low);
            }
            else
            {
                simpleMesh = m_renderer.GenerateSimpleMesh(prim.Prim, OpenMetaverse.Rendering.DetailLevel.Medium);
            }

            if (simpleMesh != null)
            {
                PhysicsMesh mesh = new PhysicsMesh();
                mesh.Vertices = new Vector3[simpleMesh.Vertices.Count];
                for (int i = 0; i < simpleMesh.Vertices.Count; i++)
                    mesh.Vertices[i] = simpleMesh.Vertices[i].Position;
                mesh.Indices = simpleMesh.Indices.ToArray();

                return mesh;
            }
            else
            {
                return null;
            }
        }

        public RenderingMesh GetRenderingMesh(LLPrimitive prim, DetailLevel lod)
        {
            if (m_renderer == null)
                return null;

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
                Bitmap sculptTexture = GetSculptMap(prim.Prim.Sculpt.SculptTexture);
                if (sculptTexture != null)
                    facetedMesh = m_renderer.GenerateFacetedSculptMesh(prim.Prim, sculptTexture, detailLevel);
            }
            else
            {
                facetedMesh = m_renderer.GenerateFacetedMesh(prim.Prim, detailLevel);
            }

            if (facetedMesh != null)
            {
                RenderingMesh mesh = new RenderingMesh();
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
