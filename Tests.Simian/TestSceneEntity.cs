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
using Simian;

namespace Tests.Simian
{
    public class TestSceneEntity : ISceneEntity
    {
        public UUID ID { get; set; }
        public uint LocalID { get; set; }
        public IScene Scene { get { return null; } }

        public string Name { get; set; }
        public UUID OwnerID { get; set; }
        public UUID CreatorID { get; set; }
        public UUID GroupID { get; set; }

        public Vector3 Scale { get; set; }
        public Vector3 RelativePosition { get; set; }
        public Quaternion RelativeRotation { get; set; }
        public Vector3 ScenePosition { get { return RelativePosition; } }
        public Quaternion SceneRotation { get { return RelativeRotation; } }
        public AABB SceneAABB
        {
            get
            {
                Vector3 center = ScenePosition;
                Vector3 halfExtent = Scale * 0.5f;

                Vector3 min = center - halfExtent;
                Vector3 max = center + halfExtent;

                // Rotate the min and max
                Matrix4 rotate = Matrix4.CreateFromQuaternion(SceneRotation);
                min *= rotate;
                max *= rotate;

                // Find the new min/max
                Vector3 newMin = new Vector3(
                    Math.Min(min.X, max.X),
                    Math.Min(min.Y, max.Y),
                    Math.Min(min.Z, max.Z)
                );
                Vector3 newMax = new Vector3(
                    Math.Max(min.X, max.X),
                    Math.Max(min.Y, max.Y),
                    Math.Max(min.Z, max.Z)
                );

                return new AABB(newMin, newMax);
            }
        }

        public Vector3 LastRelativePosition { get; set; }
        public Quaternion LastRelativeRotation { get; set; }
        public Vector3 LastSignificantPosition { get; set; }

        public Vector3d GlobalPosition { get { return Vector3d.Zero; } }

        public TestSceneEntity(UUID id, uint localID, string name, Vector3 scale, Vector3 position, Quaternion rotation)
        {
            ID = id;
            LocalID = localID;
            Name = name;
            Scale = scale;
            RelativePosition = position;
            RelativeRotation = rotation;
        }

        public TestSceneEntity(UUID id, uint localID, string name, Vector3 scale, Vector3 position, Quaternion rotation, UUID ownerID, UUID creatorID)
        {
            ID = id;
            LocalID = localID;
            Name = name;
            Scale = scale;
            RelativePosition = position;
            RelativeRotation = rotation;
            OwnerID = ownerID;
            CreatorID = creatorID;
        }

        public void MarkAsModified()
        {
        }

        public override string ToString()
        {
            return String.Format("{0} (ID: {1}, LocalID: {2}", Name, ID, LocalID);
        }
    }
}
