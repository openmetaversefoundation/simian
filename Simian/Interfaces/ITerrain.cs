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

namespace Simian
{
    public delegate void HeightmapChangedCallback(float[] heightmap);
    public delegate void HeightmapAreaChangedCallback(float[] heightmap, int xCell, int yCell);

    // FIXME: This interface is specific to Simian.Protocols.Linden and should be moved there
    public interface ITerrain : ISceneModule
    {
        event HeightmapChangedCallback OnHeightmapChanged;
        event HeightmapAreaChangedCallback OnHeightmapAreaChanged;

        float WaterHeight { get; set; }

        /// <summary>
        /// Get the terrain height at a specified position, in meters offset
        /// from the southwest corner
        /// </summary>
        float GetTerrainHeightAt(float x, float y);

        // FIXME: Are the following descriptions right?

        /// <summary>
        /// Get scene heightmap from MinPosition to MaxPosition, starting in 
        /// the southwest corner, in row-major ordering, at a one meter
        /// resolution
        /// </summary>
        float[] GetHeightmap();

        /// <summary>
        /// Like <see cref="GetHeightmap"/> but returns the heightmap without
        /// any agent modifications. Useful for reverting changes to the
        /// heightmap
        /// </summary>
        float[] GetOriginalHeightmap();

        /// <summary>
        /// Set scene heightmap from MinPosition to MaxPosition, starting in
        /// the southwest corner, in row-major ordering, at a one meter
        /// resolution
        /// </summary>
        void SetHeightmap(float[] heightmap);

        /// <summary>
        /// Set a 16x16 patch of the heightmap
        /// </summary>
        /// <param name="x">The x offset to start at, in 16 meter increments</param>
        /// <param name="y">The y offset to start at, in 16 meter increments</param>
        /// <param name="patch">256 float values making up a 16x16 patch of
        /// heightmap data</param>
        void Set16x16Patch(int x, int y, float[] patch);
    }
}
