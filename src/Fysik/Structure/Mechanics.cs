using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public struct ContactLoad
    {
        public Vec3 Point;
        public Vec3 Force;
        public Vec3 Moment;
    }

    public static class Mechanics
    {
        private const double ShearCorrection = 5.0 / 6.0;

        private const double PlateAspect = 4.0;
        private const double MinSegmentLength = 0.05;

        public static void SegmentStiffness(Body body, Vec3 point, out Mat3 translational, out Mat3 rotational)
        {
            Vec3 d = point - body.Center;
            int axial = 0;
            double best = -1;
            for (int k = 0; k < 3; k++)
            {
                double c = Math.Abs(Vec3.Dot(d, body.Axis(k)));
                if (c > best)
                {
                    best = c;
                    axial = k;
                }
            }
            if (d.LengthSquared < 1e-12)
                axial = SmallestAxis(body);

            double length = Math.Max(best, MinSegmentLength);
            int i1 = (axial + 1) % 3, i2 = (axial + 2) % 3;
            double s1 = body.Size[i1], s2 = body.Size[i2];
            Vec3 a = body.Axis(axial), e1 = body.Axis(i1), e2 = body.Axis(i2);

            double area = s1 * s2;
            double inertia1 = s1 * s2 * s2 * s2 / 12;
            double inertia2 = s2 * s1 * s1 * s1 / 12;
            double torsion = TorsionConstant(s1, s2);
            double e = body.Material.Elasticity, g = body.Material.ShearModulus;

            double shear = ShearCorrection * g * area / length;
            translational = Mat3.Outer(a, e * area / length) + Mat3.Outer(e1, shear) + Mat3.Outer(e2, shear);
            rotational = Mat3.Outer(a, g * torsion / length)
                         + Mat3.Outer(e1, e * inertia1 / length)
                         + Mat3.Outer(e2, e * inertia2 / length);
        }

        public static Mat3 Series(Mat3 k1, Mat3 k2)
        {
            Mat3 c1 = k1.Inverse(), c2 = k2.Inverse();
            return (c1 + c2).Inverse();
        }

        public static double TorsionConstant(double s1, double s2)
        {
            double a = Math.Max(s1, s2), c = Math.Min(s1, s2);
            double ratio = c / a;
            return a * c * c * c * (1.0 / 3.0 - 0.21 * ratio * (1 - ratio * ratio * ratio * ratio / 12));
        }

        public static int SmallestAxis(Body body)
        {
            Vec3 s = body.Size;
            return s.X <= s.Y && s.X <= s.Z ? 0 : s.Y <= s.Z ? 1 : 2;
        }

        public static int LongestAxis(Body body)
        {
            Vec3 s = body.Size;
            return s.X >= s.Y && s.X >= s.Z ? 0 : s.Y >= s.Z ? 1 : 2;
        }

        public static double BucklingFactor(Body body)
        {
            int axis = LongestAxis(body);
            double length = body.Size[axis];
            double thickness = Math.Min(body.Size[(axis + 1) % 3], body.Size[(axis + 2) % 3]);
            double gyration = thickness / Math.Sqrt(12);
            double slenderness = length / Math.Max(gyration, 1e-6);
            double ratio = slenderness / Math.Max(body.Material.BucklingSlenderness, 1e-6);
            return 1 / (1 + ratio * ratio);
        }

        public static BodyResult Evaluate(Body body, IReadOnlyList<ContactLoad> contacts, Vec3 gravity)
        {
            Vec3 weight = gravity * body.Mass;
            int longest = LongestAxis(body);
            double buckling = BucklingFactor(body);
            var worst = new BodyResult { Mode = StressMode.None };
            var cuts = new List<double>(contacts.Count * 2 + 1);

            for (int axis = 0; axis < 3; axis++)
            {
                Vec3 e = body.Axis(axis);
                double span = body.Size[axis];
                double half = span / 2;
                double eps = 1e-3 * span + 1e-6;

                cuts.Clear();
                cuts.Add(0);
                foreach (ContactLoad c in contacts)
                {
                    double t = Vec3.Dot(c.Point - body.Center, e);
                    cuts.Add(Clamp(t - eps, -half + eps, half - eps));
                    cuts.Add(Clamp(t + eps, -half + eps, half - eps));
                }

                int i1 = (axis + 1) % 3, i2 = (axis + 2) % 3;
                Vec3 e1 = body.Axis(i1), e2 = body.Axis(i2);
                double s1 = body.Size[i1], s2 = body.Size[i2];
                double phi = axis == longest ? buckling : 1;

                foreach (double t in cuts)
                {
                    Vec3 origin = body.Center + e * t;
                    Vec3 force = Vec3.Zero, moment = Vec3.Zero;
                    foreach (ContactLoad c in contacts)
                    {
                        if (Vec3.Dot(c.Point - body.Center, e) <= t)
                            continue;
                        force += c.Force;
                        moment += c.Moment + Vec3.Cross(c.Point - origin, c.Force);
                    }

                    double fraction = span > 0 ? (half - t) / span : 0;
                    Vec3 partWeight = weight * fraction;
                    Vec3 partCentre = body.Center + e * ((t + half) / 2);
                    force += partWeight;
                    moment += Vec3.Cross(partCentre - origin, partWeight);

                    BodyResult r = SectionCheck(body.Material, force, moment, e, e1, e2, s1, s2, phi, body.StrengthFactor);
                    if (r.Utilization > worst.Utilization || worst.Mode == StressMode.None)
                        worst = r;
                }
            }
            return worst;
        }

        public static BodyResult SectionCheck(MaterialProps m, Vec3 force, Vec3 moment, Vec3 e, Vec3 e1, Vec3 e2,
                                              double s1, double s2, double buckling, double strengthFactor = 1)
        {
            double k = Math.Max(strengthFactor, 1e-3);
            double area = s1 * s2;
            double axial = Vec3.Dot(force, e);
            double shear = (force - e * axial).Length;
            double torque = Math.Abs(Vec3.Dot(moment, e));
            double m1 = Math.Abs(Vec3.Dot(moment, e1));
            double m2 = Math.Abs(Vec3.Dot(moment, e2));

            double modulus1 = s1 * s2 * s2 / 6;
            double modulus2 = s2 * s1 * s1 / 6;
            double sigmaAxial = axial / area;
            double sigmaBending;
            double tauShear = 1.5 * shear / area;
            double tauTorsion;

            double a = Math.Max(s1, s2), c = Math.Min(s1, s2);
            if (a >= PlateAspect * c)
            {
                double weak = s1 >= s2 ? m1 : m2;
                double strong = s1 >= s2 ? m2 : m1;
                double mb = weak / a, mt = torque / a;
                double principal = Math.Abs(mb) / 2 + Math.Sqrt(mb * mb / 4 + mt * mt);
                sigmaBending = 6 * principal / (c * c) + strong / (c * a * a / 6);
                tauTorsion = 0;
            }
            else
            {
                sigmaBending = m1 / modulus1 + m2 / modulus2;
                tauTorsion = torque * (3 + 1.8 * c / a) / (a * c * c);
            }
            double tau = tauShear + tauTorsion;

            double compressionFibre = Math.Max(0, -sigmaAxial) / Math.Max(buckling, 1e-6)
                                      - Math.Max(0, sigmaAxial) + sigmaBending;
            double tensionFibre = sigmaAxial + sigmaBending;

            double uc = compressionFibre / (m.CompressiveStrength * k);
            double ut = tensionFibre / (m.TensileStrength * k);
            double us = tau / (m.ShearStrength * k);

            bool bendingDominates = sigmaBending > Math.Abs(sigmaAxial);
            var result = new BodyResult
            {
                AxialForce = axial,
                BendingMoment = Math.Sqrt(m1 * m1 + m2 * m2),
                Torque = torque,
            };
            if (uc >= ut && uc >= us)
            {
                result.Utilization = Math.Max(0, uc);
                result.Mode = bendingDominates ? StressMode.Bending : StressMode.Compression;
            }
            else if (ut >= us)
            {
                result.Utilization = ut;
                result.Mode = bendingDominates ? StressMode.Bending : StressMode.Tension;
            }
            else
            {
                result.Utilization = us;
                result.Mode = tauTorsion > tauShear ? StressMode.Torsion : StressMode.Shear;
            }
            return result;
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
