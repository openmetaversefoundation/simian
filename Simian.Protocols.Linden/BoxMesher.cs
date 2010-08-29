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
using System.ComponentModel.Composition;
using OpenMetaverse;

namespace Simian.Protocols.Linden
{
    /// <summary>
    /// Returns a simple cube mesh for all prims
    /// </summary>
    [SceneModule("BoxMesher")]
    public class BoxMesher : ISceneModule, IPrimMesher
    {
        const float front = -1f;
        const float back = 1f;
        const float left = -1f;
        const float right = 1f;
        const float top = 1f;
        const float bottom = -1f;

        const ushort leftbottomfront = 0;
        const ushort rightbottomfront = 1;
        const ushort lefttopfront = 2;
        const ushort righttopfront = 3;
        const ushort leftbottomback = 4;
        const ushort rightbottomback = 5;
        const ushort lefttopback = 6;
        const ushort righttopback = 7;

        PhysicsMesh m_cubeMesh;
        RenderingMesh m_cubeRenderingMesh;

        public BoxMesher()
        {
            m_cubeMesh = CreateCubeMesh();
            m_cubeRenderingMesh = CreateCubeRenderingMesh();
        }

        public void Start(IScene scene)
        {
        }

        public void Stop()
        {
        }

        public PhysicsMesh GetPhysicsMesh(LLPrimitive prim)
        {
            return m_cubeMesh;
        }

        public RenderingMesh GetRenderingMesh(LLPrimitive prim, DetailLevel lod)
        {
            return m_cubeRenderingMesh;
        }

        /// <summary>
        /// Generate a simple cube mesh
        /// </summary>
        /// <returns>A BasicMesh object containing vertices and indices for a
        /// cube</returns>
        private static PhysicsMesh CreateCubeMesh()
        {
            // Set up the 8 corners of the cube
            PhysicsMesh cube = new PhysicsMesh();
            cube.Vertices = new Vector3[]
            {
                new Vector3(left, bottom, front), // 0
                new Vector3(right, bottom, front), // 1
                new Vector3(left, top, front), // 2
                new Vector3(right, top, front), // 3
                new Vector3(left, bottom, back), // 4
                new Vector3(right, bottom, back), // 5
                new Vector3(left, top, back), // 6
                new Vector3(right, top, back), // 7
            };

            // Set up the index information for the 12 faces
            cube.Indices = new ushort[]
            {
                // Left faces
                lefttopfront,     lefttopback,      leftbottomback,   // 0
                leftbottomback,   leftbottomfront,  lefttopfront,     // 1

                // Front faces
                lefttopfront,     leftbottomfront,  rightbottomfront, // 2
                rightbottomfront, righttopfront,    lefttopfront,     // 3

                // Right faces
                righttopback,     righttopfront,    rightbottomfront, // 4 
                rightbottomfront, rightbottomback,  righttopback,     // 5

                // Back faces
                leftbottomback,   lefttopback,      righttopback,     // 6
                righttopback,     rightbottomback,  leftbottomback,   // 7

                // Top faces
                righttopfront,    righttopback,     lefttopback,      // 8
                lefttopback,      lefttopfront,     righttopfront,    // 9

                // Bottom faces
                leftbottomfront,  leftbottomback,   rightbottomback,  // 10
                rightbottomback,  rightbottomfront, leftbottomfront   // 11
            };

            return cube;
        }

        private static RenderingMesh CreateCubeRenderingMesh()
        {
            // Set up the 8 corners of the cube
            RenderingMesh cube = new RenderingMesh();
            cube.Faces = new RenderingMesh.Face[1];
            cube.Faces[0] = new RenderingMesh.Face();
            cube.Faces[0].Vertices = new Vertex[]
            {
                // FIXME: Set normals and UV coords too
                new Vertex() { Position = new Vector3(left, bottom, front) }, // 0
                new Vertex() { Position = new Vector3(right, bottom, front) }, // 1
                new Vertex() { Position = new Vector3(left, top, front) }, // 2
                new Vertex() { Position = new Vector3(right, top, front) }, // 3
                new Vertex() { Position = new Vector3(left, bottom, back) }, // 4
                new Vertex() { Position = new Vector3(right, bottom, back) }, // 5
                new Vertex() { Position = new Vector3(left, top, back) }, // 6
                new Vertex() { Position = new Vector3(right, top, back) }, // 7
            };

            // Set up the index information for the 12 faces
            cube.Faces[0].Indices = new ushort[]
            {
                // Left faces
                lefttopfront,     lefttopback,      leftbottomback,   // 0
                leftbottomback,   leftbottomfront,  lefttopfront,     // 1

                // Front faces
                lefttopfront,     leftbottomfront,  rightbottomfront, // 2
                rightbottomfront, righttopfront,    lefttopfront,     // 3

                // Right faces
                righttopback,     righttopfront,    rightbottomfront, // 4 
                rightbottomfront, rightbottomback,  righttopback,     // 5

                // Back faces
                leftbottomback,   lefttopback,      righttopback,     // 6
                righttopback,     rightbottomback,  leftbottomback,   // 7

                // Top faces
                righttopfront,    righttopback,     lefttopback,      // 8
                lefttopback,      lefttopfront,     righttopfront,    // 9

                // Bottom faces
                leftbottomfront,  leftbottomback,   rightbottomback,  // 10
                rightbottomback,  rightbottomfront, leftbottomfront   // 11
            };

            return cube;
        }
    }
}
