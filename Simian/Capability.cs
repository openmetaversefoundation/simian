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
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian
{
    /// <summary>
    /// An opaque URL used for accessing a protected resource
    /// </summary>
    public class Capability
    {
        /// <summary>Unique identifier for this capability</summary>
        public readonly UUID ID;
        /// <summary>ID of the user or resource this capability was created
        /// for</summary>
        public readonly UUID OwnerID;
        /// <summary>Protected resource this capability maps to, or null if a
        /// one time resource callback is used</summary>
        public readonly string Resource;
        /// <summary>One time resource callback this capability maps to, if a
        /// named resource is not used</summary>
        public readonly CapabilityCallback OneTimeResource;
        /// <summary>Set this to false to leave this capability connection 
        /// open. Useful for event queue capabilities</summary>
        public readonly bool SendResponseAfterCallback;

        /// <summary>Gets a printable name of the resource this capability maps
        /// to</summary>
        public string ResourceDisplayName
        {
            get
            {
                if (OneTimeResource != null)
                    return OneTimeResource.Method.Name;
                return Resource;
            }
        }

        /// <summary>
        /// Constructor for a capability mapping to a remote resource
        /// </summary>
        public Capability(UUID id, UUID ownerID, Uri resource)
        {
            ID = id;
            OwnerID = ownerID;
            Resource = resource.ToString();
            SendResponseAfterCallback = true;
        }

        /// <summary>
        /// Constructor for a capability mapping to a local resource
        /// </summary>
        public Capability(UUID id, UUID ownerID, string resource, bool sendResponseAfterCallback)
        {
            ID = id;
            OwnerID = ownerID;
            Resource = resource;
            SendResponseAfterCallback = sendResponseAfterCallback;
        }

        /// <summary>
        /// Constructor for a capability mapping to a one time local resource
        /// </summary>
        public Capability(UUID id, UUID ownerID, CapabilityCallback oneTimeResource, bool sendResponseAfterCallback)
        {
            ID = id;
            OwnerID = ownerID;
            OneTimeResource = oneTimeResource;
            Resource = UUID.Random().ToString();
            SendResponseAfterCallback = sendResponseAfterCallback;
        }
    }
}
