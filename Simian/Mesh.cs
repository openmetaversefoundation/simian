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
using System.Runtime.InteropServices;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian
{
    #region Enums

    public enum DetailLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Highest = 4
    }

    public enum PhysicsType : int
    {
        Avatar = 0,
        Box,
        Cone,
        Cylinder,
        Sphere,
        ConvexHull,
    }

    #endregion Enums

    #region Mesh / Convex Hull Classes

    [StructLayout(LayoutKind.Explicit)]
    public struct Vertex
    {
        public const int SIZE_OF = 12 + 12 + 8;

        [FieldOffset(0)]
        public Vector3 Position;
        [FieldOffset(12)]
        public Vector3 Normal;
        [FieldOffset(24)]
        public Vector2 TexCoord;

        public override string ToString()
        {
            return String.Format("P: {0} N: {1} T: {2}", Position, Normal, TexCoord);
        }
    }

    public class RenderingMesh
    {
        public class Face
        {
            public Vertex[] Vertices;
            public ushort[] Indices;
        }

        public Face[] Faces;

        public byte[] Serialize()
        {
            int length = 2; // Face count

            for (int i = 0; i < Faces.Length; i++)
            {
                length += 2; // Vertex count
                length += Faces[i].Vertices.Length * Vertex.SIZE_OF;
                length += 2; // Index count
                length += Faces[i].Indices.Length * 2;
            }

            byte[] data = new byte[length];
            int pos = 0;

            Utils.UInt16ToBytes((ushort)Faces.Length, data, pos);
            pos += 2;

            for (int i = 0; i < Faces.Length; i++)
            {
                Face face = Faces[i];

                Utils.UInt16ToBytes((ushort)face.Vertices.Length, data, pos);
                pos += 2;

                for (int j = 0; j < face.Vertices.Length; j++)
                {
                    Vertex v = face.Vertices[j];

                    v.Position.ToBytes(data, pos);
                    pos += 12;
                    v.Normal.ToBytes(data, pos);
                    pos += 12;
                    v.TexCoord.ToBytes(data, pos);
                    pos += 8;
                }

                Utils.UInt16ToBytes((ushort)face.Indices.Length, data, pos);
                pos += 2;

                for (int j = 0; j < face.Indices.Length; j++)
                {
                    Utils.UInt16ToBytes(face.Indices[j], data, pos);
                    pos += 2;
                }
            }

            return data;
        }

        public static RenderingMesh Deserialize(byte[] data)
        {
            int pos = 0;
            
            ushort faceCount = Utils.BytesToUInt16(data, pos);
            pos += 2;

            RenderingMesh mesh = new RenderingMesh();
            mesh.Faces = new Face[faceCount];

            for (int i = 0; i < faceCount; i++)
            {
                Face face = new Face();

                ushort vertexCount = Utils.BytesToUInt16(data, pos);
                pos += 2;

                face.Vertices = new Vertex[vertexCount];
                for (int j = 0; j < vertexCount; j++)
                {
                    Vertex v = new Vertex();

                    v.Position = new Vector3(data, pos);
                    pos += 12;

                    v.Normal = new Vector3(data, pos);
                    pos += 12;

                    v.TexCoord = new Vector2(Utils.BytesToFloat(data, pos), Utils.BytesToFloat(data, pos + 4));
                    pos += 8;

                    face.Vertices[j] = v;
                }

                ushort indexCount = Utils.BytesToUInt16(data, pos);
                pos += 2;

                face.Indices = new ushort[indexCount];
                Buffer.BlockCopy(data, pos, face.Indices, 0, indexCount * 2);
                pos += indexCount * 2;

                mesh.Faces[i] = face;
            }

            return mesh;
        }
    }

    public class BasicMesh
    {
        public Vector3[] Vertices;
        public ushort[] Indices;
        public float Volume;

        public byte[] Serialize()
        {
            int length = 4; // Volume

            length += 2; // Vertex count
            length += Vertices.Length * 12;
            length += 2; // Index count
            length += Indices.Length * 2;

            byte[] data = new byte[length];
            int pos = 0;

            Utils.FloatToBytes(Volume, data, pos);
            pos += 4;

            Utils.UInt16ToBytes((ushort)Vertices.Length, data, pos);
            pos += 2;

            for (int j = 0; j < Vertices.Length; j++)
            {
                Vector3 v = Vertices[j];

                v.ToBytes(data, pos);
                pos += 12;
            }

            Utils.UInt16ToBytes((ushort)Indices.Length, data, pos);
            pos += 2;

            for (int j = 0; j < Indices.Length; j++)
            {
                Utils.UInt16ToBytes(Indices[j], data, pos);
                pos += 2;
            }

            return data;
        }

        public static BasicMesh Deserialize(byte[] data)
        {
            int pos = 0;

            BasicMesh mesh = new BasicMesh();
            mesh.Volume = Utils.BytesToFloat(data, pos);
            pos += 4;

            ushort vertexCount = Utils.BytesToUInt16(data, pos);
            pos += 2;

            mesh.Vertices = new Vector3[vertexCount];
            for (int j = 0; j < vertexCount; j++)
            {
                Vector3 v = new Vector3(data, pos);
                pos += 12;

                mesh.Vertices[j] = v;
            }

            ushort indexCount = Utils.BytesToUInt16(data, pos);
            pos += 2;

            mesh.Indices = new ushort[indexCount];
            Buffer.BlockCopy(data, pos, mesh.Indices, 0, indexCount * 2);
            pos += indexCount * 2;

            return mesh;
        }
    }

    public class ConvexHullSet
    {
        public class HullPart
        {
            public Vector3 Offset;
            public Vector3[] Vertices;
        }

        public HullPart[] Parts;
        public float Volume;

        public byte[] Serialize()
        {
            int length = 4; // Volume
            length += 2; // Hull count

            for (int i = 0; i < Parts.Length; i++)
            {
                length += 12; // Offset
                length += 2; // Vertex count
                length += Parts[i].Vertices.Length * 12;
            }

            byte[] data = new byte[length];
            int pos = 0;

            Utils.FloatToBytes(Volume, data, pos);
            pos += 4;

            Utils.UInt16ToBytes((ushort)Parts.Length, data, pos);
            pos += 2;

            for (int i = 0; i < Parts.Length; i++)
            {
                HullPart part = Parts[i];

                part.Offset.ToBytes(data, pos);
                pos += 12;

                Utils.UInt16ToBytes((ushort)part.Vertices.Length, data, pos);
                pos += 2;

                for (int j = 0; j < part.Vertices.Length; j++)
                {
                    Vector3 v = part.Vertices[j];

                    v.ToBytes(data, pos);
                    pos += 12;
                }
            }

            return data;
        }

        public static ConvexHullSet Deserialize(byte[] data)
        {
            int pos = 0;

            ConvexHullSet hullSet = new ConvexHullSet();
            hullSet.Volume = Utils.BytesToFloat(data, pos);
            pos += 4;

            ushort partCount = Utils.BytesToUInt16(data, pos);
            pos += 2;

            hullSet.Parts = new HullPart[partCount];

            for (int i = 0; i < partCount; i++)
            {
                HullPart part = new HullPart();

                part.Offset = new Vector3(data, pos);
                pos += 12;

                ushort vertexCount = Utils.BytesToUInt16(data, pos);
                pos += 2;

                part.Vertices = new Vector3[vertexCount];
                for (int j = 0; j < vertexCount; j++)
                {
                    Vector3 v = new Vector3(data, pos);
                    pos += 12;

                    part.Vertices[j] = v;
                }

                hullSet.Parts[i] = part;
            }

            return hullSet;
        }
    }

    #endregion Mesh / Convex Hull Classes

    [ApplicationModule("MeshCache")]
    public class MeshCache : IApplicationModule
    {
        public const string RENDER_MESH_BASE_CONTENT_TYPE = "application/x-simian-rendermesh";
        public const string BASIC_MESH_BASE_CONTENT_TYPE = "application/x-simian-basicmesh";
        public const string CONVEX_HULL_BASE_CONTENT_TYPE = "application/x-simian-convexhull";

        private static readonly string[] LOD_NAMES =
        {
            String.Empty,
            "low",
            "medium",
            "high",
            "highest"
        };

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private IDataStore m_dataStore;

        public bool Start(Simian simian)
        {
            m_dataStore = simian.GetAppModule<IDataStore>();
            if (m_dataStore == null)
            {
                m_log.Error("MeshCache requires an IDataStore");
                return false;
            }

            return true;
        }

        public void Stop()
        {
        }

        public bool TryGetBasicMesh(ulong meshKey, DetailLevel lod, out BasicMesh mesh)
        {
            UUID dataID = new UUID(meshKey);
            string contentType = BASIC_MESH_BASE_CONTENT_TYPE + "-" + LOD_NAMES[(int)lod];

            mesh = null;

            byte[] meshData;
            if (m_dataStore.TryGetAsset(dataID, contentType, out meshData))
            {
                try
                {
                    mesh = BasicMesh.Deserialize(meshData);
                }
                catch (Exception ex)
                {
                    m_log.WarnFormat("Failed to deserialize basic mesh {0} ({1}): {2}", dataID, contentType, ex.Message);
                }
            }

            return (mesh != null);
        }

        public bool TryGetConvexHullSet(ulong meshKey, DetailLevel lod, out ConvexHullSet hullSet)
        {
            UUID dataID = new UUID(meshKey);
            string contentType = CONVEX_HULL_BASE_CONTENT_TYPE + "-" + LOD_NAMES[(int)lod];

            hullSet = null;

            byte[] hullData;
            if (m_dataStore.TryGetAsset(dataID, contentType, out hullData))
            {
                try
                {
                    hullSet = ConvexHullSet.Deserialize(hullData);
                }
                catch (Exception ex)
                {
                    m_log.WarnFormat("Failed to deserialize convex hull set {0} ({1}): {2}", dataID, contentType, ex.Message);
                }
            }

            return (hullSet != null);
        }

        public bool TryGetRenderingMesh(ulong meshKey, DetailLevel lod, out RenderingMesh mesh)
        {
            UUID dataID = new UUID(meshKey);
            string contentType = RENDER_MESH_BASE_CONTENT_TYPE + "-" + LOD_NAMES[(int)lod];

            mesh = null;

            byte[] meshData;
            if (m_dataStore.TryGetAsset(dataID, contentType, out meshData))
            {
                try
                {
                    mesh = RenderingMesh.Deserialize(meshData);
                }
                catch (Exception ex)
                {
                    m_log.WarnFormat("Failed to deserialize rendering mesh {0} ({1}): {2}", dataID, contentType, ex.Message);
                }
            }

            return (mesh != null);
        }

        public void StoreBasicMesh(ulong meshKey, DetailLevel lod, BasicMesh mesh)
        {
            UUID dataID = new UUID(meshKey);
            string contentType = BASIC_MESH_BASE_CONTENT_TYPE + "-" + lod.ToString().ToLower();
            byte[] data = mesh.Serialize();

            m_dataStore.AddOrUpdateAsset(dataID, contentType, data, true);
        }

        public void StoreConvexHullSet(ulong meshKey, DetailLevel lod, ConvexHullSet hullSet)
        {
            UUID dataID = new UUID(meshKey);
            string contentType = CONVEX_HULL_BASE_CONTENT_TYPE + "-" + lod.ToString().ToLower();
            byte[] data = hullSet.Serialize();

            m_dataStore.AddOrUpdateAsset(dataID, contentType, data, true);
        }

        public void StoreRenderingMesh(ulong meshKey, DetailLevel lod, RenderingMesh mesh)
        {
            UUID dataID = new UUID(meshKey);
            string contentType = RENDER_MESH_BASE_CONTENT_TYPE + "-" + lod.ToString().ToLower();
            byte[] data = mesh.Serialize();

            m_dataStore.AddOrUpdateAsset(dataID, contentType, data, true);
        }
    }
}
