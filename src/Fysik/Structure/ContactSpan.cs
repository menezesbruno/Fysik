using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public static class ContactSpan
    {
        public const int GridSide = 3;

        public static double[] Open() => new[]
        {
            double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue,
        };

        public static void Include(double[] span, OrientedBox frame, Vec3 point)
        {
            Vec3 d = point - frame.Center;
            for (int k = 0; k < 3; k++)
            {
                double t = Vec3.Dot(d, frame.Axis(k));
                span[2 * k] = Math.Min(span[2 * k], t);
                span[2 * k + 1] = Math.Max(span[2 * k + 1], t);
            }
        }

        public static void Include(double[] span, OrientedBox frame, List<Vec3> points)
        {
            foreach (Vec3 p in points)
                Include(span, frame, p);
        }

        public static double[] Close(double[] span, OrientedBox frame, double margin)
        {
            if (span[0] > span[1])
                return null;
            var result = new double[6];
            for (int k = 0; k < 3; k++)
            {
                double lo = span[2 * k] + margin, hi = span[2 * k + 1] - margin;
                if (lo > hi)
                    lo = hi = (lo + hi) / 2;
                double half = frame.Half[k];
                result[2 * k] = Math.Max(-half, Math.Min(half, lo));
                result[2 * k + 1] = Math.Max(-half, Math.Min(half, hi));
            }
            return result;
        }

        public static int Patches(bool[] hit, int[] label)
        {
            int cells = GridSide * GridSide;
            for (int k = 0; k < cells; k++)
                label[k] = hit[k] ? k : -1;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int k = 0; k < cells; k++)
                {
                    if (label[k] < 0)
                        continue;
                    int row = k / GridSide, col = k % GridSide;
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int r = row + dr, c = col + dc;
                            if (r < 0 || c < 0 || r >= GridSide || c >= GridSide)
                                continue;
                            int n = r * GridSide + c;
                            if (label[n] >= 0 && label[n] < label[k])
                            {
                                label[k] = label[n];
                                changed = true;
                            }
                        }
                }
            }
            int count = 0;
            var renumber = new int[cells];
            for (int k = 0; k < cells; k++)
                renumber[k] = -1;
            for (int k = 0; k < cells; k++)
            {
                if (label[k] < 0)
                    continue;
                if (renumber[label[k]] < 0)
                    renumber[label[k]] = count++;
                label[k] = renumber[label[k]];
            }
            return count;
        }
    }
}
