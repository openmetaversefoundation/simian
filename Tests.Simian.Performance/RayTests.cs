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
using Simian;
using Tests.Simian;
using NTime.Framework;
using OpenMetaverse;

using Ray = Simian.Ray;

namespace Tests.Simian.Performance
{
    [TimerFixture]
    public class RayTests
    {
        const int TRIANGLE_COUNT = 500000;
        const int RAY_COUNT = 100;

        Vector3[] randomTriangles;
        Vector3 origin;
        Vector3 direction;
        Ray testRay;

        Ray[] randomRays;
        float[] heightmap;

        [TimerFixtureSetUp]
        public void GlobalSetUp()
        {
            randomTriangles = new Vector3[TRIANGLE_COUNT * 3];
            for (int i = 0; i < TRIANGLE_COUNT * 3; i++)
            {
                randomTriangles[i] = RNG.RandomVector3();
            }

            origin = RNG.RandomVector3();
            direction = RNG.RandomVector3();
            direction.Normalize();
            testRay = new Ray(origin, direction);

            randomRays = new Ray[RAY_COUNT];
            for (int i = 0; i < RAY_COUNT; i++)
            {
                Vector3 thisOrigin = RNG.RandomVector3();
                Vector3 thisDirection = RNG.RandomVector3();
                thisDirection.Normalize();
                randomRays[i] = new Ray(thisOrigin, thisDirection);
            }

            heightmap = new float[256 * 256];
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    heightmap[y * 256 + x] = (float)Math.Max(RNG.NextGaussian(25.0, 10.0), 0);
                }
            }
        }

        [TimerFixtureTearDown]
        public void GlobalTearDown() { }

        [TimerSetUp]
        public void LocalSetUp() { }

        [TimerTearDown]
        public void LocalTearDown() { }

        [TimerDurationTest(50, Unit = TimePeriod.Millisecond)]
        public void RayTriangleCullTest()
        {
            for (int i = 0; i < TRIANGLE_COUNT; i += 3)
            {
                float dist;
                RayTriangle.CollisionTestCull(origin, direction, randomTriangles[i + 0], randomTriangles[i + 1], randomTriangles[i + 2], out dist);
            }
        }

        [TimerDurationTest(50, Unit = TimePeriod.Millisecond)]
        public void RayTriangleNoCullTest()
        {
            for (int i = 0; i < TRIANGLE_COUNT; i += 3)
            {
                float dist;
                RayTriangle.CollisionTestNoCull(origin, direction, randomTriangles[i + 0], randomTriangles[i + 1], randomTriangles[i + 2], out dist);
            }
        }

        [TimerDurationTest(50, Unit = TimePeriod.Millisecond)]
        public void RayHeightmapDDATest()
        {
            for (int i = 0; i < RAY_COUNT; i++)
            {
                float dist;
                RayHeightmap.CollisionTest(randomRays[i], heightmap, 256, 256, 256.0f, out dist);
            }
        }
    }
}
