using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public static class LoadPathAnalysis
    {
        private struct Neighbour
        {
            public int Other;
            public Vec3 Point;
        }

        private struct Support
        {
            public int Other;
            public Vec3 Point;
            public double Weight;
        }

        public static BodyResult[] Run(StructureModel model)
        {
            int n = model.Bodies.Count;
            var results = new BodyResult[n];
            var neighbours = new List<Neighbour>[n];
            var groundPoints = new List<Vec3>[n];
            for (int i = 0; i < n; i++)
            {
                neighbours[i] = new List<Neighbour>();
                groundPoints[i] = new List<Vec3>();
            }
            foreach (Link l in model.Links)
            {
                if (l.IsGround)
                {
                    groundPoints[l.A].Add(l.Point);
                }
                else
                {
                    neighbours[l.A].Add(new Neighbour { Other = l.B, Point = l.Point });
                    neighbours[l.B].Add(new Neighbour { Other = l.A, Point = l.Point });
                }
            }

            var level = new int[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                level[i] = groundPoints[i].Count > 0 ? 0 : int.MaxValue;
                if (level[i] == 0)
                    queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                foreach (Neighbour nb in neighbours[i])
                {
                    int j = nb.Other;
                    if (level[j] != int.MaxValue)
                        continue;
                    level[j] = level[i] + 1;
                    queue.Enqueue(j);
                }
            }

            var order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => level[a] != level[b] ? level[b].CompareTo(level[a]) : a.CompareTo(b));

            double g = model.Gravity.Length;
            Vec3 down = model.Gravity.Normalized();
            var load = new double[n];
            var loadMoment = new Vec3[n];
            for (int i = 0; i < n; i++)
            {
                load[i] = model.Bodies[i].Mass * g;
                loadMoment[i] = model.Bodies[i].Center * load[i];
            }

            var supports = new List<Support>();
            foreach (int i in order)
            {
                if (level[i] == int.MaxValue)
                {
                    results[i] = BodyResult.Unsupported;
                    continue;
                }

                Body body = model.Bodies[i];
                supports.Clear();
                if (level[i] == 0)
                {
                    foreach (Vec3 p in groundPoints[i])
                        supports.Add(new Support { Other = -1, Point = p, Weight = SupportWeight(body, p, down) });
                }
                else
                {
                    foreach (Neighbour nb in neighbours[i])
                        if (level[nb.Other] < level[i])
                            supports.Add(new Support { Other = nb.Other, Point = nb.Point, Weight = SupportWeight(body, nb.Point, down) });
                }

                double total = 0;
                Vec3 supportPoint = Vec3.Zero;
                foreach (Support s in supports)
                {
                    total += s.Weight;
                    supportPoint += s.Point * s.Weight;
                }
                supportPoint /= total;
                Vec3 centroid = loadMoment[i] / load[i];

                foreach (Support s in supports)
                {
                    if (s.Other < 0)
                        continue;
                    double share = load[i] * s.Weight / total;
                    load[s.Other] += share;
                    loadMoment[s.Other] += centroid * share;
                }

                results[i] = Check(body, load[i], centroid, supportPoint, supports, down);
            }
            return results;
        }

        private static double SupportWeight(Body body, Vec3 point, Vec3 down)
        {
            Vec3 dir = (point - body.Center).Normalized();
            return 0.25 + Math.Max(0, Vec3.Dot(dir, down));
        }

        private static Vec3 Horizontal(Vec3 v, Vec3 down) => v - down * Vec3.Dot(v, down);

        private static BodyResult Check(Body body, double load, Vec3 centroid, Vec3 supportPoint,
                                        List<Support> supports, Vec3 down)
        {
            Vec3 force = down * load;

            int vertical = MostAligned(body, down);
            BodyResult worst = Section(body, vertical, force, Vec3.Zero,
                                       vertical == Mechanics.LongestAxis(body) ? Mechanics.BucklingFactor(body) : 1);

            Vec3 arm = Horizontal(centroid - supportPoint, down);
            double spread = 0;
            for (int a = 0; a < supports.Count; a++)
                for (int b = a + 1; b < supports.Count; b++)
                    spread = Math.Max(spread, Horizontal(supports[a].Point - supports[b].Point, down).Length);

            if (arm.Length > 1e-6 || spread > 1e-6)
            {
                Vec3 armDir = arm.Length > 1e-6 ? arm.Normalized() : Horizontal(supports[1].Point - supports[0].Point, down).Normalized();
                Vec3 moment = Vec3.Cross(armDir, force) * (arm.Length + spread / 8);
                int axis = MostAligned(body, armDir);
                BodyResult bending = Section(body, axis, force, moment, 1);
                if (bending.Utilization > worst.Utilization)
                    worst = bending;
            }
            return worst;
        }

        private static BodyResult Section(Body body, int axis, Vec3 force, Vec3 moment, double buckling)
        {
            Vec3 e = body.Axis(axis);
            int i1 = (axis + 1) % 3, i2 = (axis + 2) % 3;
            if (Vec3.Dot(e, force) > 0)
                e = -e;
            return Mechanics.SectionCheck(body.Material, force, moment, e, body.Axis(i1), body.Axis(i2),
                                          body.Size[i1], body.Size[i2], buckling, body.StrengthFactor);
        }

        private static int MostAligned(Body body, Vec3 dir)
        {
            int best = 0;
            double bestDot = -1;
            for (int k = 0; k < 3; k++)
            {
                double d = Math.Abs(Vec3.Dot(body.Axis(k), dir));
                if (d > bestDot)
                {
                    bestDot = d;
                    best = k;
                }
            }
            return best;
        }
    }
}
