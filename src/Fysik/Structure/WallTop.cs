using System;

namespace Fysik.Structure
{
    public static class WallTop
    {
        public const double Grid = 1;
        public const double Narrowest = 1.05;

        public static bool Bears(Vec3 half) => Math.Min(half.X, half.Z) * 2 <= Narrowest;

        public static Vec3 Rest(Vec3 centre, Vec3 half, double width, double thickness, Vec3 hit, double side)
        {
            bool lengthX = half.X >= half.Z;
            double length = lengthX ? half.X : half.Z, across = lengthX ? half.Z : half.X;
            double along = lengthX ? hit.X - centre.X : hit.Z - centre.Z;
            double start = -length + width / 2;
            double a = start + Math.Round((along - start) / Grid) * Grid;
            double b = (side >= 0 ? 1 : -1) * (width / 2 - across);
            double y = centre.Y + half.Y + thickness / 2;
            return lengthX ? new Vec3(centre.X + a, y, centre.Z + b) : new Vec3(centre.X + b, y, centre.Z + a);
        }
    }
}
