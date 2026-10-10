using System;

namespace Fysik.Structure
{
    public enum CrackAction : byte
    {
        None,
        StartCrack,
        StopCrack,
        Fall,
    }

    public static class CrackPolicy
    {
        public const double FullWarningUpTo = 1.1;

        public const double NoWarningFrom = 2.0;

        public static double WarningFor(double utilization, double warningSeconds) =>
            warningSeconds * Math.Max(0, Math.Min(1, (NoWarningFrom - utilization) / (NoWarningFrom - FullWarningUpTo)));

        public static CrackAction Decide(double utilization, bool unsupported, bool cracking, double secondsCracking,
                                         double warningSeconds, bool cascading, bool ready, bool canFall)
        {
            if (!ready)
                return CrackAction.None;

            bool overloaded = unsupported || utilization >= 1;
            if (!overloaded || !canFall)
                return cracking ? CrackAction.StopCrack : CrackAction.None;

            double warning = WarningFor(utilization, warningSeconds);
            if (unsupported || cascading || warning <= 0)
                return CrackAction.Fall;
            if (!cracking)
                return CrackAction.StartCrack;
            return secondsCracking >= warning ? CrackAction.Fall : CrackAction.None;
        }
    }
}
