using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class PanelTests
    {
        [Theory]
        [InlineData(2, 0.13, 2)]
        [InlineData(1, 0.13, 1)]
        [InlineData(2, 2, 0.3)]
        [InlineData(2, 1, 0.3)]
        [InlineData(1, 1, 0.3)]
        [InlineData(2, 0.14, 2.29)]
        public void Floors_wall_panels_and_roofs_are_panels(double x, double y, double z)
        {
            Assert.True(Panel.Is(new Vec3(x, y, z)));
        }

        [Theory]
        [InlineData(2, 0.4, 0.4)]
        [InlineData(0.4, 4, 0.4)]
        [InlineData(4, 0.5, 0.5)]
        [InlineData(2, 0.5, 0.5)]
        [InlineData(0.4, 0.6, 2.83)]
        [InlineData(2, 0.19, 0.1)]
        [InlineData(1, 0.05, 0.6)]
        [InlineData(2, 3.47, 0.4)]
        public void Beams_poles_logs_and_narrow_boards_are_not(double x, double y, double z)
        {
            Assert.False(Panel.Is(new Vec3(x, y, z)));
        }

        [Fact]
        public void A_turned_or_mirrored_panel_is_still_one()
        {
            Assert.True(Panel.Is(new Vec3(-0.13, 2, -2)));
        }
    }
}
