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
    /// Stores three synchronized collections: two mutable dictionaries and an
    /// immutable array. Slower inserts/removes than two normal dictionaries,
    /// but provides safe iteration while maintaining fast hash lookups with
    /// either key
    /// </summary>
    /// <typeparam name="TKey1">First key type to use for hash lookups</typeparam>
    /// <typeparam name="TKey2">Second key type to use for hash lookups</typeparam>
    /// <typeparam name="TValue">Value type to store</typeparam>
    public sealed class MapsAndArray<TKey1, TKey2, TValue>
    {
        private Dictionary<TKey1, TValue> m_dict1;
        private Dictionary<TKey2, TValue> m_dict2;
        private TValue[] m_array;
        private object m_syncRoot = new object();

        /// <summary>Number of values in the collection</summary>
        public int Count { get { return m_dict1.Count; } }

        /// <summary>
        /// Default constructor
        /// </summary>
        public MapsAndArray()
        {
            m_dict1 = new Dictionary<TKey1, TValue>();
            m_dict2 = new Dictionary<TKey2, TValue>();
            m_array = new TValue[0];
        }

        /// <summary>
        /// Add a value to the collection if it does not already exist
        /// </summary>
        /// <param name="key1">First key of the object to add</param>
        /// <param name="key2">Second key of the object to add</param>
        /// <param name="value">Object to add</param>
        /// <returns>True if the client reference was successfully added,
        /// otherwise false if the given key already existed in the collection</returns>
        public bool Add(TKey1 key1, TKey2 key2, TValue value)
        {
            lock (m_syncRoot)
            {
                if (key1 != null && m_dict1.ContainsKey(key1))
                    return false;
                if (key2 != null && m_dict2.ContainsKey(key2))
                    return false;

                if (key1 != null)
                    m_dict1[key1] = value;

                if (key2 != null)
                    m_dict2[key2] = value;

                TValue[] oldArray = m_array;
                int oldLength = oldArray.Length;

                TValue[] newArray = new TValue[oldLength + 1];
                for (int i = 0; i < oldLength; i++)
                    newArray[i] = oldArray[i];
                newArray[oldLength] = value;

                m_array = newArray;
            }

            return true;
        }

        /// <summary>
        /// Updates the collection by adding and/or removing the key1 associated with a value
        /// </summary>
        public void UpdateKey1(TKey2 key2, TKey1 oldKey1, TKey1 newKey1)
        {
            lock (m_syncRoot)
            {
                // Remove the old key1
                if (oldKey1 != null)
                    m_dict1.Remove(oldKey1);

                // Add the new key1
                TValue value;
                if (newKey1 != null && m_dict2.TryGetValue(key2, out value))
                    m_dict1[newKey1] = value;
            }
        }

        /// <summary>
        /// Updates the collection by adding and/or removing the key2 associated with a value
        /// </summary>
        public void UpdateKey2(TKey1 key1, TKey2 oldKey2, TKey2 newKey2)
        {
            lock (m_syncRoot)
            {
                // Remove the old key2
                if (oldKey2 != null)
                    m_dict2.Remove(oldKey2);

                // Add the new key2
                TValue value;
                if (newKey2 != null && m_dict1.TryGetValue(key1, out value))
                    m_dict2[newKey2] = value;
            }
        }

        /// <summary>
        /// Remove a value from the collection
        /// </summary>
        /// <param name="key1">key1 of the value to remove</param>
        /// <param name="key2">key2 of the value to remove</param>
        /// <returns>True if a value was removed, or false if the given key
        /// was not present in the collection</returns>
        public bool Remove(TKey1 key1, TKey2 key2)
        {
            lock (m_syncRoot)
            {
                TValue value;

                if ((key1 != null && m_dict1.TryGetValue(key1, out value)) || (key2 != null && m_dict2.TryGetValue(key2, out value)))
                {
                    if (key1 != null)
                        m_dict1.Remove(key1);
                    if (key2 != null)
                        m_dict2.Remove(key2);

                    TValue[] oldArray = m_array;
                    int oldLength = oldArray.Length;

                    TValue[] newArray = new TValue[oldLength - 1];
                    int j = 0;
                    for (int i = 0; i < oldLength; i++)
                    {
                        if (!oldArray[i].Equals(value))
                            newArray[j++] = oldArray[i];
                    }

                    m_array = newArray;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resets the collection
        /// </summary>
        public void Clear()
        {
            lock (m_syncRoot)
            {
                m_dict1.Clear();
                m_dict2.Clear();
                m_array = new TValue[0];
            }
        }

        /// <summary>
        /// Checks if a key1 is in the collection
        /// </summary>
        /// <param name="key1">key1 to check for</param>
        /// <returns>True if the key was found in the collection, otherwise false</returns>
        public bool ContainsKey(TKey1 key1)
        {
            return m_dict1.ContainsKey(key1);
        }

        /// <summary>
        /// Checks if a key2 is in the collection
        /// </summary>
        /// <param name="key">key2 to check for</param>
        /// <returns>True if the key was found in the collection, otherwise false</returns>
        public bool ContainsKey(TKey2 key2)
        {
            return m_dict2.ContainsKey(key2);
        }

        /// <summary>
        /// Attempts to fetch a value out of the collection
        /// </summary>
        /// <param name="key1">key1 of the value to retrieve</param>
        /// <param name="value">Retrieved value, or default value on lookup failure</param>
        /// <returns>True if the lookup succeeded, otherwise false</returns>
        public bool TryGetValue(TKey1 key1, out TValue value)
        {
            try { return m_dict1.TryGetValue(key1, out value); }
            catch (Exception)
            {
                value = default(TValue);
                return false;
            }
        }

        /// <summary>
        /// Attempts to fetch a value out of the collection
        /// </summary>
        /// <param name="key2">key2 of the client to retrieve</param>
        /// <param name="value">Retrieved value, or default value on lookup failure</param>
        /// <returns>True if the lookup succeeded, otherwise false</returns>
        public bool TryGetValue(TKey2 key2, out TValue value)
        {
            try { return m_dict2.TryGetValue(key2, out value); }
            catch (Exception)
            {
                value = default(TValue);
                return false;
            }
        }

        /// <summary>
        /// Performs a given task for each of the elements in the
        /// collection
        /// </summary>
        /// <param name="action">Action to perform on each element</param>
        public void ForEach(Action<TValue> action)
        {
            TValue[] localArray = m_array;
            for (int i = 0; i < localArray.Length; i++)
                action(localArray[i]);
        }
    }
}
