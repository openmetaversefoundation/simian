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
using System.Drawing;
using System.Drawing.Imaging;
using log4net;
using Rednettle.Warp3D;
using OpenMetaverse;
using Simian.Protocols.Linden;

using WarpRenderer = global::Warp3D.Warp3D;

namespace Simian.Renderer.Warp3D
{
    [ApplicationModule("Warp3DRenderer")]
    public class Warp3DRenderer : ISceneRenderer, IApplicationModule
    {
        private static readonly Color4 WATER_COLOR = new Color4(29, 71, 95, 216);

        private static readonly ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IAssetClient m_assetClient;
        private Dictionary<UUID, Color4> m_colors = new Dictionary<UUID, Color4>();
        private bool m_useAntiAliasing = true; // TODO: Make this a config option

        public bool Start(Simian simian)
        {
            m_assetClient = simian.GetAppModule<IAssetClient>();

            return true;
        }

        public void Stop()
        {
        }

        public Image Render(IScene scene, Viewport viewport)
        {
            IPrimMesher primMesher = scene.GetSceneModule<IPrimMesher>();

            m_colors.Clear();

            int width = viewport.Width;
            int height = viewport.Height;

            if (m_useAntiAliasing)
            {
                width *= 2;
                height *= 2;
            }

            WarpRenderer renderer = new WarpRenderer();
            renderer.CreateScene(width, height);
            renderer.Scene.autoCalcNormals = false;

            #region Camera

            warp_Vector pos = ConvertVector(viewport.Position);
            pos.z -= 0.001f; // Works around an issue with the Warp3D camera
            warp_Vector lookat = warp_Vector.add(ConvertVector(viewport.Position), ConvertVector(viewport.LookDirection));

            renderer.Scene.defaultCamera.setPos(pos);
            renderer.Scene.defaultCamera.lookAt(lookat);

            if (viewport.Orthographic)
            {
                renderer.Scene.defaultCamera.isOrthographic = true;
                renderer.Scene.defaultCamera.orthoViewWidth = viewport.OrthoWindowWidth;
                renderer.Scene.defaultCamera.orthoViewHeight = viewport.OrthoWindowHeight;
            }
            else
            {
                float fov = viewport.FieldOfView;
                fov *= 1.75f; // FIXME: ???
                renderer.Scene.defaultCamera.setFov(fov);
            }

            #endregion Camera

            renderer.Scene.addLight("Light1", new warp_Light(new warp_Vector(0.2f, 0.2f, 1f), 0xffffff, 320, 80));
            renderer.Scene.addLight("Light2", new warp_Light(new warp_Vector(-1f, -1f, 1f), 0xffffff, 100, 40));

            ITerrain terrain = scene.GetSceneModule<ITerrain>();
            RegionInfo regionInfo = scene.GetSceneModule<RegionInfo>();

            CreateWater(scene, renderer, terrain);
            CreateTerrain(scene, renderer, terrain, regionInfo);
            if (primMesher != null)
                CreateAllPrims(scene, renderer, primMesher);

            renderer.Render();
            Bitmap bitmap = renderer.Scene.getImage();

            if (m_useAntiAliasing)
                bitmap = Util.ResizeImage(bitmap, viewport.Width, viewport.Height);

            return bitmap;
        }

        public Image RenderDepth(IScene scene, Viewport viewport)
        {
            return null;
        }

        private void CreateWater(IScene scene, WarpRenderer renderer, ITerrain terrain)
        {
            float waterHeight = (terrain != null) ? terrain.WaterHeight : 0f;

            renderer.AddPlane("Water", 256f * 0.5f);
            renderer.Scene.sceneobject("Water").setPos(127.5f, waterHeight, 127.5f);

            renderer.AddMaterial("WaterColor", ConvertColor(WATER_COLOR));
            renderer.Scene.material("WaterColor").setTransparency((byte)((1f - WATER_COLOR.A) * 255f));
            renderer.SetObjectMaterial("Water", "WaterColor");
        }

        private void CreateTerrain(IScene scene, WarpRenderer renderer, ITerrain terrain, RegionInfo regionInfo)
        {
            float[] heightmap = (terrain != null) ? terrain.GetHeightmap() : new float[256 * 256];

            warp_Object obj = new warp_Object(256 * 256, 255 * 255 * 2);

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    int v = y * 256 + x;
                    float height = heightmap[v];

                    warp_Vector pos = ConvertVector(new Vector3(x, y, height));
                    obj.addVertex(new warp_Vertex(pos, (float)x / 255f, (float)(255 - y) / 255f));
                }
            }

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    if (x < 255 && y < 255)
                    {
                        int v = y * 256 + x;

                        // Normal
                        Vector3 v1 = new Vector3(x, y, heightmap[y * 256 + x]);
                        Vector3 v2 = new Vector3(x + 1, y, heightmap[y * 256 + x + 1]);
                        Vector3 v3 = new Vector3(x, y + 1, heightmap[(y + 1) * 256 + x]);
                        warp_Vector norm = ConvertVector(SurfaceNormal(v1, v2, v3));
                        norm = norm.reverse();
                        obj.vertex(v).n = norm;

                        // Triangle 1
                        obj.addTriangle(
                            v,
                            v + 1,
                            v + 256);

                        // Triangle 2
                        obj.addTriangle(
                            v + 256 + 1,
                            v + 256,
                            v + 1);
                    }
                }
            }

            renderer.Scene.addObject("Terrain", obj);

            UUID[] textureIDs = new UUID[4];
            float[] startHeights = new float[4];
            float[] heightRanges = new float[4];
            if (regionInfo != null)
            {
                textureIDs[0] = regionInfo.TerrainDetail0;
                textureIDs[1] = regionInfo.TerrainDetail1;
                textureIDs[2] = regionInfo.TerrainDetail2;
                textureIDs[3] = regionInfo.TerrainDetail3;

                startHeights[0] = regionInfo.TerrainStartHeight00;
                startHeights[1] = regionInfo.TerrainStartHeight01;
                startHeights[2] = regionInfo.TerrainStartHeight10;
                startHeights[3] = regionInfo.TerrainStartHeight11;

                heightRanges[0] = regionInfo.TerrainHeightRange00;
                heightRanges[1] = regionInfo.TerrainHeightRange01;
                heightRanges[2] = regionInfo.TerrainHeightRange10;
                heightRanges[3] = regionInfo.TerrainHeightRange11;
            }

            Bitmap image = TerrainSplat.Splat(heightmap, textureIDs, startHeights, heightRanges, scene.MinPosition, m_assetClient);
            warp_Texture texture = new warp_Texture(image);
            warp_Material material = new warp_Material(texture);
            material.setReflectivity(50);
            renderer.Scene.addMaterial("TerrainColor", material);
            renderer.SetObjectMaterial("Terrain", "TerrainColor");
        }

        private void CreateAllPrims(IScene scene, WarpRenderer renderer, IPrimMesher primMesher)
        {
            if (primMesher == null)
                return;

            scene.ForEachEntity(
                delegate(ISceneEntity entity)
                {
                    if (entity is LLPrimitive)
                        CreatePrim(renderer, (LLPrimitive)entity, primMesher);
                }
            );
        }

        private void CreatePrim(WarpRenderer renderer, LLPrimitive prim, IPrimMesher primMesher)
        {
            const float MIN_SIZE = 2f;

            if (prim.Prim.PrimData.PCode != PCode.Prim)
                return;
            if (prim.Scale.LengthSquared() < MIN_SIZE * MIN_SIZE)
                return;

            RenderingMesh renderMesh;
            DetailLevel lod = DetailLevel.Medium;

            renderMesh = primMesher.GetRenderingMesh(prim, lod);

            if (renderMesh == null)
                return;

            warp_Vector primPos = ConvertVector(prim.ScenePosition);
            warp_Quaternion primRot = ConvertQuaternion(prim.RelativeRotation);

            warp_Matrix m = warp_Matrix.quaternionMatrix(primRot);

            if (prim.Parent != null)
                m.transform(warp_Matrix.quaternionMatrix(ConvertQuaternion(prim.Parent.RelativeRotation)));
            
            warp_Vector primScale = ConvertVector(prim.Scale);

            string primID = prim.ID.ToString();

            // Create the prim faces
            for (int i = 0; i < renderMesh.Faces.Length; i++)
            {
                RenderingMesh.Face face = renderMesh.Faces[i];
                string meshName = primID + "-Face-" + i.ToString();

                warp_Object faceObj = new warp_Object(face.Vertices.Length, face.Indices.Length / 3);

                for (int j = 0; j < face.Vertices.Length; j++)
                {
                    Vertex v = face.Vertices[j];

                    warp_Vector pos = ConvertVector(v.Position);
                    warp_Vector norm = ConvertVector(v.Normal);
                    if (prim.Prim.Sculpt == null || prim.Prim.Sculpt.SculptTexture == UUID.Zero)
                        norm = norm.reverse();
                    warp_Vertex vert = new warp_Vertex(pos, norm, v.TexCoord.X, v.TexCoord.Y);

                    faceObj.addVertex(vert);
                }

                for (int j = 0; j < face.Indices.Length; j += 3)
                {
                    faceObj.addTriangle(
                        face.Indices[j + 0],
                        face.Indices[j + 1],
                        face.Indices[j + 2]);
                }

                Primitive.TextureEntryFace teFace = prim.Prim.Textures.GetFace((uint)i);
                Color4 faceColor = GetFaceColor(teFace);
                string materialName = GetOrCreateMaterial(renderer, faceColor);

                faceObj.transform(m);
                faceObj.setPos(primPos);
                faceObj.scaleSelf(primScale.x, primScale.y, primScale.z);

                renderer.Scene.addObject(meshName, faceObj);

                renderer.SetObjectMaterial(meshName, materialName);
            }
        }

        private Color4 GetFaceColor(Primitive.TextureEntryFace face)
        {
            Color4 color;

            if (m_assetClient == null || face.TextureID == UUID.Zero)
                return face.RGBA;

            if (!m_colors.TryGetValue(face.TextureID, out color))
            {
                // Attempt to fetch the texture metadata
                Asset metadata;
                if (m_assetClient.TryGetAssetMetadata(face.TextureID, "image/x-j2c", out metadata) && metadata.ExtraHeaders != null)
                {
                    string rgbaStr;
                    if (metadata.ExtraHeaders.TryGetValue("X-JPEG2000-RGBA", out rgbaStr))
                    {
                        string[] colorStrs = rgbaStr.Split(',');
                        if (colorStrs.Length == 4)
                        {
                            float r = 0.5f, g = 0.5f, b = 0.5f, a = 1.0f;

                            Single.TryParse(colorStrs[0], out r);
                            Single.TryParse(colorStrs[1], out g);
                            Single.TryParse(colorStrs[2], out b);
                            Single.TryParse(colorStrs[3], out a);

                            color = new Color4(r, g, b, a);
                        }
                    }
                }
                else
                {
                    color = new Color4(0.5f, 0.5f, 0.5f, 1.0f);
                }

                m_colors[face.TextureID] = color;
            }

            return color * face.RGBA;
        }

        private string GetOrCreateMaterial(WarpRenderer renderer, Color4 color)
        {
            string name = color.ToString();
            
            warp_Material material = renderer.Scene.material(name);
            if (material != null)
                return name;

            renderer.AddMaterial(name, ConvertColor(color));
            if (color.A < 1f)
                renderer.Scene.material(name).setTransparency((byte)((1f - color.A) * 255f));
            return name;
        }

        private static warp_Vector ConvertVector(Vector3 vector)
        {
            return new warp_Vector(vector.X, vector.Z, vector.Y);
        }

        private static warp_Quaternion ConvertQuaternion(Quaternion quat)
        {
            return new warp_Quaternion(quat.X, quat.Z, quat.Y, -quat.W);
        }

        private static int ConvertColor(Color4 color)
        {
            int c = warp_Color.getColor((byte)(color.R * 255f), (byte)(color.G * 255f), (byte)(color.B * 255f));
            if (color.A < 1f)
                c |= (byte)(color.A * 255f) << 24;

            return c;
        }

        private static Vector3 SurfaceNormal(Vector3 c1, Vector3 c2, Vector3 c3)
        {
            Vector3 edge1 = new Vector3(c2.X - c1.X, c2.Y - c1.Y, c2.Z - c1.Z);
            Vector3 edge2 = new Vector3(c3.X - c1.X, c3.Y - c1.Y, c3.Z - c1.Z);

            Vector3 normal = Vector3.Cross(edge1, edge2);
            normal.Normalize();

            return normal;
        }
    }
}
