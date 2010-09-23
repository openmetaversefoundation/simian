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
using System.Threading;
using log4net;
using OpenMetaverse;

namespace Simian
{
    public sealed class SceneGraph
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private readonly Dictionary<uint, ISceneEntity> m_entityLocalIDs = new Dictionary<uint, ISceneEntity>();
        private readonly Dictionary<UUID, ISceneEntity> m_entityUUIDs = new Dictionary<UUID, ISceneEntity>();
        private readonly MapAndArray<UUID, IScenePresence> m_presences = new MapAndArray<UUID, IScenePresence>();
        private readonly System.Threading.ReaderWriterLockSlim m_syncRoot = new System.Threading.ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public int EntityCount { get { return m_entityLocalIDs.Count; } }
        public int PresenceCount { get { return m_presences.Count; } }

        public SceneGraph()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>True if the entity was added to the scene graph, false if 
        /// it was updated</returns>
        public bool AddOrUpdate(ISceneEntity entity)
        {
            bool added;

            m_syncRoot.EnterWriteLock();
            try
            {
                if (!m_entityLocalIDs.ContainsKey(entity.LocalID))
                {
                    // Sanity check
                    if (m_entityUUIDs.ContainsKey(entity.ID))
                        throw new ArgumentException("Cannot add entity with LocalID " + entity.LocalID + ", ID " + entity.ID + " already exists in the scene");

                    // Insert this entity into the scene graph, uint map, and UUID map
                    m_entityLocalIDs.Add(entity.LocalID, entity);
                    m_entityUUIDs.Add(entity.ID, entity);
                    added = true;
                }
                else
                {
                    added = false;
                }

                // If this is a scene presence, add/update it in the presence collection
                if (entity is IScenePresence)
                    m_presences.Add(entity.ID, (IScenePresence)entity);
            }
            finally { m_syncRoot.ExitWriteLock(); }

            return added;
        }

        public bool Remove(ISceneEntity entity)
        {
            bool removed = false;

            m_syncRoot.EnterWriteLock();
            try
            {
                // Remove this entity from the uint and UUID maps
                removed |= m_entityLocalIDs.Remove(entity.LocalID);
                removed |= m_entityUUIDs.Remove(entity.ID);

                // If this is a scene presence, remove it from the presence collection
                if (entity is IScenePresence)
                    removed |= m_presences.Remove(entity.ID);
            }
            finally { m_syncRoot.ExitWriteLock(); }

            return removed;
        }

        public bool ContainsKey(UUID id)
        {
            return m_entityUUIDs.ContainsKey(id);
        }

        public bool ContainsKey(uint localID)
        {
            return m_entityLocalIDs.ContainsKey(localID);
        }

        public IScenePresence[] GetPresenceArray()
        {
            return m_presences.GetArray();
        }

        public bool TryGetEntity(UUID id, out ISceneEntity entity)
        {
            // Standard thread-safe lookup
            m_syncRoot.EnterReadLock();
            try
            {
                return m_entityUUIDs.TryGetValue(id, out entity);
            }
            finally { m_syncRoot.ExitReadLock(); }
        }

        public bool TryGetEntity(uint localID, out ISceneEntity entity)
        {
            // Standard thread-safe lookup
            m_syncRoot.EnterReadLock();
            try
            {
                return m_entityLocalIDs.TryGetValue(localID, out entity);
            }
            finally { m_syncRoot.ExitReadLock(); }
        }

        public bool TryGetPresence(UUID id, out IScenePresence presence)
        {
            // The presence map tends to be smaller and there is no indirection
            // through a scene graph node, so this is a faster lookup path for 
            // presences
            m_syncRoot.EnterReadLock();
            try { return m_presences.TryGetValue(id, out presence); }
            finally { m_syncRoot.ExitReadLock(); }
        }

        public void ForEachEntity(Action<ISceneEntity> action)
        {
            // Standard thread-safe iteration. Note that our ReaderWriterLockSlim
            // does not support recursion, so attempts to access the scene graph
            // in one of the callbacks will throw an exception
            m_syncRoot.EnterReadLock();
            try
            {
                foreach (ISceneEntity entity in m_entityLocalIDs.Values)
                {
                    try { action(entity); }
                    catch (Exception ex) { m_log.Error("ForEachEntity() caught an exception: " + ex); }
                }
            }
            finally { m_syncRoot.ExitReadLock(); }
        }

        public void ForEachPresence(Action<IScenePresence> action)
        {
            // The immutable array of scene presences allows us to do lockless
            // iteration of the presence list
            IScenePresence[] presences = m_presences.GetArray();

            for (int i = 0; i < presences.Length; i++)
            {
                try { action(presences[i]); }
                catch (Exception ex) { m_log.Error("ForEachPresence() caught an exception for presence \"" + presences[i].Name + "\": " + ex); }
            }
        }

        public ISceneEntity FindEntity(Predicate<ISceneEntity> predicate)
        {
            // Standard thread-safe iteration. Note that our ReaderWriterLockSlim
            // does not support recursion, so attempts to access the scene graph
            // in one of the callbacks will throw an exception
            m_syncRoot.EnterReadLock();
            try
            {
                foreach (ISceneEntity entity in m_entityLocalIDs.Values)
                {
                    if (predicate(entity))
                        return entity;
                }
            }
            finally { m_syncRoot.ExitReadLock(); }

            return null;
        }

        public IScenePresence FindPresence(Predicate<IScenePresence> predicate)
        {
            // The immutable array of scene presences allows us to do lockless
            // iteration of the presence list
            IScenePresence[] presences = m_presences.GetArray();

            for (int i = 0; i < presences.Length; i++)
            {
                try
                {
                    if (predicate(presences[i]))
                        return presences[i];
                }
                catch (Exception ex) { m_log.Error("FindPresence() caught an exception for presence \"" + presences[i].Name + "\": " + ex); }
            }

            return null;
        }
    }
}
