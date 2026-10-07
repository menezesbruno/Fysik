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
        public static CrackAction Decide(double utilization, bool unsupported, bool cracking, double secondsCracking,
                                         double warningSeconds, bool cascading, bool ready, bool canFall)
        {
            if (!ready)
                return CrackAction.None;

            bool overloaded = unsupported || utilization >= 1;
            if (!overloaded || !canFall)
                return cracking ? CrackAction.StopCrack : CrackAction.None;

            if (unsupported || cascading || warningSeconds <= 0)
                return CrackAction.Fall;
            if (!cracking)
                return CrackAction.StartCrack;
            return secondsCracking >= warningSeconds ? CrackAction.Fall : CrackAction.None;
        }
    }
}
