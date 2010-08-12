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
    public static class RayTriangle
    {
        /// <summary>
        /// Test a ray and a triangle for intersection, assuming a one-sided 
        /// triangle with clockwise ordering
        /// </summary>
        /// <remarks>Adapted from http://www.cs.virginia.edu/~gfx/Courses/2003/ImageSynthesis/papers/Acceleration/Fast%20MinimumStorage%20RayTriangle%20Intersection.pdf</remarks>
        /// <param name="origin">Origin point of the ray</param>
        /// <param name="direction">Unit vector representing the direction of the ray</param>
        /// <param name="vert0">Position of the first triangle corner</param>
        /// <param name="vert1">Position of the second triangle corner</param>
        /// <param name="vert2">Position of the third triangle corner</param>
        /// <param name="dist">Distance along the ray where the collision occurred</param>
        /// <returns>True if the ray passes through the triangle, otherwise false</returns>
        public static bool CollisionTestCull(Vector3 origin, Vector3 direction, Vector3 vert0, Vector3 vert1, Vector3 vert2, out float dist)
        {
            const float EPSILON = 0.000001f;

            float determinant, invDeterminant;

            dist = Single.NaN;

            // Find vectors for two edges sharing vert0
            Vector3 edge1 = vert1 - vert0;
            Vector3 edge2 = vert2 - vert0;

            // Begin calculating the determinant
            Vector3 pvec = Vector3.Cross(direction, edge2);

            // If the determinant is near zero, ray lies in plane of triangle
            determinant = Vector3.Dot(edge1, pvec);

            if (determinant < EPSILON)
                return false;

            // Calculate distance from vert0 to ray origin
            Vector3 tvec = origin - vert0;

            // Calculate U parameter and test bounds
            float u = Vector3.Dot(tvec, pvec);
            if (u < 0.0f || u > determinant)
                return false;

            // Prepare to test V parameter
            Vector3 qvec = Vector3.Cross(tvec, edge1);

            // Calculate V parameter and test bounds
            float v = Vector3.Dot(direction, qvec);
            if (v < 0.0f || u + v > determinant)
                return false;

            invDeterminant = 1.0f / determinant;
            dist = Vector3.Dot(edge2, qvec) * invDeterminant;

            return dist >= 0f;
        }

        /// <summary>
        /// Test a ray and a triangle for intersection, assuming a double-sided
        /// triangle
        /// </summary>
        /// <remarks>Adapted from http://www.cs.virginia.edu/~gfx/Courses/2003/ImageSynthesis/papers/Acceleration/Fast%20MinimumStorage%20RayTriangle%20Intersection.pdf</remarks>
        /// <param name="origin">Origin point of the ray</param>
        /// <param name="direction">Unit vector representing the direction of the ray</param>
        /// <param name="vert0">Position of the first triangle corner</param>
        /// <param name="vert1">Position of the second triangle corner</param>
        /// <param name="vert2">Position of the third triangle corner</param>
        /// <param name="dist">Distance along the ray where the collision occurred</param>
        /// <returns>True if the ray passes through the triangle, otherwise false</returns>
        public static bool CollisionTestNoCull(Vector3 origin, Vector3 direction, Vector3 vert0, Vector3 vert1, Vector3 vert2, out float dist)
        {
            const float EPSILON = 0.000001f;

            float determinant, invDeterminant;

            dist = Single.NaN;

            // Find vectors for two edges sharing vert0
            Vector3 edge1 = vert1 - vert0;
            Vector3 edge2 = vert2 - vert0;

            // Begin calculating the determinant
            Vector3 pvec = Vector3.Cross(direction, edge2);

            // If the determinant is near zero, ray lies in plane of triangle
            determinant = Vector3.Dot(edge1, pvec);

            if (determinant > -EPSILON && determinant < EPSILON)
                return false;

            invDeterminant = 1.0f / determinant;

            // Calculate distance from vert0 to ray origin
            Vector3 tvec = origin - vert0;

            // Calculate U parameter and test bounds
            float u = Vector3.Dot(tvec, pvec) * invDeterminant;
            if (u < 0.0f || u > 1.0f)
                return false;

            // Prepare to test V parameter
            Vector3 qvec = Vector3.Cross(tvec, edge1);

            // Calculate V parameter and test bounds
            float v = Vector3.Dot(direction, qvec) * invDeterminant;
            if (v < 0.0f || u + v > 1.0f)
                return false;

            dist = Vector3.Dot(edge2, qvec) * invDeterminant;

            return dist >= 0f;
        }
    }
}
