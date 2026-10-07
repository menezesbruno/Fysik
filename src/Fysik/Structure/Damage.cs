namespace Fysik.Structure
{
    public static class Damage
    {
        public const double FullStrengthHealth = 0.5;

        public const double MinStrength = 0.25;

        public static double StrengthFactor(double health)
        {
            if (health >= FullStrengthHealth)
                return 1;
            if (health <= 0)
                return MinStrength;
            return MinStrength + (1 - MinStrength) * health / FullStrengthHealth;
        }
    }
}
