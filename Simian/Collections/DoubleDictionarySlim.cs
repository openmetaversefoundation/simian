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
    public class DoubleDictionarySlim<TKey1, TKey2, TValue>
    {
        Dictionary<TKey1, TValue> Dictionary1;
        Dictionary<TKey2, TValue> Dictionary2;
        object m_syncRoot = new object();

        public DoubleDictionarySlim()
        {
            Dictionary1 = new Dictionary<TKey1, TValue>();
            Dictionary2 = new Dictionary<TKey2, TValue>();
        }

        public DoubleDictionarySlim(int capacity)
        {
            Dictionary1 = new Dictionary<TKey1, TValue>(capacity);
            Dictionary2 = new Dictionary<TKey2, TValue>(capacity);
        }

        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            lock (m_syncRoot)
            {
                if (Dictionary1.ContainsKey(key1))
                {
                    if (!Dictionary2.ContainsKey(key2))
                        throw new ArgumentException("key1 exists in the dictionary but not key2");
                }
                else if (Dictionary2.ContainsKey(key2))
                {
                    if (!Dictionary1.ContainsKey(key1))
                        throw new ArgumentException("key2 exists in the dictionary but not key1");
                }

                Dictionary1[key1] = value;
                Dictionary2[key2] = value;
            }
        }

        public bool Remove(TKey1 key1, TKey2 key2)
        {
            bool success;

            lock (m_syncRoot)
            {
                Dictionary1.Remove(key1);
                success = Dictionary2.Remove(key2);
            }

            return success;
        }

        public bool Remove(TKey1 key1)
        {
            bool found = false;

            lock (m_syncRoot)
            {
                // This is an O(n) operation!
                TValue value;
                if (Dictionary1.TryGetValue(key1, out value))
                {
                    foreach (KeyValuePair<TKey2, TValue> kvp in Dictionary2)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            Dictionary1.Remove(key1);
                            Dictionary2.Remove(kvp.Key);
                            found = true;
                            break;
                        }
                    }
                }
            }

            return found;
        }

        public bool Remove(TKey2 key2)
        {
            bool found = false;

            lock (m_syncRoot)
            {
                // This is an O(n) operation!
                TValue value;
                if (Dictionary2.TryGetValue(key2, out value))
                {
                    foreach (KeyValuePair<TKey1, TValue> kvp in Dictionary1)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            Dictionary2.Remove(key2);
                            Dictionary1.Remove(kvp.Key);
                            found = true;
                            break;
                        }
                    }
                }
            }

            return found;
        }

        public void Clear()
        {
            lock (m_syncRoot)
            {
                Dictionary1.Clear();
                Dictionary2.Clear();
            }
        }

        public int Count
        {
            get { return Dictionary1.Count; }
        }

        public bool ContainsKey(TKey1 key)
        {
            return Dictionary1.ContainsKey(key);
        }

        public bool ContainsKey(TKey2 key)
        {
            return Dictionary2.ContainsKey(key);
        }

        public bool TryGetValue(TKey1 key, out TValue value)
        {
            bool success;

            lock (m_syncRoot)
                success = Dictionary1.TryGetValue(key, out value);

            return success;
        }

        public bool TryGetValue(TKey2 key, out TValue value)
        {
            bool success;

            lock (m_syncRoot)
                success = Dictionary2.TryGetValue(key, out value);

            return success;
        }

        public void ForEach(Action<TValue> action)
        {
            lock (m_syncRoot)
            {
                foreach (TValue value in Dictionary1.Values)
                    action(value);
            }
        }

        public void ForEach(Action<KeyValuePair<TKey1, TValue>> action)
        {
            lock (m_syncRoot)
            {
                foreach (KeyValuePair<TKey1, TValue> entry in Dictionary1)
                    action(entry);
            }
        }

        public void ForEach(Action<KeyValuePair<TKey2, TValue>> action)
        {
            lock (m_syncRoot)
            {
                foreach (KeyValuePair<TKey2, TValue> entry in Dictionary2)
                    action(entry);
            }
        }

        public TValue FindValue(Predicate<TValue> predicate)
        {
            lock (m_syncRoot)
            {
                foreach (TValue value in Dictionary1.Values)
                {
                    if (predicate(value))
                        return value;
                }
            }

            return default(TValue);
        }

        public IList<TValue> FindAll(Predicate<TValue> predicate)
        {
            IList<TValue> list = new List<TValue>();
            
            lock (m_syncRoot)
            {
                foreach (TValue value in Dictionary1.Values)
                {
                    if (predicate(value))
                        list.Add(value);
                }
            }

            return list;
        }

        public int RemoveAll(Predicate<TValue> predicate)
        {
            IList<TKey1> list = new List<TKey1>();

            lock (m_syncRoot)
            {
                foreach (KeyValuePair<TKey1, TValue> kvp in Dictionary1)
                {
                    if (predicate(kvp.Value))
                        list.Add(kvp.Key);
                }

                IList<TKey2> list2 = new List<TKey2>(list.Count);
                foreach (KeyValuePair<TKey2, TValue> kvp in Dictionary2)
                {
                    if (predicate(kvp.Value))
                        list2.Add(kvp.Key);
                }

                for (int i = 0; i < list.Count; i++)
                    Dictionary1.Remove(list[i]);

                for (int i = 0; i < list2.Count; i++)
                    Dictionary2.Remove(list2[i]);
            }

            return list.Count;
        }
    }
}
