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
using System.IO;
using System.Text;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    [SceneModule("LLPrimitiveLoader")]
    public class LLPrimitiveLoader : ISceneModule
    {
        private struct PrimSerialization
        {
            public UUID ID;
            public LLPrimitive Prim;
        }

        private static readonly string PRIM_CONTENT_TYPE = LLUtil.LLAssetTypeToContentType((int)AssetType.Object);

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IScene m_scene;
        private IDataStore m_dataStore;
        private IPrimMesher m_primMesher;
        private ILSLScriptEngine m_scriptEngine;
        ThrottledQueue<UUID, PrimSerialization> m_writeQueue;

        public void Start(IScene scene)
        {
            m_scene = scene;

            m_dataStore = m_scene.Simian.GetAppModule<IDataStore>();
            if (m_dataStore == null)
            {
                m_log.Error("LLPrimitiveLoader requires an IDataStore");
                return;
            }

            m_primMesher = m_scene.GetSceneModule<IPrimMesher>();
            if (m_primMesher == null)
            {
                m_log.Error("LLPrimitiveLoader requires an IPrimMesher");
                return;
            }

            m_scriptEngine = m_scene.GetSceneModule<ILSLScriptEngine>();

            m_writeQueue = new ThrottledQueue<UUID, PrimSerialization>(5, 1000 * 30, true, SerializationHandler);
            m_writeQueue.Start();

            m_scene.OnEntityAddOrUpdate += EntityAddOrUpdateHandler;
            m_scene.OnEntityRemove += EntityRemoveHandler;

            Deserialize();
        }

        public void Stop()
        {
            m_scene.OnEntityAddOrUpdate -= EntityAddOrUpdateHandler;
            m_scene.OnEntityRemove -= EntityRemoveHandler;

            m_log.Debug("Finishing LLPrimitive serializations for " + m_scene.Name + "...");
            m_writeQueue.Stop(true);
        }

        private void EntityAddOrUpdateHandler(object sender, EntityAddOrUpdateArgs e)
        {
            if (sender != this && e.Entity is LLPrimitive && (e.UpdateFlags != 0 || e.ExtraFlags != 0))
            {
                LLPrimitive prim = (LLPrimitive)e.Entity;
                ILinkable parent = prim.Parent;

                if (parent == null)
                    m_writeQueue.Add(prim.ID, new PrimSerialization { ID = prim.ID, Prim = prim });
                else if (parent is LLPrimitive)
                    m_writeQueue.Add(parent.ID, new PrimSerialization { ID = parent.ID, Prim = (LLPrimitive)parent });
            }
        }

        private void EntityRemoveHandler(object sender, EntityArgs e)
        {
            if (e.Entity is LLPrimitive)
                m_writeQueue.Add(e.Entity.ID, new PrimSerialization { ID = e.Entity.ID, Prim = null });
        }

        private void SerializationHandler(PrimSerialization serialization)
        {
            if (serialization.Prim != null && serialization.Prim.Parent != null)
            {
                m_log.Debug("Skipping serialization for child prim " + serialization.Prim.ID);
                return;
            }

            SerializedData item = new SerializedData
            {
                ContentType = PRIM_CONTENT_TYPE,
                Name = serialization.ID.ToString(),
                Section = "llprimitives",
                StoreID = m_scene.ID,
                Version = 1
            };

            // Removes set item.Data = null, signaling a delete. Adds set item.Data to the serialized prim data
            if (serialization.Prim != null)
                item.Data = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(LLPrimitive.SerializeLinkset(serialization.Prim)));

            m_dataStore.BeginSerialize(item);
        }

        private void Deserialize()
        {
            IList<SerializedData> items = m_dataStore.Deserialize(m_scene.ID, "llprimitives");

            int linksetCount = 0;
            int primCount = 0;

            for (int i = 0; i < items.Count; i++)
            {
                SerializedData item = items[i];

                using (MemoryStream stream = new MemoryStream(item.Data))
                {
                    OSDMap linksetMap = OSDParser.DeserializeJson(stream) as OSDMap;

                    if (linksetMap != null)
                    {
                        IList<LLPrimitive> linkset = LLPrimitive.DeserializeLinkset(linksetMap, m_scene, m_primMesher, false);

                        // Rez the parent(s) first
                        for (int j = 0; j < linkset.Count; j++)
                        {
                            LLPrimitive prim = linkset[j];
                            if (prim.Parent == null)
                                m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
                        }

                        // Rez the children
                        for (int j = 0; j < linkset.Count; j++)
                        {
                            LLPrimitive prim = linkset[j];
                            if (prim.Parent != null)
                                m_scene.EntityAddOrUpdate(this, prim, UpdateFlags.FullUpdate, 0);
                        }

                        // Start any scripts
                        for (int j = 0; j < linkset.Count; j++)
                            StartScripts(linkset[j]);

                        ++linksetCount;
                        primCount += linkset.Count;
                    }
                    else
                    {
                        m_log.WarnFormat("Failed to deserialize store object {0} ({1} bytes), Content-Type={2}, Version={3}",
                            item.Name, item.Data.Length, item.ContentType, item.Version);
                    }
                }
            }

            m_log.DebugFormat("Deserialized and loaded {0} LLPrimitives in {1} linksets", primCount, linksetCount);
        }

        private void StartScripts(LLPrimitive prim)
        {
            if (m_scriptEngine == null)
                return;

            IList<LLInventoryTaskItem> scriptItems = prim.Inventory.FindAllItems(item => item.AssetType == AssetType.LSLText);
            for (int i = 0; i < scriptItems.Count; i++)
            {
                LLInventoryItem item = scriptItems[i];
                m_scriptEngine.RezScript(item.ID, item.AssetID, prim, 0);
            }
        }
    }
}
