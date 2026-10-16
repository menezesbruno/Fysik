using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class WallTopTests
    {
        private static void Near(Vec3 expected, Vec3 actual) =>
            Assert.True((expected - actual).Length < 1e-9, $"expected {expected}, got {actual}");

        [Fact]
        public void A_floor_on_a_thin_wall_covers_its_top_flush_with_the_far_face()
        {
            var half = new Vec3(1, 0.5, 0.25);
            Near(new Vec3(0, 0.75, 0.75), WallTop.Rest(Vec3.Zero, half, 2, 0.5, new Vec3(0.3, 0.5, 0.1), 1));
            Near(new Vec3(0, 0.75, -0.75), WallTop.Rest(Vec3.Zero, half, 2, 0.5, new Vec3(0.3, 0.5, 0.1), -1));
        }

        [Fact]
        public void A_floor_on_a_metre_thick_wall_sits_the_same_way()
        {
            Near(new Vec3(0, 0.75, 0.5), WallTop.Rest(Vec3.Zero, new Vec3(1, 0.5, 0.5), 2, 0.5, new Vec3(-0.2, 0.5, 0), 1));
        }

        [Theory]
        [InlineData(0.3, 0.5)]
        [InlineData(-0.7, -0.5)]
        [InlineData(1.4, 1.5)]
        public void A_small_floor_steps_along_the_wall_on_the_metre_grid_from_its_ends(double hit, double expected)
        {
            Vec3 rest = WallTop.Rest(Vec3.Zero, new Vec3(1, 0.5, 0.25), 1, 0.5, new Vec3(hit, 0.5, 0), 1);
            Assert.Equal(expected, rest.X, 9);
            Assert.Equal(0.25, rest.Z, 9);
        }

        [Fact]
        public void A_wall_running_along_its_own_depth_is_followed_too()
        {
            Near(new Vec3(-0.75, 0.75, 0), WallTop.Rest(Vec3.Zero, new Vec3(0.25, 0.5, 1), 2, 0.5, new Vec3(0, 0.5, 0.2), -1));
        }

        [Fact]
        public void Only_narrow_tops_carry_a_floor_this_way()
        {
            Assert.True(WallTop.Bears(new Vec3(1, 0.5, 0.25)));
            Assert.True(WallTop.Bears(new Vec3(2, 1, 0.5)));
            Assert.False(WallTop.Bears(new Vec3(1, 0.25, 1)));
        }
    }
}
