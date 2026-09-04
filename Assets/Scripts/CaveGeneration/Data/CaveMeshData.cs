using System.Collections.Generic;
using UnityEngine;

namespace CaveGeneration.Data
{
    /// <summary>
    /// Encapsulates vertex, triangle, and UV buffers along with mesh construction helpers.
    /// </summary>
    public class CaveMeshData
    {
        public List<Vector3> Vertices { get; }
        public List<int> Triangles { get; }
        public List<Vector2> UVs { get; }

        public int VertexCount => Vertices.Count;
        public int TriangleCount => Triangles.Count;

        public CaveMeshData()
        {
            Vertices = new List<Vector3>();
            Triangles = new List<int>();
            UVs = new List<Vector2>();
        }

        public CaveMeshData(int vertexCapacity, int triangleCapacity)
        {
            Vertices = new List<Vector3>(vertexCapacity);
            Triangles = new List<int>(triangleCapacity);
            UVs = new List<Vector2>(vertexCapacity);
        }

        public void Clear()
        {
            Vertices.Clear();
            Triangles.Clear();
            UVs.Clear();
        }

        public int AddVertex(Vector3 position, Vector2 uv)
        {
            int index = Vertices.Count;
            Vertices.Add(position);
            UVs.Add(uv);
            return index;
        }

        public void AddTriangleIndices(int i0, int i1, int i2)
        {
            Triangles.Add(i0);
            Triangles.Add(i1);
            Triangles.Add(i2);
        }

        public void AddQuadIndices(int i0, int i1, int i2, int i3)
        {
            Triangles.Add(i0);
            Triangles.Add(i1);
            Triangles.Add(i2);

            Triangles.Add(i0);
            Triangles.Add(i2);
            Triangles.Add(i3);
        }

        /// <summary>
        /// Inverts the winding order of all triangles in this mesh.
        /// </summary>
        public void InvertNormals()
        {
            for (int i = 0; i < Triangles.Count; i += 3)
            {
                int temp = Triangles[i + 1];
                Triangles[i + 1] = Triangles[i + 2];
                Triangles[i + 2] = temp;
            }
        }

        /// <summary>
        /// Applies buffered vertex data to the target Unity Mesh, recalculating normals and bounds.
        /// </summary>
        public void ApplyToMesh(Mesh mesh, bool recalculateNormals = true, bool recalculateBounds = true)
        {
            if (mesh == null) return;

            mesh.Clear();
            mesh.SetVertices(Vertices);
            mesh.SetTriangles(Triangles, 0);
            mesh.SetUVs(0, UVs);

            if (recalculateNormals)
            {
                mesh.RecalculateNormals();
            }

            if (recalculateBounds)
            {
                mesh.RecalculateBounds();
            }
        }
    }
}
