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

namespace Simian
{
    /// <summary>
    /// A generic object pool that uses weak references to allow the pool to 
    /// grow and shrink as needed
    /// </summary>
    /// <typeparam name="T">Type of object to pool</typeparam>
    public sealed class ObjectPool<T>
    {
        /// <summary>Factory method for creating pooled objects</summary>
        /// <returns>A new instance of an object to pool</returns>
        public delegate T CreateObjectCallback();

        private int m_capacity;
        private Stack<T> m_hardReferences;
        private Stack<WeakReference> m_weakReferences;
        private CreateObjectCallback m_createCallback;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="capacity">Minimum capacity of the pool</param>
        /// <param name="createCallback">Factory method for creating pooled 
        /// objects</param>
        public ObjectPool(int capacity, CreateObjectCallback createCallback)
        {
            m_capacity = capacity;
            m_hardReferences = new Stack<T>(capacity);
            m_weakReferences = new Stack<WeakReference>();
            m_createCallback = createCallback;

            for (int i = 0; i < capacity; i++)
                m_hardReferences.Push(createCallback());
        }

        /// <summary>
        /// Retrieves a pooled object or creates a new object if the pool is 
        /// empty
        /// </summary>
        /// <returns>An object that can be returned to the pool later</returns>
        public T Pop()
        {
            lock (m_hardReferences)
            {
                // Try to get a hard reference
                if (m_hardReferences.Count > 0)
                    return m_hardReferences.Pop();

                // Try to get a weak reference
                while (m_weakReferences.Count > 0)
                {
                    WeakReference reference = m_weakReferences.Pop();
                    T obj = (T)reference.Target;

                    if (obj != null)
                        return obj;
                }
            }

            // Create a new reference
            return m_createCallback();
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        /// <param name="item">Object to return to the pool</param>
        public void Push(T item)
        {
            if (item == null)
                throw new ArgumentNullException("Items added to the ObjectPool cannot be null");

            lock (m_hardReferences)
            {
                // Try to return it to the hard reference pool first
                if (m_hardReferences.Count < m_capacity)
                    m_hardReferences.Push(item);

                // Return it to the weak reference pool
                m_weakReferences.Push(new WeakReference(item));
            }
        }

        /// <summary>
        /// Rebuilds the weakly reference portion of the object pool, pruning 
        /// object references that have been garbage collected
        /// </summary>
        public void PruneDeadEntries()
        {
            lock (m_hardReferences)
            {
                WeakReference[] stack = m_weakReferences.ToArray();
                m_weakReferences.Clear();

                for (int i = 0; i < stack.Length; i++)
                {
                    if (stack[i].IsAlive)
                        m_weakReferences.Push(stack[i]);
                }
            }
        }
    }
}
