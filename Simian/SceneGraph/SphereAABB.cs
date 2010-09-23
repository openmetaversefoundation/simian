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

namespace Simian
{
    public static class SphereAABB
    {
        public static bool CollisionTest(AABB box, Vector3 position, float radius)
        {
            // The center of the sphere relative to the center of the AABB
            Vector3 sphereCenterRelBox = position - box.Center;
            // The point on the surface of the box closest to the center of the sphere
            Vector3 boxPoint;

            float halfXLength = box.XLength * 0.5f;
            float halfYLength = box.YLength * 0.5f;
            float halfZLength = box.ZLength * 0.5f;

            // X
            if (sphereCenterRelBox.X < -halfXLength)
                boxPoint.X = -halfXLength;
            else if (sphereCenterRelBox.X > halfXLength)
                boxPoint.X = halfXLength;
            else
                boxPoint.X = sphereCenterRelBox.X;

            // Y
            if (sphereCenterRelBox.Y < -halfYLength)
                boxPoint.Y = -halfYLength;
            else if (sphereCenterRelBox.Y > halfYLength)
                boxPoint.Y = halfYLength;
            else
                boxPoint.Y = sphereCenterRelBox.Y;

            // Z
            if (sphereCenterRelBox.Z < -halfZLength)
                boxPoint.Z = -halfZLength;
            else if (sphereCenterRelBox.Z > halfZLength)
                boxPoint.Z = halfZLength;
            else
                boxPoint.Z = sphereCenterRelBox.Z;

            // Get the distance from the closest point on the box to the center of the 
            // sphere and test if it is less than the sphere radius
            return Vector3.DistanceSquared(sphereCenterRelBox, boxPoint) < (radius * radius);
        }
    }
}
