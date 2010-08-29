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
    public interface ISceneEntity
    {
        UUID ID { get; }
        uint LocalID { get; }
        IScene Scene { get; }

        string Name { get; set; }
        UUID OwnerID { get; set; }
        UUID CreatorID { get; set; }
        UUID GroupID { get; set; }
        
        Vector3 Scale { get; set; }
        Vector3 RelativePosition { get; set; }
        Quaternion RelativeRotation { get; set; }
        Vector3 ScenePosition { get; }
        Quaternion SceneRotation { get; }
        AABB SceneAABB { get; }

        /// <summary>Last relative position before any movement was last detected</summary>
        Vector3 LastRelativePosition { get; set; }
        /// <summary>Last relative rotation before any movement was last detected</summary>
        Quaternion LastRelativeRotation { get; set; }
        /// <summary>Last ScenePosition before significant movement was last detected</summary>
        Vector3 LastSignificantPosition { get; set; }

        /// <summary>Marks an entity as modified</summary>
        void MarkAsModified();
    }
}
