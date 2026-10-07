using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class DamageTests
    {
        [Theory]
        [InlineData(1.0, 1.0)]
        [InlineData(0.75, 1.0)]
        [InlineData(0.5, 1.0)]
        [InlineData(0.25, 0.625)]
        [InlineData(0.0, 0.25)]
        public void Strength_holds_down_to_half_health_then_falls_to_a_quarter(double health, double strength)
        {
            Assert.Equal(strength, Damage.StrengthFactor(health), 9);
        }

        [Fact]
        public void Strength_never_rises_as_health_falls()
        {
            double previous = Damage.StrengthFactor(1);
            for (int i = 99; i >= -5; i--)
            {
                double current = Damage.StrengthFactor(i / 100.0);
                Assert.True(current <= previous);
                previous = current;
            }
        }
    }
}
