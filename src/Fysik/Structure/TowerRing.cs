using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public readonly struct TowerRing
    {
        public const double Overlap = 0.01;

        public readonly double OuterRadius;
        public readonly double Thickness;
        public readonly double Angle;
        public readonly double Height;

        public TowerRing(double outerRadius, double thickness, double angle, double height = 1)
        {
            OuterRadius = outerRadius;
            Thickness = thickness;
            Angle = angle;
            Height = height;
        }

        public double InnerRadius => OuterRadius - Thickness;

        public double MidRadius => OuterRadius - Thickness / 2;

        public double MeanWidth => MidRadius * Angle * Math.PI / 180;

        public double Volume => MeanWidth * Thickness * Height;

        public double BoxTurn => Angle / 2;

        public double Sag => OuterRadius * (1 - Math.Cos(Angle / 2 * Math.PI / 180));

        public double BoxDepth => Thickness - Sag;

        public double BoxShift => -Sag / 2;

        public Vec3 Centre => Direction(Angle / 2) * -MidRadius;

        public static Vec3 Direction(double degrees)
        {
            double a = degrees * Math.PI / 180;
            return new Vec3(Math.Sin(a), 0, Math.Cos(a));
        }

        public static Vec3 Turn(Vec3 v, double degrees)
        {
            double a = degrees * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            return new Vec3(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);
        }

        public Vec3 Point(double degrees, double radius, double y) => Centre + Direction(degrees) * radius + new Vec3(0, y, 0);

        public double AngleOf(Vec3 p)
        {
            Vec3 d = p - Centre;
            return Math.Atan2(d.X, d.Z) * 180 / Math.PI;
        }

        public int SideOf(Vec3 p) => AngleOf(p) >= Angle / 2 ? 1 : -1;

        public double Next(TowerRing ghost, int side, bool above, bool flush)
        {
            if (!above)
                return side >= 0 ? Angle : -ghost.Angle;
            if (flush)
                return side >= 0 ? Angle - ghost.Angle : 0;
            return side >= 0 ? Angle - ghost.Angle / 2 : -ghost.Angle / 2;
        }

        public Vec3 Origin(TowerRing ghost, double start, bool above)
        {
            double rise = above ? (Height + ghost.Height) / 2 : 0;
            return Centre - Turn(ghost.Centre, start) + new Vec3(0, rise, 0);
        }

        private static Vec3 Tangent(double degrees)
        {
            double a = degrees * Math.PI / 180;
            return new Vec3(Math.Cos(a), 0, -Math.Sin(a));
        }

        public double RadiusOf(Vec3 p)
        {
            Vec3 d = p - Centre;
            return Math.Sqrt(d.X * d.X + d.Z * d.Z);
        }

        public double FromStart(Vec3 p) => Vec3.Dot(p - Centre, Tangent(0));

        public double FromEnd(Vec3 p) => -Vec3.Dot(p - Centre, Tangent(Angle));

        public bool InSector(Vec3 p) => FromStart(p) >= 0 && FromEnd(p) >= 0;

        public bool Beyond(Vec3 p) => RadiusOf(p) > MidRadius;

        public bool Contains(Vec3 p, double margin, bool openSides = false, bool openDepth = false,
                             double outerGrow = 0, double innerGrow = 0, double depthGrow = 0)
        {
            if (Math.Abs(p.Y) > Height / 2 + (openDepth ? margin + depthGrow : -margin))
                return false;
            double r = RadiusOf(p);
            if (r < InnerRadius - innerGrow + margin || r > OuterRadius + outerGrow - margin)
                return false;
            double side = openSides ? -margin : margin;
            return FromStart(p) > side && FromEnd(p) > side;
        }

        public bool Claims(Vec3 p, bool outward, double margin, bool openSides = false, bool openDepth = false,
                           double outerGrow = 0, double innerGrow = 0, double depthGrow = 0)
        {
            if (Math.Abs(p.Y) > Height / 2 + (openDepth ? margin + depthGrow : -margin))
                return false;
            double r = RadiusOf(p);
            if (outward ? r > OuterRadius + outerGrow - margin : r < InnerRadius - innerGrow + margin)
                return false;
            double side = openSides ? -margin : margin;
            return FromStart(p) > side && FromEnd(p) > side;
        }

        public Vec3 ToFace(Vec3 p, bool outward, double offset)
        {
            double r = RadiusOf(p);
            if (r < 1e-9)
                return p;
            double scale = (outward ? OuterRadius + offset : InnerRadius - offset) / r;
            Vec3 d = p - Centre;
            return new Vec3(Centre.X + d.X * scale, p.Y, Centre.Z + d.Z * scale);
        }

        public Vec3 ToFaceWithin(Vec3 p, OrientedBox block, bool outward, double offset, double slack)
        {
            Vec3 radial = ToFace(p, outward, offset);
            if (Inside(radial, block, slack) && Math.Abs(Vec3.Dot(radial - p, block.AxisY)) <= slack)
                return radial;
            double target = outward ? OuterRadius + offset : InnerRadius - offset;
            double dx = p.X - Centre.X, dz = p.Z - Centre.Z;
            Vec3 best = default;
            double shortest = double.MaxValue;
            for (int k = 0; k < 2; k++)
            {
                Vec3 axis = k == 0 ? block.AxisX : block.AxisZ;
                double half = (k == 0 ? block.Half.X : block.Half.Z) + slack;
                double flat = axis.X * axis.X + axis.Z * axis.Z;
                if (flat < 1e-12)
                    continue;
                double along = dx * axis.X + dz * axis.Z;
                double disc = along * along - flat * (dx * dx + dz * dz - target * target);
                if (disc < 0)
                    continue;
                foreach (double t in new[] { (-along - Math.Sqrt(disc)) / flat, (-along + Math.Sqrt(disc)) / flat })
                {
                    if (Math.Abs(t) >= shortest)
                        continue;
                    Vec3 q = p + axis * t;
                    if (Math.Abs(Vec3.Dot(q - block.Center, axis)) > half)
                        continue;
                    shortest = Math.Abs(t);
                    best = q;
                }
            }
            return shortest < double.MaxValue ? best : radial;
        }

        private static bool Inside(Vec3 p, OrientedBox block, double slack)
        {
            Vec3 d = p - block.Center;
            return Math.Abs(Vec3.Dot(d, block.AxisX)) <= block.Half.X + slack && Math.Abs(Vec3.Dot(d, block.AxisZ)) <= block.Half.Z + slack;
        }

        public bool Opens(Vec3 centre, IEnumerable<Vec3> corners)
        {
            if (InSector(centre))
                return true;
            foreach (Vec3 c in corners)
                if (FromStart(c) > Overlap && FromEnd(c) > Overlap)
                    return true;
            return false;
        }

        public bool Reaches(IEnumerable<Vec3> corners)
        {
            double bottom = double.MaxValue, top = double.MinValue;
            foreach (Vec3 c in corners)
            {
                bottom = Math.Min(bottom, c.Y);
                top = Math.Max(top, c.Y);
            }
            return Math.Min(top, Height / 2) - Math.Max(bottom, -Height / 2) > Overlap;
        }
    }
}
