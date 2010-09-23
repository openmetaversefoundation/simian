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
    public sealed class Ray
    {
        public enum RayType
        {
            MMM,
            MMP,
            MPM,
            MPP,
            PMM,
            PMP,
            PPM,
            PPP
        }

        /// <summary>Ray classification</summary>
        public readonly RayType Type;
        /// <summary>Ray origin X</summary>
        public readonly float X;
        /// <summary>Ray origin Y</summary>
        public readonly float Y;
        /// <summary>Ray origin Z</summary>
        public readonly float Z;
        /// <summary>Pluecker coefficient R0</summary>
        public readonly float R0;
        /// <summary>Pluecker coefficient R1</summary>
        public readonly float R1;
        /// <summary>Pluecker coefficient R3</summary>
        public readonly float R3;
        /// <summary>-R2 or i direction component</summary>
        public readonly float I;
        /// <summary>R5 or j direction component</summary>
        public readonly float J;
        /// <summary>-R4 or k direction component</summary>
        public readonly float K;
        /// <summary>Inverse of i direction component</summary>
        public readonly float II;
        /// <summary>Inverse of j direction component</summary>
        public readonly float IJ;
        /// <summary>Inverse of j direction component</summary>
        public readonly float IK;

        public Ray(Vector3 start, Vector3 direction)
        {
            X = start.X;
            Y = start.Y;
            Z = start.Z;
            I = direction.X;
            J = direction.Y;
            K = direction.Z;
            II = 1.0f / I;
            IJ = 1.0f / J;
            IK = 1.0f / K;
            R0 = start.X * J - I * start.Y;
            R1 = start.X * K - I * start.Z;
            R3 = start.Y * K - J * start.Z;

            // If direction.X/Y/Z are 0.0, for some reason they are getting treated as -0.0.
            // Fix that here
            if (II == Single.NegativeInfinity) II = Single.PositiveInfinity;
            if (IJ == Single.NegativeInfinity) IJ = Single.PositiveInfinity;
            if (IK == Single.NegativeInfinity) IK = Single.PositiveInfinity;

            if (I < 0f)
            {
                if (J < 0f)
                {
                    if (K < 0f)
                        Type = RayType.MMM;
                    else
                        Type = RayType.MMP;
                }
                else
                {
                    if (K < 0f)
                        Type = RayType.MPM;
                    else
                        Type = RayType.MPP;
                }
            }
            else
            {
                if (J < 0f)
                {
                    if (K < 0f)
                        Type = RayType.PMM;
                    else
                        Type = RayType.PMP;
                }
                else
                {
                    if (K < 0f)
                        Type = RayType.PPM;
                    else
                        Type = RayType.PPP;
                }
            }
        }

        public Vector3 GetPoint(float dist)
        {
            return new Vector3(X, Y, Z) + new Vector3(I, J, K) * dist;
        }

        public override string ToString()
        {
            return String.Format("Start={0}, Dir={1}", new Vector3(X, Y, Z), new Vector3(I, J, K));
        }
    }
}
