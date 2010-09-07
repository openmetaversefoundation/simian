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
using System.ComponentModel.Composition;
using OpenMetaverse;

namespace Simian.Scripting.Linden
{
    public partial class LindenApi : ISceneModule, IScriptApi
    {
        [ScriptMethod]
        public double llSin(IScriptInstance script, double theta)
        {
            return Math.Sin(theta);
        }

        [ScriptMethod]
        public double llCos(IScriptInstance script, double theta)
        {
            return Math.Cos(theta);
        }

        [ScriptMethod]
        public double llTan(IScriptInstance script, double theta)
        {
            return Math.Tan(theta);
        }

        [ScriptMethod]
        public double llAtan2(IScriptInstance script, double y, double x)
        {
            return Math.Atan2(y, x);
        }

        [ScriptMethod]
        public double llLog(double val)
        {
            return (double)Math.Log(val);
        }

        [ScriptMethod]
        public double llLog10(double val)
        {
            return (double)Math.Log10(val);
        }

        [ScriptMethod]
        public double llSqrt(IScriptInstance script, double val)
        {
            return Math.Sqrt(val);
        }

        [ScriptMethod]
        public double llPow(IScriptInstance script, double base_, double exponent)
        {
            return Math.Pow(base_, exponent);
        }

        [ScriptMethod]
        public double llModPow(int a, int b, int c)
        {
            return (float)Math.Pow(a, b) % c;
        }

        [ScriptMethod]
        public int llAbs(IScriptInstance script, int val)
        {
            return Math.Abs(val);
        }

        [ScriptMethod]
        public double llFabs(IScriptInstance script, double val)
        {
            return Math.Abs(val);
        }

        [ScriptMethod]
        public double llFrand(IScriptInstance script, double mag)
        {
            return Utils.RandomDouble() * (mag - Double.Epsilon);
        }

        [ScriptMethod]
        public double llFloor(IScriptInstance script, double val)
        {
            return Math.Floor(val);
        }

        [ScriptMethod]
        public double llCeil(IScriptInstance script, double val)
        {
            return Math.Ceiling(val);
        }

        [ScriptMethod]
        public double llRound(IScriptInstance script, double val)
        {
            return Math.Round(val);
        }

        [ScriptMethod]
        public double llVecMag(IScriptInstance script, Vector3 vec)
        {
            return vec.Length();
        }

        [ScriptMethod]
        public Vector3 llVecNorm(IScriptInstance script, Vector3 vec)
        {
            // NOTE: Emulates behavior reported in https://jira.secondlife.com/browse/SVC-4711
            double mag = vec.Length();
            return (mag != 0.0d) ? vec / (float)mag : Vector3.Zero;
        }

        [ScriptMethod]
        public double llVecDist(IScriptInstance script, Vector3 vec_a, Vector3 vec_b)
        {
            return Vector3.Distance(vec_a, vec_b);
        }

        [ScriptMethod]
        public Vector3 llRot2Euler(IScriptInstance script, Quaternion rot)
        {
            Quaternion quat = rot;

            // This implementation is from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions
            Quaternion t = new Quaternion(quat.X * quat.X, quat.Y * quat.Y, quat.Z * quat.Z, quat.W * quat.W);
            
            double m = (t.X + t.Y + t.Z + t.W);
            if (m == 0.0) return Vector3.Zero;

            double n = 2.0 * (quat.Y * quat.W + quat.X * quat.Z);
            double p = m * m - n * n;

            if (p > 0.0)
            {
                return new Vector3(
                    (float)NormalizeAngle(Math.Atan2(2.0 * (quat.X * quat.W - quat.Y * quat.Z), (-t.X - t.Y + t.Z + t.W))),
                    (float)NormalizeAngle(Math.Atan2(n, Math.Sqrt(p))),
                    (float)NormalizeAngle(Math.Atan2(2.0 * (quat.Z * quat.W - quat.X * quat.Y), (t.X - t.Y - t.Z + t.W))));
            }
            else if (n > 0.0)
            {
                return new Vector3(
                    0.0f,
                    (float)(Math.PI * 0.5),
                    (float)NormalizeAngle(Math.Atan2((quat.Z * quat.W + quat.X * quat.Y), 0.5 - t.X - t.Z)));
            }
            else
            {
                return new Vector3(
                    0.0f,
                    (float)(-Math.PI * 0.5),
                    (float)NormalizeAngle(Math.Atan2((quat.Z * quat.W + quat.X * quat.Y), 0.5 - t.X - t.Z)));
            }
        }

        [ScriptMethod]
        public Quaternion llEuler2Rot(IScriptInstance script, Vector3 v)
        {
            return Quaternion.CreateFromEulers(v);
        }

        [ScriptMethod]
        public Quaternion llAxes2Rot(IScriptInstance script, Vector3 fwd, Vector3 left, Vector3 up)
        {
            double s;
            double tr = fwd.X + left.Y + up.Z + 1.0;

            if (tr >= 1.0)
            {
                s = 0.5 / Math.Sqrt(tr);
                return new Quaternion(
                    (float)((left.Z - up.Y) * s),
                    (float)((up.X - fwd.Z) * s),
                    (float)((fwd.Y - left.X) * s),
                    (float)(0.25 / s));
            }
            else
            {
                double max = (left.Y > up.Z) ? left.Y : up.Z;

                if (max < fwd.X)
                {
                    s = Math.Sqrt(fwd.X - (left.Y + up.Z) + 1.0);
                    double x = s * 0.5;
                    s = 0.5 / s;
                    return new Quaternion(
                        (float)x,
                        (float)((fwd.Y + left.X) * s),
                        (float)((up.X + fwd.Z) * s),
                        (float)((left.Z - up.Y) * s));
                }
                else if (max == left.Y)
                {
                    s = Math.Sqrt(left.Y - (up.Z + fwd.X) + 1.0);
                    double y = s * 0.5;
                    s = 0.5 / s;
                    return new Quaternion(
                        (float)((fwd.Y + left.X) * s),
                        (float)y,
                        (float)((left.Z + up.Y) * s),
                        (float)((up.X - fwd.Z) * s));
                }
                else
                {
                    s = Math.Sqrt(up.Z - (fwd.X + left.Y) + 1.0);
                    double z = s * 0.5;
                    s = 0.5 / s;
                    return new Quaternion(
                        (float)((up.X + fwd.Z) * s),
                        (float)((left.Z + up.Y) * s),
                        (float)z,
                        (float)((fwd.Y - left.X) * s));
                }
            }
        }

        [ScriptMethod]
        public Vector3 llRot2Fwd(IScriptInstance script, Quaternion q)
        {
            return llVecNorm(script, Vector3.UnitX * q);
        }

        [ScriptMethod]
        public Vector3 llRot2Left(IScriptInstance script, Quaternion q)
        {
            return llVecNorm(script, Vector3.UnitY * q);
        }

        [ScriptMethod]
        public Vector3 llRot2Up(IScriptInstance script, Quaternion q)
        {
            return llVecNorm(script, Vector3.UnitZ * q);
        }

        [ScriptMethod]
        public Quaternion llRotBetween(IScriptInstance script, Vector3 start, Vector3 end)
        {
            start = Vector3.Normalize(start);
            end = Vector3.Normalize(end);

            double dotProduct = Vector3.Dot(start, end);
            Vector3 crossProduct = Vector3.Cross(start, end);
            double magProduct = Vector3.Mag(start) * Vector3.Mag(end);
            double angle = Math.Acos(dotProduct / magProduct);
            Vector3 axis = Vector3.Normalize(crossProduct);
            float s = (float)Math.Sin(angle / 2.0);

            float x = axis.X * s;
            float y = axis.Y * s;
            float z = axis.Z * s;
            float w = (float)Math.Cos(angle / 2.0);

            if (Single.IsNaN(x) || Single.IsNaN(y) || Single.IsNaN(z) || Single.IsNaN(w))
                return Quaternion.Identity;

            return new Quaternion(x, y, z, w);
        }

        #region Helpers

        /// <summary>
        /// Normalize an angle between -PI and PI (-180 degrees to 180 degrees)
        /// </summary>
        /// <param name="angle">Angle to normalize, in radians</param>
        /// <returns>Normalized angle, in radians</returns>
        private double NormalizeAngle(double angle)
        {
            if (angle > -Math.PI && angle < Math.PI)
                return angle;

            int numPis = (int)(Math.PI / angle);
            double remainder = angle - Math.PI * numPis;

            if (numPis % 2 == 1)
                return Math.PI - angle;
            else
               return remainder;
        }

        #endregion Helpers
    }
}
