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

namespace Tests.Simian.Performance
{
    public static class RNG
    {
        private static Random rng = new Random();

        public static Vector3 RandomVector3()
        {
            return new Vector3(
                (float)(rng.NextDouble() * 255.0),
                (float)(rng.NextDouble() * 255.0),
                (float)(rng.NextDouble() * 255.0));
        }

        public static double NextGaussian(double mean, double stdDev)
        {
            double u1 = rng.NextDouble();
            double u2 = rng.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double randNormal = mean + stdDev * randStdNormal;

            return randNormal;
        }

        public static Vector3 GaussianVector3()
        {
            const double MEAN = 0.02;
            const double STD_DEV = 0.02;

            return new Vector3(
                (float)(Math.Max(NextGaussian(MEAN, STD_DEV) * 255.0, 0.01)),
                (float)(Math.Max(NextGaussian(MEAN, STD_DEV) * 255.0, 0.01)),
                (float)(Math.Max(NextGaussian(MEAN, STD_DEV) * 255.0, 0.01))
            );
        }
    }
}
