using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public sealed class MeshCarver
    {
        public readonly List<Vec3> Points;
        public readonly int OriginalCount;
        public readonly List<int> FirstParent = new List<int>();
        public readonly List<int> SecondParent = new List<int>();

        private readonly Func<Vec3, int> _wedgeOf;
        private readonly int _maxDepth;
        private readonly Dictionary<long, int> _middles = new Dictionary<long, int>();

        public MeshCarver(IEnumerable<Vec3> points, Func<Vec3, int> wedgeOf, int maxDepth)
        {
            Points = new List<Vec3>(points);
            OriginalCount = Points.Count;
            _wedgeOf = wedgeOf;
            _maxDepth = maxDepth;
        }

        public List<int> Split(IReadOnlyList<int> triangles)
        {
            var output = new List<int>(triangles.Count);
            for (int t = 0; t + 2 < triangles.Count; t += 3)
                Split(triangles[t], triangles[t + 1], triangles[t + 2], 0, output);
            return output;
        }

        public bool Push(Func<Vec3, int, Vec3> push)
        {
            bool moved = false;
            for (int i = 0; i < Points.Count; i++)
            {
                int wedge = _wedgeOf(Points[i]);
                if (wedge < 0)
                    continue;
                Points[i] = push(Points[i], wedge);
                moved = true;
            }
            return moved;
        }

        public void Clamp(OrientedBox box)
        {
            for (int i = 0; i < Points.Count; i++)
            {
                Vec3 d = Points[i] - box.Center;
                double x = Math.Max(-box.Half.X, Math.Min(box.Half.X, Vec3.Dot(d, box.AxisX)));
                double y = Math.Max(-box.Half.Y, Math.Min(box.Half.Y, Vec3.Dot(d, box.AxisY)));
                double z = Math.Max(-box.Half.Z, Math.Min(box.Half.Z, Vec3.Dot(d, box.AxisZ)));
                Points[i] = box.Center + box.AxisX * x + box.AxisY * y + box.AxisZ * z;
            }
        }

        private void Split(int a, int b, int c, int depth, List<int> output)
        {
            if (depth < _maxDepth && Straddles(Points[a], Points[b], Points[c]))
            {
                int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                Split(a, ab, ca, depth + 1, output);
                Split(ab, b, bc, depth + 1, output);
                Split(ca, bc, c, depth + 1, output);
                Split(ab, bc, ca, depth + 1, output);
                return;
            }
            output.Add(a);
            output.Add(b);
            output.Add(c);
        }

        private bool Straddles(Vec3 a, Vec3 b, Vec3 c)
        {
            int first = _wedgeOf(a);
            return _wedgeOf(b) != first || _wedgeOf(c) != first || _wedgeOf((a + b) * 0.5) != first ||
                   _wedgeOf((b + c) * 0.5) != first || _wedgeOf((c + a) * 0.5) != first || _wedgeOf((a + b + c) / 3) != first;
        }

        private int Middle(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (_middles.TryGetValue(key, out int m))
                return m;
            m = Points.Count;
            Points.Add((Points[a] + Points[b]) * 0.5);
            FirstParent.Add(a);
            SecondParent.Add(b);
            _middles[key] = m;
            return m;
        }
    }
}
