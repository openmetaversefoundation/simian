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

#define RAY_TRI_CULLING

using System;
using OpenMetaverse;

namespace Simian
{
    public static class RayMesh
    {
        public static bool CollisionTest(Ray ray, IPhysical obj, PhysicsMesh mesh, out float dist)
        {
            Vector3 start = new Vector3(ray.X, ray.Y, ray.Z);
            Vector3 direction = new Vector3(ray.I, ray.J, ray.K);
            dist = Single.MaxValue;

            // Construct a matrix to transform to scene space
            Matrix4 transform = Matrix4.Identity;

            transform *= Matrix4.CreateScale(obj.Scale);
            transform *= Matrix4.CreateFromQuaternion(obj.RelativeRotation);
            transform *= Matrix4.CreateTranslation(obj.RelativePosition);

            ILinkable parent = obj.Parent;
            if (parent != null)
            {
                // Apply parent rotation and translation
                transform *= Matrix4.CreateFromQuaternion(parent.RelativeRotation);
                transform *= Matrix4.CreateTranslation(parent.RelativePosition);
            }

            // Iterate through all of the triangles in the mesh, doing a ray-triangle intersection
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                Vector3 point0 = mesh.Vertices[mesh.Indices[i + 0]] * transform;
                Vector3 point1 = mesh.Vertices[mesh.Indices[i + 1]] * transform;
                Vector3 point2 = mesh.Vertices[mesh.Indices[i + 2]] * transform;

                float thisDist;
                if (RayTriangle.CollisionTestCull(start, direction, point0, point1, point2, out thisDist))
                {
                    if (thisDist < dist)
                        dist = thisDist;
                }
            }

            return dist < Single.MaxValue;
        }
    }
}
