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
        private readonly Func<int, double> _longest;
        private readonly Dictionary<long, int> _middles = new Dictionary<long, int>();

        public MeshCarver(IEnumerable<Vec3> points, Func<Vec3, int> wedgeOf, int maxDepth, Func<int, double> longest = null)
        {
            Points = new List<Vec3>(points);
            OriginalCount = Points.Count;
            _wedgeOf = wedgeOf;
            _maxDepth = maxDepth;
            _longest = longest;
        }

        public List<int> Split(IReadOnlyList<int> triangles)
        {
            var split = new List<int>(triangles.Count);
            for (int t = 0; t + 2 < triangles.Count; t += 3)
                Split(triangles[t], triangles[t + 1], triangles[t + 2], 0, split);
            var output = new List<int>(split.Count);
            for (int t = 0; t + 2 < split.Count; t += 3)
                Conform(split[t], split[t + 1], split[t + 2], output);
            return output;
        }

        private void Conform(int a, int b, int c, List<int> output)
        {
            if (_middles.TryGetValue(Key(a, b), out int m))
            {
                Conform(a, m, c, output);
                Conform(m, b, c, output);
            }
            else if (_middles.TryGetValue(Key(b, c), out m))
            {
                Conform(a, b, m, output);
                Conform(a, m, c, output);
            }
            else if (_middles.TryGetValue(Key(c, a), out m))
            {
                Conform(a, b, m, output);
                Conform(m, b, c, output);
            }
            else
            {
                output.Add(a);
                output.Add(b);
                output.Add(c);
            }
        }

        private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

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

        private const int DeepestSplit = 8;

        private void Split(int a, int b, int c, int depth, List<int> output)
        {
            if (Divides(Points[a], Points[b], Points[c], depth))
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

        private readonly int[] _samples = new int[7];

        private bool Divides(Vec3 a, Vec3 b, Vec3 c, int depth)
        {
            _samples[0] = _wedgeOf(a);
            _samples[1] = _wedgeOf(b);
            _samples[2] = _wedgeOf(c);
            _samples[3] = _wedgeOf((a + b) * 0.5);
            _samples[4] = _wedgeOf((b + c) * 0.5);
            _samples[5] = _wedgeOf((c + a) * 0.5);
            _samples[6] = _wedgeOf((a + b + c) / 3);
            bool straddles = false;
            for (int i = 1; i < _samples.Length; i++)
                straddles |= _samples[i] != _samples[0];
            if (straddles && depth < _maxDepth)
                return true;
            if (_longest == null || depth >= DeepestSplit)
                return false;
            double limit = 0;
            foreach (int wedge in _samples)
            {
                double longest = wedge >= 0 ? _longest(wedge) : 0;
                if (longest > 0 && (limit == 0 || longest < limit))
                    limit = longest;
            }
            if (limit <= 0 || (!straddles && _samples[0] < 0))
                return false;
            return Math.Max((a - b).Length, Math.Max((b - c).Length, (c - a).Length)) > limit;
        }

        private int Middle(int a, int b)
        {
            long key = Key(a, b);
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
