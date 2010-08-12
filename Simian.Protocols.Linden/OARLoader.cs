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
using OpenMetaverse.Assets;

namespace Simian.Protocols.Linden
{
    [SceneModule("OARLoader")]
    public class OARLoader : ISceneModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IPrimMesher m_primMesher;
        private IAssetClient m_assetClient;
        private ITerrain m_terrain;
        private float m_lastPercent;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_assetClient = m_scene.Simian.GetAppModule<IAssetClient>();
            if (m_assetClient == null)
            {
                m_log.Error("OARLoader requires an IAssetClient");
                return;
            }

            m_primMesher = m_scene.GetSceneModule<IPrimMesher>();
            m_terrain = m_scene.GetSceneModule<ITerrain>();

            m_scene.AddCommandHandler("loadoar", LoadOARHandler);
        }

        public void Stop()
        {
            m_scene.RemoveCommandHandler("loadoar");
        }

        private void LoadOARHandler(string command, string[] args, bool printHelp)
        {
            if (printHelp || args.Length == 0)
            {
                Console.WriteLine("Replace the contents of this scene with the contents of an OpenSim Archive.\n\nExample: loadoar myscene.oar");
            }
            else
            {
                string filename = String.Join(" ", args).Replace("\"", String.Empty);

                // Basic sanity check
                if (!System.IO.File.Exists(filename))
                {
                    m_log.Error("OAR file not found: " + filename);
                    return;
                }

                // Wipe all existing sim content
                List<ISceneEntity> entities = new List<ISceneEntity>(m_scene.EntityCount());
                m_scene.ForEachEntity(delegate(ISceneEntity entity) { if (!(entity is IScenePresence)) entities.Add(entity); } );
                foreach (ISceneEntity entity in entities)
                    m_scene.EntityRemove(this, entity);
                if (entities.Count > 0)
                    m_log.Info("Wiped " + entities.Count + " scene entities");
                entities = null;

                // Unpack and load the OAR file
                try
                {
                    m_lastPercent = 0f;
                    OarFile.UnpackageArchive(filename, AssetLoadedHandler, TerrainLoadedHandler, ObjectLoadedHandler);
                }
                catch (Exception ex)
                {
                    m_log.Error("Failed to load OAR file " + filename + ": " + ex.Message);
                }
            }
        }

        private void AssetLoadedHandler(OpenMetaverse.Assets.Asset asset, long bytesRead, long totalBytes)
        {
            m_assetClient.StoreAsset(new Asset
            {
                ContentType = LLUtil.LLAssetTypeToContentType((int)asset.AssetType),
                CreationDate = DateTime.UtcNow,
                CreatorID = UUID.Zero,
                Data = asset.AssetData,
                ID = asset.AssetID
            });

            //m_log.DebugFormat("Loaded asset {0} ({1}), {2} bytes", asset.AssetID, asset.AssetType, asset.AssetData.Length);
            PrintProgress(bytesRead, totalBytes);
        }

        private void TerrainLoadedHandler(float[,] terrain, long bytesRead, long totalBytes)
        {
            if (m_terrain != null)
            {
                float[] heightmap = Flatten<float>(terrain);
                m_terrain.SetHeightmap(heightmap);
                m_log.Debug("Loaded terrain");
            }
            else
            {
                m_log.Debug("Skipped terrain, no ITerrain loaded");
            }

            PrintProgress(bytesRead, totalBytes);
        }

        private void ObjectLoadedHandler(AssetPrim linkset, long bytesRead, long totalBytes)
        {
            if (m_primMesher == null)
                return;

            // Get the root prim
            LLPrimitive parent = LLUtil.PrimObjectToLLPrim(linkset.Parent, m_scene, m_primMesher);

            // Get the child prims and sort them by link order
            SortedList<int, LLPrimitive> children = new SortedList<int, LLPrimitive>(linkset.Children.Count);
            for (int i = 0; i < linkset.Children.Count; i++)
                children.Add(linkset.Children[i].LinkNumber, LLUtil.PrimObjectToLLPrim(linkset.Children[i], m_scene, m_primMesher));

            // Set the child prims as children of the root, in order
            foreach (LLPrimitive child in children.Values)
                child.SetParent(parent, false, false);

            // Send updates for everything
            m_scene.EntityAddOrUpdate(this, parent, UpdateFlags.FullUpdate, 0);
            foreach (LLPrimitive child in children.Values)
                m_scene.EntityAddOrUpdate(this, child, UpdateFlags.FullUpdate, 0);

            PrintProgress(bytesRead, totalBytes);
        }

        private void PrintProgress(long bytesRead, long totalBytes)
        {
            float percent = (float)bytesRead / (float)totalBytes;
            if (percent > m_lastPercent + 0.01f)
            {
                m_log.Info((int)(percent * 100f) + "% complete loading OAR file");
                m_lastPercent = percent;
            }
        }

        private static T[] Flatten<T>(T[,] array)
            where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf(array[0, 0]);
            int totalSize = Buffer.ByteLength(array);
            T[] result = new T[totalSize / size];
            Buffer.BlockCopy(array, 0, result, 0, totalSize);
            return result;
        }
    }
}
