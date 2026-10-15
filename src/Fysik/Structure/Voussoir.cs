using System;

namespace Fysik.Structure
{
    public readonly struct Voussoir
    {
        public readonly double Angle;
        public readonly double Width;
        public readonly double Height;

        public Voussoir(double angle, double width, double height)
        {
            Angle = angle;
            Width = width;
            Height = height;
        }

        public static Voussoir HalfCircle(int stones, double innerRadius, double outerRadius)
        {
            double angle = 180.0 / stones;
            double half = angle * Math.PI / 360;
            return new Voussoir(angle, 2 * outerRadius * Math.Sin(half), (outerRadius - innerRadius) * Math.Cos(half));
        }

        public double OuterRadius => Width / (2 * Math.Tan(Angle * Math.PI / 360));

        public double InnerRadius => OuterRadius - Height;

        public double Centre => Height / 2 - OuterRadius;

        public double InnerWidth => Width * InnerRadius / OuterRadius;

        public double MeanWidth => (Width + InnerWidth) / 2;

        public int Stones => (int)Math.Round(180 / Angle);

        public double InnerCorner => InnerRadius / Math.Cos(Angle * Math.PI / 360);

        public double OuterCorner => OuterRadius / Math.Cos(Angle * Math.PI / 360);

        public double SpringerTurn => 90 - Angle / 2;

        public Vec3 SpringingFace => new Vec3(-(Width + InnerWidth) / 4, 0, 0);

        public double Narrowing(double y) => (y - Centre) / OuterRadius;

        public bool Contains(Vec3 p, double depth, double margin, bool openSides = false, bool openDepth = false,
                             double outerGrow = 0, double innerGrow = 0, double depthGrow = 0)
        {
            if (Math.Abs(p.Z) > depth / 2 + (openDepth ? margin + depthGrow : -margin))
                return false;
            if (p.Y < -Height / 2 - innerGrow + margin || p.Y > Height / 2 + outerGrow - margin)
                return false;
            return Math.Abs(p.X) < Width / 2 * Narrowing(p.Y) + (openSides ? margin : -margin);
        }

        public bool InSector(Vec3 p) => Math.Abs(p.X) <= Width / 2 * Narrowing(p.Y);

        public static bool InDepth(Vec3 p, double depth) => Math.Abs(p.Z) < depth / 2;

        public bool Beyond(Vec3 p) => (p - new Vec3(0, Centre, 0)).Length > OuterRadius - Height / 2;

        public Vec3 ToFace(Vec3 p, bool outward, double offset)
        {
            double face = outward ? Height / 2 + offset : -Height / 2 - offset;
            double scale = (face - Centre) / (p.Y - Centre);
            return new Vec3(p.X * scale, face, p.Z);
        }

        public double Turn(int side) => side >= 0 ? -Angle : Angle;

        public Vec3 Next(int side)
        {
            double a = Turn(side) * Math.PI / 180;
            double c = Centre;
            return new Vec3(c * Math.Sin(a), c - c * Math.Cos(a), 0);
        }
    }
}
