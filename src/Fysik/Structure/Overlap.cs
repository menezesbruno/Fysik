using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public struct OrientedBox
    {
        public Vec3 Center;
        public Vec3 AxisX, AxisY, AxisZ;
        public Vec3 Half;

        public Vec3 Axis(int i) => i == 0 ? AxisX : i == 1 ? AxisY : AxisZ;
    }

    public static class Overlap
    {
        private const double PlaneTolerance = 1e-9;
        private const double SamePoint = 1e-12;
        private const double MinVolume = 1e-12;

        public static bool Of(OrientedBox a, OrientedBox b, out Vec3 centroid, out double volume)
        {
            centroid = Vec3.Zero;
            volume = 0;
            Vec3 origin = a.Center;
            List<List<Vec3>> faces = Faces(a, origin);
            for (int axis = 0; axis < 3; axis++)
            {
                Vec3 n = b.Axis(axis);
                double c = Vec3.Dot(n, b.Center - origin);
                faces = Clip(faces, n, c + b.Half[axis]);
                if (faces.Count == 0)
                    return false;
                faces = Clip(faces, -n, b.Half[axis] - c);
                if (faces.Count == 0)
                    return false;
            }
            if (!Measure(faces, out Vec3 local, out volume))
                return false;
            centroid = local + origin;
            return true;
        }

        private static List<List<Vec3>> Faces(OrientedBox box, Vec3 origin)
        {
            Vec3 c = box.Center - origin;
            Vec3 x = box.AxisX * box.Half.X, y = box.AxisY * box.Half.Y, z = box.AxisZ * box.Half.Z;
            var corners = new Vec3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = c + x * ((i & 1) == 0 ? -1 : 1) + y * ((i & 2) == 0 ? -1 : 1) + z * ((i & 4) == 0 ? -1 : 1);
            int[][] quads =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 },
                new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 },
                new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 },
            };
            var faces = new List<List<Vec3>>(6);
            foreach (int[] q in quads)
                faces.Add(new List<Vec3> { corners[q[0]], corners[q[1]], corners[q[2]], corners[q[3]] });
            return faces;
        }

        private static List<List<Vec3>> Clip(List<List<Vec3>> faces, Vec3 n, double limit)
        {
            bool outside = false, inside = false;
            foreach (List<Vec3> face in faces)
                foreach (Vec3 v in face)
                {
                    double s = Vec3.Dot(n, v) - limit;
                    outside |= s > PlaneTolerance;
                    inside |= s < -PlaneTolerance;
                }
            if (!outside)
                return faces;
            if (!inside)
                return new List<List<Vec3>>();

            var result = new List<List<Vec3>>(faces.Count + 1);
            var cap = new List<Vec3>();
            foreach (List<Vec3> face in faces)
            {
                var kept = new List<Vec3>(face.Count + 1);
                for (int i = 0; i < face.Count; i++)
                {
                    Vec3 p = face[i], q = face[(i + 1) % face.Count];
                    double sp = Vec3.Dot(n, p) - limit, sq = Vec3.Dot(n, q) - limit;
                    bool pIn = sp <= PlaneTolerance, qIn = sq <= PlaneTolerance;
                    if (pIn)
                        kept.Add(p);
                    if (pIn != qIn)
                        kept.Add(p + (q - p) * (sp / (sp - sq)));
                }
                if (kept.Count < 3)
                    continue;
                result.Add(kept);
                foreach (Vec3 v in kept)
                    if (Math.Abs(Vec3.Dot(n, v) - limit) <= PlaneTolerance)
                        AddUnique(cap, v);
            }
            if (cap.Count >= 3)
                result.Add(Ordered(cap, n));
            return result;
        }

        private static void AddUnique(List<Vec3> points, Vec3 p)
        {
            foreach (Vec3 q in points)
                if ((q - p).LengthSquared < SamePoint)
                    return;
            points.Add(p);
        }

        private static List<Vec3> Ordered(List<Vec3> points, Vec3 n)
        {
            Vec3 mean = Vec3.Zero;
            foreach (Vec3 p in points)
                mean += p;
            mean = mean / points.Count;
            Vec3 u = (points[0] - mean).Normalized();
            Vec3 w = Vec3.Cross(n, u);
            var keys = new double[points.Count];
            var order = new int[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vec3 d = points[i] - mean;
                keys[i] = PseudoAngle(Vec3.Dot(d, u), Vec3.Dot(d, w));
                order[i] = i;
            }
            Array.Sort(keys, order);
            var ordered = new List<Vec3>(points.Count);
            foreach (int i in order)
                ordered.Add(points[i]);
            return ordered;
        }

        private static double PseudoAngle(double x, double y)
        {
            double r = Math.Abs(x) + Math.Abs(y);
            if (r <= 0)
                return 0;
            double t = y / r;
            return x >= 0 ? (y >= 0 ? t : 4 + t) : 2 - t;
        }

        private static bool Measure(List<List<Vec3>> faces, out Vec3 centroid, out double volume)
        {
            centroid = Vec3.Zero;
            volume = 0;
            Vec3 o = Vec3.Zero;
            int count = 0;
            foreach (List<Vec3> face in faces)
                foreach (Vec3 v in face)
                {
                    o += v;
                    count++;
                }
            if (count == 0)
                return false;
            o = o / count;

            Vec3 sum = Vec3.Zero;
            foreach (List<Vec3> face in faces)
                for (int i = 1; i + 1 < face.Count; i++)
                {
                    Vec3 p0 = face[0] - o, p1 = face[i] - o, p2 = face[i + 1] - o;
                    double v = Math.Abs(Vec3.Dot(p0, Vec3.Cross(p1, p2))) / 6;
                    volume += v;
                    sum += (p0 + p1 + p2) * (v / 4);
                }
            if (volume < MinVolume)
                return false;
            centroid = o + sum / volume;
            return true;
        }
    }
}
