using System;
using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class AnglesTests
    {
        private static readonly double[] Noise = { 0, -2e-5, 2e-5 };

        private static double Wrap(double degrees) => (degrees % 360 + 360) % 360;

        private static double Saved(double degrees) => Math.Floor(Wrap(degrees) * 2) / 2;

        private static void SameAngle(double expected, double actual) =>
            Assert.True(Math.Abs(Wrap(actual - expected + 180) - 180) < 1e-9, $"expected {expected}, got {actual}");

        [Fact]
        public void Tower_stone_turns_come_back_exactly_after_a_world_save()
        {
            for (int k = 0; k < 32; k++)
                foreach (double noise in Noise)
                {
                    double yaw = k * 11.25;
                    Assert.True(Angles.TrySnap(Saved(yaw + noise), 0, 11.25, out double snapped));
                    SameAngle(yaw, snapped);
                }
        }

        [Theory]
        [InlineData(9)]
        [InlineData(13)]
        [InlineData(15)]
        [InlineData(19)]
        [InlineData(23)]
        [InlineData(25)]
        [InlineData(29)]
        public void Arch_stone_tilts_come_back_exactly_after_a_world_save(int stones)
        {
            Voussoir v = Voussoir.HalfCircle(stones, 1, 2);
            for (int j = -stones; j <= stones; j++)
                foreach (double noise in Noise)
                {
                    double tilt = v.SpringerTurn - j * v.Angle;
                    Assert.True(Angles.TrySnap(Saved(tilt + noise), v.SpringerTurn, v.Angle, out double snapped));
                    SameAngle(tilt, snapped);
                }
        }

        [Fact]
        public void A_turn_far_from_the_steps_is_left_alone()
        {
            Assert.False(Angles.TrySnap(5, 0, 11.25, out double _));
            Assert.False(Angles.TrySnap(Saved(30.2), 0, 22.5, out double _));
            Assert.True(Angles.TrySnap(Saved(0 - 2e-5), 0, 360, out double flat));
            SameAngle(0, flat);
        }
    }
}
