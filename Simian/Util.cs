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
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
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
        private static HashSet<char> m_invalidPathChars;

        #region CRC32 Table

        private static readonly uint[] CRC_TABLE = new uint[]
        {
            0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419,
            0x706AF48F, 0xE963A535, 0x9E6495A3, 0x0EDB8832, 0x79DCB8A4,
            0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07,
            0x90BF1D91, 0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE,
            0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7, 0x136C9856,
            0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9,
            0xFA0F3D63, 0x8D080DF5, 0x3B6E20C8, 0x4C69105E, 0xD56041E4,
            0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
            0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3,
            0x45DF5C75, 0xDCD60DCF, 0xABD13D59, 0x26D930AC, 0x51DE003A,
            0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599,
            0xB8BDA50F, 0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924,
            0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D, 0x76DC4190,
            0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F,
            0x9FBFE4A5, 0xE8B8D433, 0x7807C9A2, 0x0F00F934, 0x9609A88E,
            0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
            0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED,
            0x1B01A57B, 0x8208F4C1, 0xF50FC457, 0x65B0D9C6, 0x12B7E950,
            0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3,
            0xFBD44C65, 0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2,
            0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB, 0x4369E96A,
            0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5,
            0xAA0A4C5F, 0xDD0D7CC9, 0x5005713C, 0x270241AA, 0xBE0B1010,
            0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
            0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17,
            0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD, 0xEDB88320, 0x9ABFB3B6,
            0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615,
            0x73DC1683, 0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8,
            0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1, 0xF00F9344,
            0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB,
            0x196C3671, 0x6E6B06E7, 0xFED41B76, 0x89D32BE0, 0x10DA7A5A,
            0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
            0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1,
            0xA6BC5767, 0x3FB506DD, 0x48B2364B, 0xD80D2BDA, 0xAF0A1B4C,
            0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF,
            0x4669BE79, 0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236,
            0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F, 0xC5BA3BBE,
            0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31,
            0x2CD99E8B, 0x5BDEAE1D, 0x9B64C2B0, 0xEC63F226, 0x756AA39C,
            0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
            0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B,
            0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21, 0x86D3D2D4, 0xF1D4E242,
            0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1,
            0x18B74777, 0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C,
            0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45, 0xA00AE278,
            0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7,
            0x4969474D, 0x3E6E77DB, 0xAED16A4A, 0xD9D65ADC, 0x40DF0B66,
            0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
            0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605,
            0xCDD70693, 0x54DE5729, 0x23D967BF, 0xB3667A2E, 0xC4614AB8,
            0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B,
            0x2D02EF8D
        };

        #endregion CRC32 Table

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

            #region Invalid Characters Init

            // A hashset of invalid path and filename characters
            m_invalidPathChars = new HashSet<char>();

            foreach (char c in Path.GetInvalidFileNameChars())
                m_invalidPathChars.Add(c);
            foreach (char c in Path.GetInvalidPathChars())
                m_invalidPathChars.Add(c);
            
            #endregion Invalid Characters Init
        }

        /// <summary>
        /// Returns the environment tick count as an always positive number
        /// </summary>
        /// <returns>Environment tick count as an always positive number</returns>
        public static int TickCount()
        {
            return Environment.TickCount & Int32.MaxValue;
        }

        /// <summary>
        /// Gets the name of the directory where the current running executable
        /// is located
        /// </summary>
        /// <returns>Filesystem path to the directory containing the current
        /// executable</returns>
        public static string ExecutingDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
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
        /// Replaces illegal filename and path characters with a given character
        /// </summary>
        /// <param name="filename">Filename to sanitize</param>
        /// <param name="replaceChar">Character to use in place of invalid characters</param>
        /// <returns>String that is safe to use as a path or filename</returns>
        public static string CleanFilename(string filename, char replaceChar)
        {
            StringBuilder output = new StringBuilder(filename.Length);

            for (int i = 0; i < filename.Length; i++)
            {
                char c = filename[i];
                if (!m_invalidPathChars.Contains(c))
                    output.Append(c);
                else
                    output.Append(replaceChar);
            }

            return output.ToString();
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
        /// Calculates a unique hash for a mesh based on its vertex and index
        /// data
        /// </summary>
        /// <param name="vertices">Mesh vertices</param>
        /// <param name="indices">Mesh indices</param>
        /// <returns></returns>
        private static ulong GetMeshKey(Vector3[] vertices, ushort[] indices)
        {
            ulong meshKey = 5381; // Nice prime to start with

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                meshKey = Util.djb2(meshKey, vertex.X);
                meshKey = Util.djb2(meshKey, vertex.Y);
                meshKey = Util.djb2(meshKey, vertex.Z);
            }

            for (int i = 0; i < indices.Length; i++)
            {
                ushort index = indices[i];
                meshKey = Util.djb2(meshKey, (byte)(index % 256));
                meshKey = Util.djb2(meshKey, (byte)((index >> 8) % 256));
            }

            return meshKey;
        }

        public static uint Crc32(byte[] buffer, int start, int length)
        {
            uint crcValue = 0xffffffff;

            unchecked
            {
                while (--length >= 0)
                    crcValue = CRC_TABLE[(crcValue ^ buffer[start++]) & 0xFF] ^ (crcValue >> 8);
            }

            return ~crcValue;
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
        /// Performs bilinear interpolation between four values
        /// </summary>
        /// <param name="v00">First, or top left value</param>
        /// <param name="v01">Second, or top right value</param>
        /// <param name="v10">Third, or bottom left value</param>
        /// <param name="v11">Fourth, or bottom right value</param>
        /// <param name="xPercent">Interpolation value on the X axis, between 0.0 and 1.0</param>
        /// <param name="yPercent">Interpolation value on fht Y axis, between 0.0 and 1.0</param>
        /// <returns>The bilinearly interpolated result</returns>
        public static float Bilinear(float v00, float v01, float v10, float v11, float xPercent, float yPercent)
        {
            return Utils.Lerp(Utils.Lerp(v00, v01, xPercent), Utils.Lerp(v10, v11, xPercent), yPercent);
        }

        /// <summary>
        /// Performs a high quality image resize
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        /// <returns>Resized image</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            return result;
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
