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
using OpenMetaverse;

namespace Simian
{
    /// <summary>
    /// A blob of serialized data that can be stored with IDataStore.BeginSerialize()
    /// </summary>
    public class SerializedData
    {
        /// <summary>Unique identifier of the storage bin the serialized data is stored in. Usually
        /// UUID.Zero for global data or a SceneID for scene-specific data</summary>
        public UUID StoreID;
        /// <summary>String identifier for the section of the storage bin the serialized data is 
        /// stored in</summary>
        public string Section;
        /// <summary>Name of the serialized data</summary>
        public string Name;
        /// <summary>MIME type of the serialized data</summary>
        public string ContentType;
        /// <summary>Version number of the serialization format used to store the data</summary>
        public int Version;
        /// <summary>The serialized data blob</summary>
        public byte[] Data;
    }

    public interface IDataStore
    {
        /// <summary>
        /// Adds a new asset or updates an existing asset in the local data store
        /// </summary>
        /// <param name="dataID">Unique identifier for the data</param>
        /// <param name="contentType">MIME type of the data</param>
        /// <param name="data">Asset data to store</param>
        /// <returns>True if a new asset was created, false if an existing asset was updated</returns>
        bool AddOrUpdateAsset(UUID dataID, string contentType, byte[] data);

        /// <summary>
        /// Adds a new asset or updates an existing asset in the local data store
        /// </summary>
        /// <param name="dataID">Unique identifier for the data</param>
        /// <param name="contentType">MIME type of the data</param>
        /// <param name="data">Asset data to store</param>
        /// <param name="temporary">True if this is a temporary asset, false if we should 
        /// permanently store this data</param>
        /// <returns>True if a new asset was created, false if an existing asset was updated</returns>
        bool AddOrUpdateAsset(UUID dataID, string contentType, byte[] data, bool temporary);

        /// <summary>
        /// Removes an asset from the local data store
        /// </summary>
        /// <param name="dataID">Unique identifier for the data to remove</param>
        /// <param name="contentType">Content type of the data to remove</param>
        /// <returns>True if the specified asset was deleted, false if the asset could not be found</returns>
        bool RemoveAsset(UUID dataID, string contentType);

        /// <summary>
        /// Attempt to fetch an asset from the local data store
        /// </summary>
        /// <param name="dataID">Unique identifier for the data to fetch</param>
        /// <param name="contentType">Content type of the data to fetch</param>
        /// <param name="data">The actual data if the fetch was successful</param>
        /// <returns>True if the fetch was successful, otherwise false</returns>
        bool TryGetAsset(UUID dataID, string contentType, out byte[] data);

        /// <summary>
        /// Used to serialize data to local storage or remove serialized data.
        /// To delete a serialized item, use this method to save over the item
        /// with SerializedData.Data set to null
        /// </summary>
        /// <param name="item"></param>
        /// <remarks>The difference between serialization and asset storage is in the access 
        /// guarantees. When a call to AddOrUpdateAsset completes, that asset must be immediately 
        /// fetchable with a call to TryGetAsset. BeginSerialize is a lazy serialization that only 
        /// guarantees the data will be stored before a clean shutdown of the application, and may 
        /// be overwritten in memory by a future serialization before ever being written to disk. 
        /// BeginSerialize is useful for application state caching such as the scene state while 
        /// AddOrUpdateAsset is more suited for content caching where the content may need to be
        /// retrieved in the same session that it is stored</remarks>
        void BeginSerialize(SerializedData item);

        /// <summary>
        /// Deserializes all the data in a given storage bin and section that was stored with a 
        /// call to BeginSerialize
        /// </summary>
        /// <param name="storeID">Unique identifier of the storage bin the serialized data is 
        /// stored in. Usually UUID.Zero for global data or a SceneID for scene-specific data</param>
        /// <param name="section">String identifier for the section of the storage bin the 
        /// serialized data is stored in</param>
        /// <returns>All of the serialized data stored in the specified storage bin and section, or
        /// an empty collection on failure</returns>
        /// <remarks>See <see cref="BeginSerialize"/> for information on the differences between
        /// asset storage and serialization in the IDataStore</remarks>
        IList<SerializedData> Deserialize(UUID storeID, string section);

        /// <summary>
        /// Deserializes exactly one serialized data blob in a given storage bin and section that
        /// was stored with a call to BeginSerialize
        /// </summary>
        /// <param name="storeID">Unique identifier of the storage bin the serialized data is 
        /// stored in. Usually UUID.Zero for global data or a SceneID for scene-specific data</param>
        /// <param name="section">String identifier for the section of the storage bin the 
        /// serialized data is stored in</param>
        /// <returns>The first blob of serialized data found in the given storage bin and section,
        /// or null if no data was found</returns>
        /// <remarks>See <see cref="BeginSerialize"/> for information on the differences between
        /// asset storage and serialization in the IDataStore</remarks>
        SerializedData DeserializeOne(UUID storeID, string section);
    }
}
