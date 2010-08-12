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
    public class SerializedData
    {
        public UUID StoreID;
        public string Section;
        public string Name;
        public string ContentType;
        public int Version;
        public byte[] Data;
    }

    public interface IDataStore
    {
        bool AddOrUpdateAsset(UUID dataID, string contentType, byte[] data);
        bool AddOrUpdateAsset(UUID dataID, string contentType, byte[] data, TimeSpan expiration);
        bool RemoveAsset(UUID dataID, string contentType);
        bool TryGetAsset(UUID dataID, string contentType, out byte[] data);

        IList<SerializedData> Deserialize(UUID storeID, string section);
        SerializedData DeserializeOne(UUID storeID, string section);

        /// <summary>
        /// Used to serialize data to local storage or remove serialized data.
        /// To delete a serialized item, use this method to save over the item
        /// with SerializedData.Data set to null
        /// </summary>
        /// <param name="item"></param>
        void BeginSerialize(SerializedData item);
    }
}
