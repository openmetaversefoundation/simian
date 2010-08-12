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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection.Emit;
using System.Web;
using log4net;
using OpenMetaverse;

namespace Simian
{
    /// <summary>
    /// Miscellaneous utility functions for Simian and Simian modules
    /// </summary>
    public static class Util
    {
        unsafe public delegate void MemcpyCallback(void* des, void* src, uint bytes);

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);
        private static readonly MemcpyCallback m_memcpy;

        static Util()
        {
            #region memcpy Init

            unsafe
            {
                var dynamicMethod = new DynamicMethod
                (
                    "memcpy",
                    typeof(void),
                    new[] { typeof(void*), typeof(void*), typeof(uint) },
                    typeof(Util)
                );

                var ilGenerator = dynamicMethod.GetILGenerator();

                ilGenerator.Emit(OpCodes.Ldarg_0); // push des
                ilGenerator.Emit(OpCodes.Ldarg_1); // push src
                ilGenerator.Emit(OpCodes.Ldarg_2); // push bytes
                ilGenerator.Emit(OpCodes.Cpblk);   // memcpy
                ilGenerator.Emit(OpCodes.Ret);     // return

                m_memcpy = (MemcpyCallback)dynamicMethod.CreateDelegate(typeof(MemcpyCallback));
            }

            #endregion memcpy Init
        }

        /// <summary>
        /// Compare two floating point numbers for approximate equality
        /// </summary>
        /// <param name="lhs">First value to compare</param>
        /// <param name="rhs">Second value to compare</param>
        /// <param name="tolerance">Maximum difference between the values 
        /// before they are considered not equal</param>
        /// <returns>True if the values are approximately equal, otherwise false</returns>
        public static bool ApproxEquals(float lhs, float rhs, float tolerance)
        {
            return Math.Abs(lhs - rhs) <= tolerance;
        }

        /// <summary>
        /// Converts a global position to an LL region handle
        /// </summary>
        /// <param name="position">A global position inside the space of the
        /// target region handle</param>
        /// <returns>An LL region handle</returns>
        public static ulong PositionToRegionHandle(Vector3d position)
        {
            uint x = ((uint)position.X / 256u) * 256u;
            uint y = ((uint)position.Y / 256u) * 256u;

            return Utils.UIntsToLong(x, y);
        }

        /// <summary>
        /// Converts a global position to an LL scene-relative position
        /// </summary>
        /// <param name="position">A global position</param>
        /// <returns>A position relative from the 256x256 scene it is inside of</returns>
        public static Vector3 PositionToLocalPosition(Vector3d position)
        {
            uint x = ((uint)position.X / 256u) * 256u;
            uint y = ((uint)position.Y / 256u) * 256u;

            return new Vector3((float)position.X - x, (float)position.Y - y, (float)position.Z);
        }

        /// <summary>
        /// Convert a name with a single space in it to a first and last name
        /// </summary>
        /// <param name="name">A full name such as "John Doe"</param>
        /// <param name="firstName">First name</param>
        /// <param name="lastName">Last name (surname)</param>
        public static void GetFirstLastName(string name, out string firstName, out string lastName)
        {
            if (String.IsNullOrEmpty(name))
            {
                firstName = String.Empty;
                lastName = String.Empty;
            }
            else
            {
                string[] names = name.Split(' ');

                if (names.Length == 2)
                {
                    firstName = names[0];
                    lastName = names[1];
                }
                else
                {
                    firstName = String.Empty;
                    lastName = name;
                }
            }
        }

        /// <summary>
        /// Gets the IP address of the default local interface to bind to
        /// </summary>
        /// <returns>IP address of a local interface</returns>
        public static IPAddress GetLocalInterface()
        {
            IPAddress[] hosts = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            for (int i = 0; i < hosts.Length; i++)
            {
                IPAddress host = hosts[i];
                if (!IPAddress.IsLoopback(host) && host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return host;
            }

            for (int i = 0; i < hosts.Length; i++)
            {
                IPAddress host = hosts[i];
                if (host.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return host;
            }

            if (hosts.Length > 0)
                return hosts[0];

            return IPAddress.Loopback;
        }

        /// <summary>
        /// Strips illegal filename characters from an input string
        /// </summary>
        /// <param name="input">Input string to sanitize</param>
        /// <returns>String that is safe to use as a filename</returns>
        public static string GetSafeFilename(string input)
        {
            string safe = input;

            foreach (char disallowed in Path.GetInvalidFileNameChars())
                safe = safe.Replace(disallowed, '_');

            foreach (char disallowed in Path.GetInvalidPathChars())
                safe = safe.Replace(disallowed, '_');

            return safe.Trim();
        }

        /// <summary>
        /// Calculates the approximate volume of a scaled mesh
        /// </summary>
        /// <param name="mesh">Mesh data</param>
        /// <param name="scale">Object scale</param>
        /// <returns>Approximate volume of the mesh</returns>
        public static float GetMeshVolume(PhysicsMesh mesh, Vector3 scale)
        {
            const float OO_SIX = 1f / 6f;

            double volume = 0.0f;

            // Formula adapted from Stan Melax's algorithm: <http://www.melax.com/volint.html>
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                Vector3 v0 = mesh.Vertices[mesh.Indices[i + 0]];
                Vector3 v1 = mesh.Vertices[mesh.Indices[i + 1]];
                Vector3 v2 = mesh.Vertices[mesh.Indices[i + 2]];

                volume += Determinant3x3(v0, v1, v2);
            }

            return (float)(volume * OO_SIX) * scale.X * scale.Y * scale.Z;
        }

        /// <summary>
        /// Performs the djb2 hash algorithm on a single byte and combines it 
        /// with an existing hash result
        /// </summary>
        /// <param name="hash">Input hash that will be combined with the result</param>
        /// <param name="c">Byte to hash</param>
        /// <returns>The combined input hash and new hash</returns>
        public static ulong djb2(ulong hash, byte c)
        {
            return ((hash << 5) + hash) + (ulong)c;
        }

        /// <summary>
        /// Performs the djb2 hash algorithm on a four byte floating point 
        /// value and combines it with an existing hash result
        /// </summary>
        /// <param name="hash">Input hash that will be combined with the result</param>
        /// <param name="f">Single-precision float to hash</param>
        /// <returns>The combined input hash and new hash</returns>
        public static ulong djb2(ulong hash, float f)
        {
            byte[] bytes = BitConverter.GetBytes(f);
            for (int i = 0; i < bytes.Length; i++)
                hash = djb2(hash, bytes[i]);

            return hash;
        }

        /// <summary>
        /// Performs an efficient memory copy using unsafe code
        /// </summary>
        /// <param name="des">Destination pointer</param>
        /// <param name="src">Source pointer</param>
        /// <param name="bytes">Number of bytes to copy</param>
        unsafe public static void memcpy(IntPtr des, IntPtr src, uint bytes)
        {
            m_memcpy(des.ToPointer(), src.ToPointer(), bytes);
        }

        /// <summary>
        /// Performs an efficient memory copy using unsafe code
        /// </summary>
        /// <param name="des">Destination pointer</param>
        /// <param name="src">Source pointer</param>
        /// <param name="bytes">Number of bytes to copy</param>
        unsafe public static void memcpy(void* des, void* src, uint bytes)
        {
            m_memcpy(des, src, bytes);
        }

        private static float Determinant3x3(Vector3 r0, Vector3 r1, Vector3 r2)
        {
            // Calculate the determinant of a 3x3 matrix using Sarrus' method
            return
                 r0.X * r1.Y * r2.Z
               + r0.Y * r1.Z * r2.X
               + r0.Z * r1.X * r2.Y
               - r0.Z * r1.Y * r2.X
               - r1.Z * r2.Y * r0.X
               - r2.Z * r0.Y * r1.X;
        }
    }
}
