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
    public static class RayAABB
    {
        /// <summary>
        /// A reasonably fast Ray-AABB collision test that returns true/false 
        /// and the collision point along the ray
        /// </summary>
        public static bool CollisionTestSmits(AABB b, Ray r, out float tNear, out float tFar)
        {
            // This source code accompanies the Journal of Graphics Tools paper:
            // "Fast Ray-Axis Aligned Bounding Box Overlap Tests With Pluecker Coordinates" by
            // Jeffrey Mahovsky and Brian Wyvill
            // Department of Computer Science, University of Calgary
            // This source code is public domain, but please mention us if you use it.

            tNear = -1.0e6f;
            tFar = 1.0e6f;

            switch (r.Type)
            {
                case Ray.RayType.MMM:
                    {
                        // multiply by the inverse instead of dividing
                        float t1 = (b.Max.X - r.X) * r.II;
                        float t2 = (b.Min.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Y - r.Y) * r.IJ;
                        float t2 = (b.Min.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Z - r.Z) * r.IK;
                        float t2 = (b.Min.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
                case Ray.RayType.MMP:
                    {
                        float t1 = (b.Max.X - r.X) * r.II;
                        float t2 = (b.Min.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Y - r.Y) * r.IJ;
                        float t2 = (b.Min.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Z - r.Z) * r.IK;
                        float t2 = (b.Max.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
                case Ray.RayType.MPM:
                    {
                        float t1 = (b.Max.X - r.X) * r.II;
                        float t2 = (b.Min.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Y - r.Y) * r.IJ;
                        float t2 = (b.Max.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Z - r.Z) * r.IK;
                        float t2 = (b.Min.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
                case Ray.RayType.MPP:
                    {
                        float t1 = (b.Max.X - r.X) * r.II;
                        float t2 = (b.Min.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Y - r.Y) * r.IJ;
                        float t2 = (b.Max.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Z - r.Z) * r.IK;
                        float t2 = (b.Max.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
                case Ray.RayType.PMM:
                    {
                        float t1 = (b.Min.X - r.X) * r.II;
                        float t2 = (b.Max.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Y - r.Y) * r.IJ;
                        float t2 = (b.Min.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Z - r.Z) * r.IK;
                        float t2 = (b.Min.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
                case Ray.RayType.PMP:
                    {
                        float t1 = (b.Min.X - r.X) * r.II;
                        float t2 = (b.Max.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Y - r.Y) * r.IJ;
                        float t2 = (b.Min.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Z - r.Z) * r.IK;
                        float t2 = (b.Max.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
                case Ray.RayType.PPM:
                    {
                        float t1 = (b.Min.X - r.X) * r.II;
                        float t2 = (b.Max.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Y - r.Y) * r.IJ;
                        float t2 = (b.Max.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Max.Z - r.Z) * r.IK;
                        float t2 = (b.Min.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
                case Ray.RayType.PPP:
                    {
                        float t1 = (b.Min.X - r.X) * r.II;
                        float t2 = (b.Max.X - r.X) * r.II;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Y - r.Y) * r.IJ;
                        float t2 = (b.Max.Y - r.Y) * r.IJ;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }
                    {
                        float t1 = (b.Min.Z - r.Z) * r.IK;
                        float t2 = (b.Max.Z - r.Z) * r.IK;

                        if (t1 > tNear)
                            tNear = t1;
                        if (t2 < tFar)
                            tFar = t2;

                        if (tNear > tFar)
                            return false;
                        if (tFar < 0f)
                            return false;
                    }

                    tNear = Math.Max(tNear, 0.0f);
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A very fast Ray-AABB collision test that only returns true/false
        /// </summary>
        public static bool CollisionTestPluecker(AABB b, Ray r)
        {
            if (Contains(b, new Vector3(r.X, r.Y, r.Z)))
                return true;

            // This source code accompanies the Journal of Graphics Tools paper:
            // "Fast Ray-Axis Aligned Bounding Box Overlap Tests With Pluecker Coordinates" by
            // Jeffrey Mahovsky and Brian Wyvill
            // Department of Computer Science, University of Calgary
            // This source code is public domain, but please mention us if you use it.

            switch (r.Type)
            {
                case Ray.RayType.MMM:
                    // side(R,HD) < 0 or side(R,FB) > 0 or side(R,EF) > 0 or side(R,DC) < 0 or side(R,CB) < 0 or side(R,HE) > 0 to miss
                    if ((r.X < b.Min.X) || (r.Y < b.Min.Y) || (r.Z < b.Min.Z) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Max.X < 0f) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Max.X < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Min.Z < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Max.Z > 0f))
                        return false;

                    return true;
                case Ray.RayType.MMP:
                    // side(R,HD) < 0 or side(R,FB) > 0 or side(R,HG) > 0 or side(R,AB) < 0 or side(R,DA) < 0 or side(R,GF) > 0 to miss
                    if ((r.X < b.Min.X) || (r.Y < b.Min.Y) || (r.Z > b.Max.Z) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Max.X < 0f) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Min.X < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Min.Z < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Max.Z > 0f))
                        return false;

                    return true;
                case Ray.RayType.MPM:
                    // side(R,EA) < 0 or side(R,GC) > 0 or side(R,EF) > 0 or side(R,DC) < 0 or side(R,GF) < 0 or side(R,DA) > 0 to miss
                    if ((r.X < b.Min.X) || (r.Y > b.Max.Y) || (r.Z < b.Min.Z) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Min.X < 0f) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Max.X < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Max.Z < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Min.Z > 0f))
                        return false;

                    return true;
                case Ray.RayType.MPP:
                    // side(R,EA) < 0 or side(R,GC) > 0 or side(R,HG) > 0 or side(R,AB) < 0 or side(R,HE) < 0 or side(R,CB) > 0 to miss
                    if ((r.X < b.Min.X) || (r.Y > b.Max.Y) || (r.Z > b.Max.Z) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Min.X < 0f) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Min.X < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Max.Z < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Min.Z > 0f))
                        return false;

                    return true;
                case Ray.RayType.PMM:
                    // side(R,GC) < 0 or side(R,EA) > 0 or side(R,AB) > 0 or side(R,HG) < 0 or side(R,CB) < 0 or side(R,HE) > 0 to miss
                    if ((r.X > b.Max.X) || (r.Y < b.Min.Y) || (r.Z < b.Min.Z) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Max.X < 0f) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Max.X < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Min.Z < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Max.Z > 0f))
                        return false;

                    return true;
                case Ray.RayType.PMP:
                    // side(R,GC) < 0 or side(R,EA) > 0 or side(R,DC) > 0 or side(R,EF) < 0 or side(R,DA) < 0 or side(R,GF) > 0 to miss
                    if ((r.X > b.Max.X) || (r.Y < b.Min.Y) || (r.Z > b.Max.Z) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Max.X < 0f) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Min.X < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Min.Z < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Max.Z > 0f))
                        return false;

                    return true;
                case Ray.RayType.PPM:
                    // side(R,FB) < 0 or side(R,HD) > 0 or side(R,AB) > 0 or side(R,HG) < 0 or side(R,GF) < 0 or side(R,DA) > 0 to miss
                    if ((r.X > b.Max.X) || (r.Y > b.Max.Y) || (r.Z < b.Min.Z) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Min.X < 0f) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Min.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Max.X < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Max.Z < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Min.Z > 0f))
                        return false;

                    return true;
                case Ray.RayType.PPP:
                    // side(R,FB) < 0 or side(R,HD) > 0 or side(R,DC) > 0 or side(R,EF) < 0 or side(R,HE) < 0 or side(R,CB) > 0 to miss
                    if ((r.X > b.Max.X) || (r.Y > b.Max.Y) || (r.Z > b.Max.Z) ||
                        (r.R0 + r.I * b.Max.Y - r.J * b.Min.X < 0f) ||
                        (r.R0 + r.I * b.Min.Y - r.J * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Min.Z - r.K * b.Max.X > 0f) ||
                        (r.R1 + r.I * b.Max.Z - r.K * b.Min.X < 0f) ||
                        (r.R3 - r.K * b.Min.Y + r.J * b.Max.Z < 0f) ||
                        (r.R3 - r.K * b.Max.Y + r.J * b.Min.Z > 0f))
                        return false;

                    return true;
            }

            return false;
        }

        private static bool Contains(AABB a, Vector3 b)
        {
            return
                b.X >= a.Min.X &&
                b.Y >= a.Min.Y &&
                b.Z >= a.Min.Z &&
                b.X <= a.Max.X &&
                b.Y <= a.Max.Y &&
                b.Z <= a.Max.Z;
        }
    }
}
