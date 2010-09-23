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
    public static class RayHeightmap
    {
        public static bool CollisionTest(Ray ray, float[] heightmap, int columns, int rows, float height, out float dist)
        {
            const float TOLERANCE = 1.0e-8f;

            // Find the entry point of the ray into the heightmap's AABB
            AABB heightmapAABB = new AABB(Vector3.Zero, new Vector3((float)columns, (float)rows, height));
            float exitDist;

            if (!RayAABB.CollisionTestSmits(heightmapAABB, ray, out dist, out exitDist))
                return false;

            Vector3 direction = new Vector3(ray.I, ray.J, ray.K);
            Vector3 entryPoint = ray.GetPoint(dist);
            Vector3 exitPoint = ray.GetPoint(exitDist);

            Vector3 delta = exitPoint - entryPoint;
            float incX = (Math.Abs(delta.X) < TOLERANCE) ? 1.0f / TOLERANCE : 1.0f / Math.Abs(delta.X);
            float incY = (Math.Abs(delta.Y) < TOLERANCE) ? 1.0f / TOLERANCE : 1.0f / Math.Abs(delta.Y);

            // Heightmap coordinates
            int x = (int)entryPoint.X;
            int y = (int)entryPoint.Y;
            int dx = (ray.I < 0.0f) ? -1 : (ray.I > 0.0f) ? 1 : 0;
            int dy = (ray.J < 0.0f) ? -1 : (ray.J > 0.0f) ? 1 : 0;

            float accumX = (delta.X < 0.0f) ? (entryPoint.X - (float)x) * incX : ((float)(x + 1) - entryPoint.X) * incX;
            float accumY = (delta.Y < 0.0f) ? (entryPoint.Y - (float)y) * incY : ((float)(y + 1) - entryPoint.Y) * incY;
            float t = 0.0f;

            // Digital differential analyzer (DDA) loop over the heightmap
            while (t <= 1.0f)
            {
                // TODO: We could further optimize this by testing if the current
                // z value passes below HighestAlt(heightmap, columns, rows, x, y)
                if (Intersects(entryPoint, direction, heightmap, columns, rows, x, y, out dist))
                    return true;

                if (accumX < accumY)
                {
                    t = accumX;
                    accumX += incX;
                    x += dx;
                }
                else
                {
                    t = accumY;
                    accumY += incY;
                    y += dy;
                }
            }

            return false;
        }

        public static bool CollisionTestSlow(Ray ray, float[] heightmap, int columns, int rows, out float dist)
        {
            // TODO: Optimize this function with a 
            Vector3 start = new Vector3(ray.X, ray.Y, ray.Z);
            Vector3 direction = new Vector3(ray.I, ray.J, ray.K);
            dist = Single.MaxValue;

            // Iterate through all of the triangles in the heightmap, doing a ray-triangle intersection
            for (int y = 0; y < rows - 1; y++)
            {
                for (int x = 0; x < columns - 1; x++)
                {
                    // 0--1-
                    // | /|
                    // |/ |
                    // 2--3-
                    // |  |
                    Vector3 v0 = new Vector3(x, y, heightmap[y * columns + x]);
                    Vector3 v1 = new Vector3(x + 1, x, heightmap[y * columns + (x + 1)]);
                    Vector3 v2 = new Vector3(x, y + 1, heightmap[(y + 1) * columns + x]);
                    Vector3 v3 = new Vector3(x + 1, y + 1, heightmap[(y + 1) * columns + (x + 1)]);

                    float thisDist;
                    if (RayTriangle.CollisionTestCull(start, direction, v0, v1, v2, out thisDist))
                    {
                        if (thisDist < dist)
                            dist = thisDist;
                    }
                    if (RayTriangle.CollisionTestCull(start, direction, v3, v2, v1, out thisDist))
                    {
                        if (thisDist < dist)
                            dist = thisDist;
                    }
                }
            }

            return dist < Single.MaxValue;
        }

        //private static float HighestAlt(float[] heightmap, int columns, int rows, int xCell, int yCell)
        //{
        //    float height0 = heightmap[yCell * columns + xCell];
        //    float height1 = heightmap[yCell * columns + xCell + 1];
        //    float height2 = heightmap[(yCell + 1) * columns + xCell];
        //    float height3 = heightmap[(yCell + 1) * columns + xCell + 1];

        //    return Math.Max(height0, Math.Max(height1, Math.Max(height2, height3)));
        //}

        private static bool Intersects(Vector3 start, Vector3 direction, float[] heightmap, int columns, int rows, int xCell, int yCell, out float dist)
        {
            xCell = Utils.Clamp(xCell, 0, columns - 2);
            yCell = Utils.Clamp(yCell, 0, rows - 2);

            // 0--1-
            // | /|
            // |/ |
            // 2--3-
            // |  |
            Vector3 v0 = new Vector3(xCell, yCell, heightmap[yCell * columns + xCell]);
            Vector3 v1 = new Vector3(xCell + 1, xCell, heightmap[yCell * columns + (xCell + 1)]);
            Vector3 v2 = new Vector3(xCell, yCell + 1, heightmap[(yCell + 1) * columns + xCell]);
            Vector3 v3 = new Vector3(xCell + 1, yCell + 1, heightmap[(yCell + 1) * columns + (xCell + 1)]);

            return
                RayTriangle.CollisionTestCull(start, direction, v0, v1, v2, out dist) ||
                RayTriangle.CollisionTestCull(start, direction, v3, v2, v1, out dist);
        }
    }
}
