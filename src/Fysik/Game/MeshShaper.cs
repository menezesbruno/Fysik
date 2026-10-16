using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fysik.Game
{
    internal static class MeshShaper
    {
        private const int UvChannels = 4;

        public static Mesh Deform(Mesh source, Matrix4x4 toRoot, string name, Func<Vector3, Vector3> map,
                                  Func<Vector3, Matrix4x4> jacobian, float maxLength = 0)
        {
            if (!source.isReadable)
                throw new InvalidOperationException($"mesh {source.name} is not readable");
            Mesh mesh = maxLength > 0 ? Subdivide(source, toRoot, maxLength) : UnityEngine.Object.Instantiate(source);
            mesh.name = name;
            Matrix4x4 fromRoot = toRoot.inverse;
            Matrix4x4 normalToRoot = fromRoot.transpose, normalFromRoot = toRoot.transpose;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            mesh.GetTangents(tangents);
            bool hasNormals = normals.Count == vertices.Count, hasTangents = tangents.Count == vertices.Count;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(vertices[i]);
                Matrix4x4 j = jacobian(p);
                vertices[i] = fromRoot.MultiplyPoint3x4(map(p));
                if (hasNormals)
                {
                    Vector3 n = j.inverse.transpose.MultiplyVector(normalToRoot.MultiplyVector(normals[i]));
                    normals[i] = normalFromRoot.MultiplyVector(n).normalized;
                }
                if (hasTangents)
                {
                    Vector4 t = tangents[i];
                    Vector3 d = fromRoot.MultiplyVector(j.MultiplyVector(toRoot.MultiplyVector(t))).normalized;
                    tangents[i] = new Vector4(d.x, d.y, d.z, t.w);
                }
            }
            mesh.SetVertices(vertices);
            if (hasNormals)
                mesh.SetNormals(normals);
            if (hasTangents)
                mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Matrix4x4 Columns(Vector3 x, Vector3 y, Vector3 z)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.SetColumn(0, x);
            m.SetColumn(1, y);
            m.SetColumn(2, z);
            return m;
        }

        private sealed class Grid
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector4> Tangents = new List<Vector4>();
            public readonly List<Vector4>[] Uvs = new List<Vector4>[UvChannels];
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<float> Along = new List<float>();
            private readonly Dictionary<long, int> _middles = new Dictionary<long, int>();

            public Grid(Mesh source, Matrix4x4 toRoot)
            {
                source.GetVertices(Positions);
                source.GetNormals(Normals);
                source.GetTangents(Tangents);
                source.GetColors(Colors);
                for (int c = 0; c < UvChannels; c++)
                {
                    Uvs[c] = new List<Vector4>();
                    source.GetUVs(c, Uvs[c]);
                }
                foreach (Vector3 p in Positions)
                    Along.Add(toRoot.MultiplyPoint3x4(p).x);
            }

            public int Count => Positions.Count;

            public float Length(int a, int b) => Mathf.Abs(Along[a] - Along[b]);

            public int Middle(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (_middles.TryGetValue(key, out int m))
                    return m;
                int count = Positions.Count;
                m = count;
                Positions.Add((Positions[a] + Positions[b]) * 0.5f);
                Along.Add((Along[a] + Along[b]) * 0.5f);
                if (Normals.Count == count)
                    Normals.Add((Normals[a] + Normals[b]).normalized);
                if (Tangents.Count == count)
                {
                    Vector3 t = ((Vector3)Tangents[a] + (Vector3)Tangents[b]).normalized;
                    Tangents.Add(new Vector4(t.x, t.y, t.z, Tangents[a].w));
                }
                if (Colors.Count == count)
                    Colors.Add(Color.Lerp(Colors[a], Colors[b], 0.5f));
                foreach (List<Vector4> uv in Uvs)
                    if (uv.Count == count)
                        uv.Add((uv[a] + uv[b]) * 0.5f);
                _middles[key] = m;
                return m;
            }
        }

        public static Mesh Subdivide(Mesh source, Matrix4x4 toRoot, float maxLength)
        {
            var grid = new Grid(source, toRoot);
            var submeshes = new List<List<int>>();
            var pending = new Stack<int>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var output = new List<int>();
                int[] triangles = source.GetTriangles(s);
                for (int i = triangles.Length - 3; i >= 0; i -= 3)
                {
                    pending.Push(triangles[i + 2]);
                    pending.Push(triangles[i + 1]);
                    pending.Push(triangles[i]);
                }
                while (pending.Count > 0)
                {
                    int a = pending.Pop(), b = pending.Pop(), c = pending.Pop();
                    float ab = grid.Length(a, b), bc = grid.Length(b, c), ca = grid.Length(c, a);
                    if (Mathf.Max(ab, Mathf.Max(bc, ca)) <= maxLength)
                    {
                        output.Add(a);
                        output.Add(b);
                        output.Add(c);
                        continue;
                    }
                    if (ab >= bc && ab >= ca)
                        Split(pending, a, grid.Middle(a, b), b, c);
                    else if (bc >= ca)
                        Split(pending, b, grid.Middle(b, c), c, a);
                    else
                        Split(pending, c, grid.Middle(c, a), a, b);
                }
                submeshes.Add(output);
            }

            var mesh = new Mesh { name = source.name };
            if (grid.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(grid.Positions);
            if (grid.Normals.Count == grid.Count)
                mesh.SetNormals(grid.Normals);
            if (grid.Tangents.Count == grid.Count)
                mesh.SetTangents(grid.Tangents);
            if (grid.Colors.Count == grid.Count)
                mesh.SetColors(grid.Colors);
            for (int c = 0; c < UvChannels; c++)
                if (grid.Uvs[c].Count == grid.Count)
                    mesh.SetUVs(c, grid.Uvs[c]);
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++)
                mesh.SetTriangles(submeshes[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Split(Stack<int> pending, int a, int middle, int b, int c)
        {
            pending.Push(c);
            pending.Push(middle);
            pending.Push(a);
            pending.Push(c);
            pending.Push(b);
            pending.Push(middle);
        }
    }
}
