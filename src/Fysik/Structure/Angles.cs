using System;

namespace Fysik.Structure
{
    public static class Angles
    {
        public const double SavedTolerance = 0.6;

        public static bool TrySnap(double degrees, double origin, double step, out double snapped,
                                   double tolerance = SavedTolerance)
        {
            snapped = origin + Math.Round((degrees - origin) / step) * step;
            return Math.Abs(degrees - snapped) <= tolerance;
        }
    }
}
