using System;

namespace Fysik.Structure
{
    public static class Panel
    {
        public const double Thickest = 0.35;
        public const double Narrowest = 0.9;

        public static bool Is(Vec3 size)
        {
            double a = Math.Abs(size.X), b = Math.Abs(size.Y), c = Math.Abs(size.Z);
            double thin = Math.Min(a, Math.Min(b, c)), wide = Math.Max(a, Math.Max(b, c));
            return thin <= Thickest && a + b + c - thin - wide >= Narrowest;
        }
    }
}
