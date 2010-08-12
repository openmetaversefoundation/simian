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
using System.IO;

namespace Simian.Protocols.Linden
{
    public class LLRAW
    {
        private struct HeightmapLookupValue : IComparable<HeightmapLookupValue>
        {
            public ushort Index;
            public float Value;

            public HeightmapLookupValue(ushort index, float value)
            {
                Index = index;
                Value = value;
            }

            public int CompareTo(HeightmapLookupValue val)
            {
                return Value.CompareTo(val.Value);
            }
        }

        private const float OO_128 = 1.0f / 128.0f;
        private const byte ZERO_BYTE = 0;

        /// <summary>Lookup table to speed up serialization</summary>
        private static readonly HeightmapLookupValue[] LOOKUP_HEIGHT_TABLE;

        public float[] Heightmap;
        public float WaterHeight;

        /// <summary>
        /// Static constructor, initializes the lookup table for exports
        /// </summary>
        static LLRAW()
        {
            LOOKUP_HEIGHT_TABLE = new HeightmapLookupValue[256 * 256];

            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < 256; j++)
                {
                    LOOKUP_HEIGHT_TABLE[i + (j * 256)] = new HeightmapLookupValue(
                        (ushort)(i + (j * 256)), (float)((double)i * ((double)j * OO_128)));
                }
            }

            Array.Sort<HeightmapLookupValue>(LOOKUP_HEIGHT_TABLE);
        }

        public void ToFile(string filename)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.Write))
                ToStream(stream);
        }

        public void ToStream(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            byte waterHeight = (byte)(Math.Min(Math.Max(WaterHeight, 0f), 255f));

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float height = Math.Max(Heightmap[(255 - y) * 256 + x], 0f);

                    // The lookup table is pre-sorted, so we either find an exact match or
                    // the next closest (smaller) match with a binary search
                    int index = Array.BinarySearch<HeightmapLookupValue>(LOOKUP_HEIGHT_TABLE, new HeightmapLookupValue(0, height));
                    if (index < 0)
                        index = ~index - 1;
                    index = LOOKUP_HEIGHT_TABLE[index].Index;

                    byte value = (byte)(index & 0xFF);
                    byte multiplier = (byte)((index >> 8) & 0xFF);

                    writer.Write(value);       //  0
                    writer.Write(multiplier);  //  1
                    writer.Write(waterHeight); //  2
                    writer.Write(ZERO_BYTE);   //  3
                    writer.Write(ZERO_BYTE);   //  4
                    writer.Write(ZERO_BYTE);   //  5
                    writer.Write(ZERO_BYTE);   //  6
                    writer.Write(ZERO_BYTE);   //  7
                    writer.Write(ZERO_BYTE);   //  8
                    writer.Write(ZERO_BYTE);   //  9
                    writer.Write(ZERO_BYTE);   // 10
                    writer.Write(ZERO_BYTE);   // 11
                    writer.Write(ZERO_BYTE);   // 12
                }
            }

            writer.Close();
        }

        public static LLRAW FromFile(string filename)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                return FromStream(stream);
        }

        public static LLRAW FromStream(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);

            float[] heightmap = new float[256 * 256];
            float waterHeight = 0f;

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float value = (float)reader.ReadByte();
                    float multiplier = (float)reader.ReadByte();
                    waterHeight = (float)reader.ReadByte();

                    heightmap[(255 - y) * 256 + x] = value * (multiplier * OO_128);

                    reader.ReadBytes(10); // Skip the currently unused channels
                }
            }

            reader.Close();

            return new LLRAW { Heightmap = heightmap, WaterHeight = waterHeight };
        }
    }
}
