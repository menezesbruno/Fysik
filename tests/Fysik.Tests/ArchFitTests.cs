using System;
using System.Collections.Generic;
using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class ArchFitTests
    {
        private static readonly Voussoir Stone = Voussoir.HalfCircle(15, 4, 5);

        [Fact]
        public void A_point_is_inside_an_arch_stone_only_within_its_wedge()
        {
            Assert.True(Stone.Contains(new Vec3(0, 0, 0), 1, 0.001));
            Assert.True(Stone.Contains(new Vec3(0.45, 0.4, 0.3), 1, 0.001));
            Assert.False(Stone.Contains(new Vec3(0.45, -0.4, 0), 1, 0.001));
            Assert.False(Stone.Contains(new Vec3(0, 0, 0.6), 1, 0.001));
            Assert.False(Stone.Contains(new Vec3(0, Stone.Height / 2 + 0.01, 0), 1, 0.001));
        }

        [Theory]
        [InlineData(0.3, 0.2, true)]
        [InlineData(-0.35, 0.45, true)]
        [InlineData(0.2, -0.3, false)]
        [InlineData(-0.38, -0.45, false)]
        public void A_point_pushed_out_of_an_arch_stone_lands_on_its_own_face_along_the_radius(double x, double y, bool outward)
        {
            var p = new Vec3(x, y, 0.2);
            Assert.True(Stone.Contains(p, 1, 0.001));
            Vec3 q = Stone.ToFace(p, outward, 0);
            double face = outward ? Stone.Height / 2 : -Stone.Height / 2;
            Assert.Equal(face, q.Y, 9);
            Assert.Equal(p.Z, q.Z, 9);
            double half = (outward ? Stone.Width : Stone.InnerWidth) / 2;
            Assert.InRange(q.X, -half - 1e-9, half + 1e-9);
            var centre = new Vec3(0, Stone.Centre, 0);
            Vec3 a = p - centre, b = q - centre;
            Assert.Equal(0, a.X * b.Y - a.Y * b.X, 9);
        }

        [Fact]
        public void A_block_beyond_the_middle_of_the_ring_is_pushed_out()
        {
            Assert.True(Stone.Beyond(new Vec3(0, 0.1, 0)));
            Assert.False(Stone.Beyond(new Vec3(0, -0.1, 0)));
        }

        private sealed class Arch
        {
            private const double Margin = 0.002, Gap = 0.005;
            private readonly double _outerGrow, _depthGrow;
            private readonly List<Vec3> _centres = new List<Vec3>();
            private readonly List<double> _turns = new List<double>();
            private readonly List<bool> _outward = new List<bool>(), _open = new List<bool>(), _deep = new List<bool>();

            public Arch(double outerGrow = 0, double depthGrow = 0)
            {
                _outerGrow = outerGrow;
                _depthGrow = depthGrow;
                double r = Stone.OuterRadius - Stone.Height / 2;
                for (int k = 0; k < Stone.Stones; k++)
                {
                    double phi = 90 - Stone.Angle / 2 - k * Stone.Angle, a = phi * Math.PI / 180;
                    _centres.Add(new Vec3(-r * Math.Sin(a), r * Math.Cos(a), 0));
                    _turns.Add(phi);
                }
            }

            private Vec3 ToLocal(int k, Vec3 p)
            {
                double a = -_turns[k] * Math.PI / 180;
                Vec3 d = p - _centres[k];
                return new Vec3(d.X * Math.Cos(a) - d.Y * Math.Sin(a), d.X * Math.Sin(a) + d.Y * Math.Cos(a), d.Z);
            }

            private Vec3 ToWorld(int k, Vec3 p)
            {
                double a = _turns[k] * Math.PI / 180;
                return _centres[k] + new Vec3(p.X * Math.Cos(a) - p.Y * Math.Sin(a), p.X * Math.Sin(a) + p.Y * Math.Cos(a), p.Z);
            }

            private int WedgeOf(Vec3 p)
            {
                for (int k = 0; k < _centres.Count; k++)
                    if (Stone.Contains(ToLocal(k, p), 1, Margin, _open[k], _deep[k], _outerGrow, 0, _depthGrow))
                        return k;
                return -1;
            }

            public bool InsideEnvelope(Vec3 p)
            {
                for (int k = 0; k < _centres.Count; k++)
                    if (Stone.Contains(ToLocal(k, p), 1, Margin, false, _deep[k], _outerGrow - 0.003, 0, _depthGrow - 0.003))
                        return true;
                return false;
            }

            public List<Vec3> Carved = new List<Vec3>();

            public (double volume, bool moved) Carve(Vec3 min, Vec3 max, bool clamp = false)
            {
                Vec3 centre = (min + max) * 0.5;
                _outward.Clear();
                _open.Clear();
                _deep.Clear();
                for (int k = 0; k < _centres.Count; k++)
                {
                    Vec3 local = ToLocal(k, centre);
                    _outward.Add(Stone.Beyond(local));
                    _open.Add(Stone.InSector(local));
                    _deep.Add(Voussoir.InDepth(local, 1));
                }
                var corners = new List<Vec3>();
                for (int q = 0; q < 8; q++)
                    corners.Add(new Vec3((q & 1) == 0 ? min.X : max.X, (q & 2) == 0 ? min.Y : max.Y, (q & 4) == 0 ? min.Z : max.Z));
                var carver = new MeshCarver(corners, WedgeOf, 4);
                List<int> triangles = carver.Split(Box);
                bool moved = carver.Push((p, k) => ToWorld(k, Stone.ToFace(ToLocal(k, p), _outward[k], Gap + (_outward[k] ? _outerGrow : 0))));
                if (clamp)
                    carver.Clamp(new OrientedBox
                    {
                        Center = centre, AxisX = Vec3.UnitX, AxisY = Vec3.UnitY, AxisZ = Vec3.UnitZ, Half = (max - min) * 0.5,
                    });
                Carved = carver.Points;
                return (MeshVolume.Of(carver.Points, triangles), moved);
            }
        }

        private static readonly int[] Box =
        {
            0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
        };

        private static bool InsideExtrados(double x, double y)
        {
            if (y < 0)
                return false;
            double step = Math.PI / Stone.Stones;
            for (int k = 0; k < Stone.Stones; k++)
            {
                double a0 = Math.PI - k * step, a1 = a0 - step;
                double x0 = 5 * Math.Cos(a0), y0 = 5 * Math.Sin(a0), x1 = 5 * Math.Cos(a1), y1 = 5 * Math.Sin(a1);
                if ((x1 - x0) * (y - y0) - (y1 - y0) * (x - x0) > 1e-12)
                    return false;
            }
            return true;
        }

        [Fact]
        public void Wall_blocks_around_an_arch_are_cut_along_its_curve()
        {
            var arch = new Arch();
            int cut = 0;
            for (int i = -6; i < 6; i++)
                for (int j = 0; j < 7; j++)
                {
                    int inside = 0, samples = 0;
                    bool opening = false;
                    for (int u = 0; u < 100; u++)
                        for (int v = 0; v < 100; v++)
                        {
                            double x = i + (u + 0.5) / 100, y = j + (v + 0.5) / 100;
                            samples++;
                            if (InsideExtrados(x, y))
                                inside++;
                            opening |= Math.Sqrt(x * x + y * y) < 3.99;
                        }
                    (double volume, bool moved) = arch.Carve(new Vec3(i, j, -0.5), new Vec3(i + 1, j + 1, 0.5));
                    if (inside == 0)
                    {
                        Assert.Equal(1.0, volume, 9);
                        continue;
                    }
                    if (inside == samples || opening)
                        continue;
                    cut++;
                    Assert.True(moved);
                    double exact = 1.0 - (double)inside / samples;
                    double tolerance = j == 0 ? 0.04 : 0.012;
                    Assert.True(Math.Abs(volume - exact) < tolerance, $"block ({i}, {j}): carved {volume:0.000} m³, exact {exact:0.000} m³");
                }
            Assert.Equal(14, cut);
        }

        [Theory]
        [InlineData(-2, 4)]
        [InlineData(-4, 3)]
        [InlineData(-5, 1)]
        [InlineData(0, 4)]
        public void A_rough_block_leaves_nothing_inside_the_arch_stones_rough_surface(int i, int j)
        {
            var arch = new Arch(0.1, 0.12);
            (double volume, bool moved) = arch.Carve(new Vec3(i, j, -0.6), new Vec3(i + 1, j + 1, 0.6));
            Assert.True(moved);
            Assert.True(volume < 1.2);
            foreach (Vec3 p in arch.Carved)
                Assert.False(arch.InsideEnvelope(p), $"({p.X:0.000}, {p.Y:0.000}, {p.Z:0.000}) is left inside the arch");
        }

        [Theory]
        [InlineData(-6, 1, 2)]
        [InlineData(-5, 2, 1)]
        [InlineData(-3, 4, 2)]
        public void A_cut_collider_stays_inside_its_own_block(int i, int j, int width)
        {
            var arch = new Arch();
            var min = new Vec3(i, j, -0.5);
            var max = new Vec3(i + width, j + 1, 0.5);
            arch.Carve(min, max);
            bool spilled = arch.Carved.Exists(p => p.Y > max.Y + 1e-6 || p.X < min.X - 1e-6 || p.X > max.X + 1e-6);
            (double volume, bool moved) = arch.Carve(min, max, true);
            Assert.True(moved);
            Assert.True(spilled || volume < width);
            foreach (Vec3 p in arch.Carved)
            {
                Assert.InRange(p.X, min.X - 1e-9, max.X + 1e-9);
                Assert.InRange(p.Y, min.Y - 1e-9, max.Y + 1e-9);
                Assert.InRange(p.Z, min.Z - 1e-9, max.Z + 1e-9);
            }
        }

        [Fact]
        public void Blocks_that_only_touch_an_arch_are_left_whole()
        {
            var arch = new Arch();
            (double pier, bool pierMoved) = arch.Carve(new Vec3(-5, -1, -0.5), new Vec3(-4, 0, 0.5));
            (double front, bool frontMoved) = arch.Carve(new Vec3(-5, 1, 0.5), new Vec3(-4, 2, 1.5));
            (double above, bool aboveMoved) = arch.Carve(new Vec3(-1, 5, -0.5), new Vec3(0, 6, 0.5));
            Assert.False(pierMoved);
            Assert.False(frontMoved);
            Assert.False(aboveMoved);
            Assert.Equal(1.0, pier, 9);
            Assert.Equal(1.0, front, 9);
            Assert.Equal(1.0, above, 9);
        }

        [Fact]
        public void The_volume_of_a_closed_mesh_is_measured_exactly()
        {
            var cube = new List<Vec3>();
            for (int i = 0; i < 8; i++)
                cube.Add(new Vec3((i & 1) * 2.0, ((i >> 1) & 1) * 1.0, ((i >> 2) & 1) * 1.0));
            var triangles = new List<int>
            {
                0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
            };
            Assert.Equal(2.0, MeshVolume.Of(cube, triangles), 9);
            var tetra = new List<Vec3> { Vec3.Zero, Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ };
            Assert.Equal(1.0 / 6, MeshVolume.Of(tetra, new List<int> { 0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3 }), 9);
        }
    }
}
