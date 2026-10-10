using System;

namespace Fysik.Structure
{
    public static class Loads
    {
        public const double FlatSnowShare = 0.8;

        public const double SnowSlidesFrom = 30;

        public const double SnowGoneAt = 60;

        public static double SnowShapeFactor(double slopeDegrees)
        {
            if (slopeDegrees <= SnowSlidesFrom)
                return FlatSnowShare;
            if (slopeDegrees >= SnowGoneAt)
                return 0;
            return FlatSnowShare * (SnowGoneAt - slopeDegrees) / (SnowGoneAt - SnowSlidesFrom);
        }

        public static double SnowArea(Vec3 axisX, Vec3 axisY, Vec3 axisZ, Vec3 size)
        {
            double ux = Math.Abs(axisX.Y), uy = Math.Abs(axisY.Y), uz = Math.Abs(axisZ.Y);
            double plan = size.Y * size.Z * ux + size.X * size.Z * uy + size.X * size.Y * uz;
            double up = Math.Min(1, Math.Max(ux, Math.Max(uy, uz)));
            double slope = Math.Acos(up) * 180 / Math.PI;
            return plan * SnowShapeFactor(slope);
        }
    }
}
