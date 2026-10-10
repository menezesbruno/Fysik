using System;
using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class LoadsTests
    {
        private static double Tilted(double degrees, Vec3 size)
        {
            double a = degrees * Math.PI / 180;
            var slope = new Vec3(0, Math.Sin(a), Math.Cos(a));
            var normal = new Vec3(0, Math.Cos(a), -Math.Sin(a));
            return Loads.SnowArea(Vec3.UnitX, normal, slope, size);
        }

        [Theory]
        [InlineData(0, 0.8)]
        [InlineData(26, 0.8)]
        [InlineData(30, 0.8)]
        [InlineData(45, 0.4)]
        [InlineData(60, 0.0)]
        [InlineData(75, 0.0)]
        public void Steeper_roofs_keep_less_snow(double slope, double share) =>
            Assert.Equal(share, Loads.SnowShapeFactor(slope), 9);

        [Fact]
        public void A_flat_floor_holds_snow_over_its_whole_top() =>
            Assert.Equal(2 * 2 * 0.8, Loads.SnowArea(Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ, new Vec3(2, 0.13, 2)), 9);

        [Fact]
        public void A_roof_holds_snow_over_its_plan_area()
        {
            var roof = new Vec3(2, 0.14, 2.29);
            double a = 26 * Math.PI / 180;
            double plan = 2 * 2.29 * Math.Cos(a) + 2 * 0.14 * Math.Sin(a);
            Assert.Equal(plan * 0.8, Tilted(26, roof), 9);
            Assert.True(Tilted(45, new Vec3(2, 0.16, 2.83)) < Tilted(26, roof) * 0.55);
        }

        [Fact]
        public void A_wall_only_holds_snow_on_its_top_edge() =>
            Assert.Equal(2 * 0.2 * 0.8, Loads.SnowArea(Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ, new Vec3(2, 2, 0.2)), 9);
    }
}
