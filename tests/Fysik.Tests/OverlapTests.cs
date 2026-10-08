using System;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class OverlapTests
    {
        private readonly ITestOutputHelper _out;

        public OverlapTests(ITestOutputHelper output) => _out = output;

        private static OrientedBox Box(Vec3 center, Vec3 size, double yawDegrees = 0)
        {
            double a = yawDegrees * Math.PI / 180;
            var x = new Vec3(Math.Cos(a), 0, -Math.Sin(a));
            var z = new Vec3(Math.Sin(a), 0, Math.Cos(a));
            return new OrientedBox { Center = center, AxisX = x, AxisY = Vec3.UnitY, AxisZ = z, Half = size * 0.5 };
        }

        [Fact]
        public void Aligned_boxes_overlap_in_a_box()
        {
            OrientedBox a = Box(new Vec3(0, 0, 0), new Vec3(2, 2, 2));
            OrientedBox b = Box(new Vec3(1.5, 0.5, 0), new Vec3(2, 2, 4));

            Assert.True(Overlap.Of(a, b, out Vec3 centroid, out double volume));
            Assert.Equal(0.5 * 1.5 * 2, volume, 1e-9);
            Assert.Equal(0.75, centroid.X, 1e-9);
            Assert.Equal(0.25, centroid.Y, 1e-9);
            Assert.Equal(0, centroid.Z, 1e-9);
        }

        [Fact]
        public void Floor_on_a_beam_under_its_edge_touches_along_the_middle_of_the_edge()
        {
            OrientedBox floor = Box(new Vec3(1, 0.315, 1), new Vec3(2.3, 0.43, 2.3));
            OrientedBox beam = Box(new Vec3(0, 0, 2), new Vec3(0.8, 0.8, 4.3));

            Assert.True(Overlap.Of(floor, beam, out Vec3 one, out double v1));
            Assert.True(Overlap.Of(beam, floor, out Vec3 other, out double v2));
            _out.WriteLine($"contact at ({one.X:0.000}, {one.Y:0.000}, {one.Z:0.000})");

            Assert.Equal(0.125, one.X, 1e-9);
            Assert.Equal(1, one.Z, 1e-9);
            Assert.Equal(v1, v2, 1e-9);
            Assert.Equal(0, (one - other).Length, 1e-9);
        }

        [Theory]
        [InlineData(45)]
        [InlineData(22.5)]
        [InlineData(67.5)]
        public void Rotated_overlap_matches_sampling(double yaw)
        {
            OrientedBox a = Box(new Vec3(0, 0, 0), new Vec3(4, 0.5, 0.5));
            OrientedBox b = Box(new Vec3(1.2, 0.3, 0.3), new Vec3(2, 0.5, 1), yaw);

            Assert.True(Overlap.Of(a, b, out Vec3 centroid, out double volume));

            const int n = 80;
            double inside = 0;
            Vec3 sum = Vec3.Zero;
            double cell = 4.0 * 0.5 * 0.5 / (n * n * n);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    for (int k = 0; k < n; k++)
                    {
                        var p = new Vec3(-2 + 4 * (i + 0.5) / n, -0.25 + 0.5 * (j + 0.5) / n, -0.25 + 0.5 * (k + 0.5) / n);
                        Vec3 d = p - b.Center;
                        if (Math.Abs(Vec3.Dot(d, b.AxisX)) <= b.Half.X && Math.Abs(Vec3.Dot(d, b.AxisY)) <= b.Half.Y &&
                            Math.Abs(Vec3.Dot(d, b.AxisZ)) <= b.Half.Z)
                        {
                            inside += cell;
                            sum += p * cell;
                        }
                    }
            Vec3 sampled = sum / inside;
            _out.WriteLine($"volume {volume:0.0000} (sampled {inside:0.0000}), centroid ({centroid.X:0.000}, {centroid.Y:0.000}, {centroid.Z:0.000}) " +
                           $"(sampled ({sampled.X:0.000}, {sampled.Y:0.000}, {sampled.Z:0.000}))");

            Assert.Equal(inside, volume, inside * 0.02);
            Assert.Equal(0, (centroid - sampled).Length, 0.01);
        }

        [Fact]
        public void Separate_boxes_do_not_overlap()
        {
            OrientedBox a = Box(new Vec3(0, 0, 0), new Vec3(1, 1, 1));
            OrientedBox b = Box(new Vec3(1.2, 0, 0), new Vec3(1, 1, 1), 30);
            Assert.False(Overlap.Of(a, b, out _, out _));
        }

        [Fact]
        public void Box_inside_another_overlaps_with_its_own_volume()
        {
            OrientedBox outer = Box(new Vec3(0, 0, 0), new Vec3(4, 4, 4));
            OrientedBox inner = Box(new Vec3(0.5, -0.5, 1), new Vec3(1, 2, 0.5), 30);

            Assert.True(Overlap.Of(outer, inner, out Vec3 centroid, out double volume));
            Assert.Equal(1, volume, 1e-9);
            Assert.Equal(0, (centroid - inner.Center).Length, 1e-9);
        }
    }
}
