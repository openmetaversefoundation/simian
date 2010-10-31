using System;
using Simian;
using NUnit.Framework;
using OpenMetaverse;

namespace Tests.Simian
{
    [TestFixture]
    public class MeshTests
    {
        private static Random m_rng = new Random();

        [Test]
        public void BasicMeshSerializationTest()
        {
            const int VERTICES = 499;
            const int INDICES = 1009;

            BasicMesh mesh = new BasicMesh();
            mesh.Volume = 42f;

            mesh.Vertices = new Vector3[VERTICES];
            for (int i = 0; i < VERTICES; i++)
                mesh.Vertices[i] = RandomVector();

            mesh.Indices = new ushort[INDICES];
            for (int i = 0; i < INDICES; i++)
                mesh.Indices[i] = (ushort)m_rng.Next(VERTICES);

            byte[] data = mesh.Serialize();

            BasicMesh mesh2 = BasicMesh.Deserialize(data);

            Assert.AreEqual(mesh.Volume, mesh2.Volume);
            Assert.AreEqual(mesh.Vertices.Length, mesh2.Vertices.Length);
            Assert.AreEqual(mesh.Indices.Length, mesh2.Indices.Length);

            for (int i = 0; i < mesh.Vertices.Length; i++)
                Assert.AreEqual(mesh.Vertices[i], mesh2.Vertices[i]);

            for (int i = 0; i < mesh.Indices.Length; i++)
                Assert.AreEqual(mesh.Indices[i], mesh2.Indices[i]);
        }

        [Test]
        public void RenderingMeshSerializationTest()
        {
            const int FACES = 13;
            const int MAX_VERTICES = 499;
            const int MAX_INDICES = 1009;

            RenderingMesh mesh = new RenderingMesh();
            mesh.Faces = new RenderingMesh.Face[FACES];

            for (int i = 0; i < FACES; i++)
            {
                RenderingMesh.Face face = new RenderingMesh.Face();
                face.Vertices = new Vertex[m_rng.Next(MAX_VERTICES)];
                for (int j = 0; j < face.Vertices.Length; j++)
                    face.Vertices[j] = new Vertex { Normal = RandomVector(), Position = RandomVector(), TexCoord = new Vector2(0.5f, 0.5f) };

                face.Indices = new ushort[m_rng.Next(MAX_INDICES)];
                for (int j = 0; j < face.Indices.Length; j++)
                    face.Indices[j] = (ushort)m_rng.Next(face.Vertices.Length);

                mesh.Faces[i] = face;
            }

            byte[] data = mesh.Serialize();

            RenderingMesh mesh2 = RenderingMesh.Deserialize(data);

            Assert.AreEqual(mesh.Faces.Length, mesh2.Faces.Length);

            for (int i = 0; i < mesh.Faces.Length; i++)
            {
                RenderingMesh.Face face = mesh.Faces[i];
                RenderingMesh.Face face2 = mesh2.Faces[i];

                Assert.AreEqual(face.Vertices.Length, face2.Vertices.Length);
                Assert.AreEqual(face.Indices.Length, face2.Indices.Length);

                for (int j = 0; j < face.Vertices.Length; j++)
                {
                    Vertex v = face.Vertices[j];
                    Vertex v2 = face2.Vertices[j];

                    Assert.AreEqual(v.Position, v2.Position);
                    Assert.AreEqual(v.Normal, v2.Normal);
                    Assert.AreEqual(v.TexCoord, v2.TexCoord);
                }

                for (int j = 0; j < face.Indices.Length; j++)
                    Assert.AreEqual(face.Indices[j], face2.Indices[j]);
            }
        }

        [Test]
        public void ConvexHullSetSerializationTest()
        {
            const int HULLS = 13;
            const int MAX_VERTICES = 499;
            const int MAX_INDICES = 1009;

            ConvexHullSet hullSet = new ConvexHullSet();
            hullSet.Volume = 42f;
            hullSet.Parts = new ConvexHullSet.HullPart[HULLS];

            for (int i = 0; i < HULLS; i++)
            {
                ConvexHullSet.HullPart part = new ConvexHullSet.HullPart();
                part.Offset = RandomVector();

                part.Vertices = new Vector3[m_rng.Next(MAX_VERTICES)];
                for (int j = 0; j < part.Vertices.Length; j++)
                    part.Vertices[j] = RandomVector();

                part.Indices = new int[m_rng.Next(MAX_INDICES)];
                for (int j = 0; j < part.Indices.Length; j++)
                    part.Indices[j] = m_rng.Next(part.Vertices.Length);

                hullSet.Parts[i] = part;
            }

            byte[] data = hullSet.Serialize();

            ConvexHullSet hullSet2 = ConvexHullSet.Deserialize(data);

            Assert.AreEqual(hullSet.Volume, hullSet2.Volume);
            Assert.AreEqual(hullSet.Parts.Length, hullSet2.Parts.Length);

            for (int i = 0; i < hullSet.Parts.Length; i++)
            {
                ConvexHullSet.HullPart part = hullSet.Parts[i];
                ConvexHullSet.HullPart part2 = hullSet2.Parts[i];

                Assert.AreEqual(part.Offset, part2.Offset);
                Assert.AreEqual(part.Vertices.Length, part2.Vertices.Length);
                Assert.AreEqual(part.Indices.Length, part2.Indices.Length);

                for (int j = 0; j < part.Vertices.Length; j++)
                {
                    Vector3 v = part.Vertices[j];
                    Vector3 v2 = part2.Vertices[j];

                    Assert.AreEqual(v, v2);
                }

                for (int j = 0; j < part.Indices.Length; j++)
                {
                    int idx = part.Indices[j];
                    int idx2 = part2.Indices[j];

                    Assert.AreEqual(idx, idx2);
                }
            }
        }

        private static Vector3 RandomVector()
        {
            return new Vector3(
                (float)(m_rng.NextDouble() - 0.5) * 100f,
                (float)(m_rng.NextDouble() - 0.5) * 100f,
                (float)(m_rng.NextDouble() - 0.5) * 100f);
        }
    }
}
