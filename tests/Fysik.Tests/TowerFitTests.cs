using System;
using System.Collections.Generic;
using System.Linq;
using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class TowerFitTests
    {
        private const double Step = 22.5;
        private static readonly TowerRing Ring = new TowerRing(3, 1, Step);

        [Fact]
        public void A_point_is_inside_a_tower_stone_only_within_its_ring_sector()
        {
            Assert.True(Ring.Contains(Vec3.Zero, 0.001));
            Assert.True(Ring.Contains(Ring.Point(20, 2.2, 0.4), 0.001));
            Assert.False(Ring.Contains(Ring.Point(25, 2.5, 0), 0.001));
            Assert.False(Ring.Contains(Ring.Point(-2, 2.5, 0), 0.001));
            Assert.False(Ring.Contains(Ring.Point(10, 1.9, 0), 0.001));
            Assert.False(Ring.Contains(Ring.Point(10, 3.1, 0), 0.001));
            Assert.False(Ring.Contains(Ring.Point(10, 2.5, 0.51), 0.001));
            Assert.True(Ring.Contains(Ring.Point(10, 2.5, 0.505), 0.001, openDepth: true, depthGrow: 0.01));
        }

        [Theory]
        [InlineData(5, 2.2, true)]
        [InlineData(18, 2.9, false)]
        public void A_point_pushed_out_of_a_tower_stone_lands_on_its_face_along_the_radius(double angle, double radius, bool outward)
        {
            Vec3 p = Ring.Point(angle, radius, 0.3);
            Vec3 q = Ring.ToFace(p, outward, 0);
            Assert.Equal(outward ? Ring.OuterRadius : Ring.InnerRadius, Ring.RadiusOf(q), 9);
            Assert.Equal(angle, Ring.AngleOf(q), 9);
            Assert.Equal(p.Y, q.Y, 9);
        }

        private sealed class Tower
        {
            private const double Margin = 0.002, Gap = 0.005, Curved = 0.1, Inside = 4;
            private readonly double _outerGrow, _innerGrow, _depthGrow;
            private readonly List<TowerRing> _rings = new List<TowerRing>();
            private readonly List<Vec3> _origins = new List<Vec3>();
            private readonly List<double> _yaws = new List<double>();
            private readonly List<bool> _outward = new List<bool>(), _open = new List<bool>(), _deep = new List<bool>();
            private readonly List<OrientedBox> _blocks = new List<OrientedBox>();
            private readonly List<Vec3> _reachMin = new List<Vec3>(), _reachMax = new List<Vec3>();
            public List<Vec3> Carved = new List<Vec3>();

            public Tower(double outerGrow = 0, double innerGrow = 0, double depthGrow = 0)
            {
                _outerGrow = outerGrow;
                _innerGrow = innerGrow;
                _depthGrow = depthGrow;
            }

            public Tower Lay(TowerRing ring, double start, int course)
            {
                _rings.Add(ring);
                _origins.Add(TowerRing.Turn(-ring.Centre, start) + new Vec3(0, course + ring.Height / 2, 0));
                _yaws.Add(start);
                int k = _rings.Count - 1;
                double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
                double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
                for (int a = 0; a <= 4; a++)
                    foreach (double radius in new[] { ring.InnerRadius, ring.OuterRadius })
                        foreach (double y in new[] { -ring.Height / 2, ring.Height / 2 })
                        {
                            Vec3 w = ToWorld(k, ring.Point(ring.Angle * a / 4, radius, y));
                            x0 = Math.Min(x0, w.X);
                            y0 = Math.Min(y0, w.Y);
                            z0 = Math.Min(z0, w.Z);
                            x1 = Math.Max(x1, w.X);
                            y1 = Math.Max(y1, w.Y);
                            z1 = Math.Max(z1, w.Z);
                        }
                _reachMin.Add(new Vec3(x0 - Reach, y0 - Reach, z0 - Reach));
                _reachMax.Add(new Vec3(x1 + Reach, y1 + Reach, z1 + Reach));
                return this;
            }

            private const double Reach = 0.15;

            private bool Reaches(Vec3 a, Vec3 b, Vec3 c)
            {
                for (int k = 0; k < _rings.Count; k++)
                {
                    bool apart = false;
                    for (int i = 0; i < 3 && !apart; i++)
                        apart = Math.Max(a[i], Math.Max(b[i], c[i])) < _reachMin[k][i] ||
                                Math.Min(a[i], Math.Min(b[i], c[i])) > _reachMax[k][i];
                    if (!apart)
                        return true;
                }
                return false;
            }

            public Tower Course(int course, double offset = 0, int stones = 16, TowerRing? ring = null)
            {
                for (int i = 0; i < stones; i++)
                    Lay(ring ?? Ring, offset + i * Step, course);
                return this;
            }

            private Vec3 ToLocal(int k, Vec3 p) => TowerRing.Turn(p - _origins[k], -_yaws[k]);

            private Vec3 ToWorld(int k, Vec3 p) => _origins[k] + TowerRing.Turn(p, _yaws[k]);

            private int WedgeOf(Vec3 p)
            {
                for (int k = 0; k < _rings.Count; k++)
                    if (_rings[k].Claims(ToLocal(k, p), _outward[k], Margin, _open[k], _deep[k], _outerGrow, _innerGrow, _depthGrow))
                        return k;
                return -1;
            }

            public bool InsideEnvelope(Vec3 p)
            {
                for (int k = 0; k < _rings.Count; k++)
                    if (_rings[k].Contains(ToLocal(k, p), Margin, false, _deep[k], _outerGrow - 0.003, _innerGrow - 0.003, _depthGrow - 0.003))
                        return true;
                return false;
            }

            public (double volume, bool moved) Carve(Vec3 min, Vec3 max)
            {
                Vec3 centre = (min + max) * 0.5;
                var corners = new List<Vec3>();
                for (int q = 0; q < 8; q++)
                    corners.Add(new Vec3((q & 1) == 0 ? min.X : max.X, (q & 2) == 0 ? min.Y : max.Y, (q & 4) == 0 ? min.Z : max.Z));
                _outward.Clear();
                _open.Clear();
                _deep.Clear();
                _blocks.Clear();
                for (int k = 0; k < _rings.Count; k++)
                {
                    Vec3 local = ToLocal(k, centre);
                    List<Vec3> locals = corners.Select(c => ToLocal(k, c)).ToList();
                    _outward.Add(_rings[k].Beyond(local));
                    _open.Add(_rings[k].Opens(local, locals));
                    _deep.Add(_rings[k].Reaches(locals));
                    _blocks.Add(new OrientedBox
                    {
                        Center = local, AxisX = TowerRing.Turn(Vec3.UnitX, -_yaws[k]), AxisY = Vec3.UnitY,
                        AxisZ = TowerRing.Turn(Vec3.UnitZ, -_yaws[k]), Half = (max - min) * 0.5,
                    });
                }
                var carver = new MeshCarver(corners, WedgeOf, 4, w => Curved, Inside, Reaches);
                List<int> triangles = carver.Split(Box);
                bool moved = carver.Push((p, k) =>
                    ToWorld(k, _rings[k].ToFaceWithin(ToLocal(k, p), _blocks[k], _outward[k], Gap + (_outward[k] ? _outerGrow : _innerGrow),
                                                      _outerGrow > 0 ? 0.15 : 0.02)));
                Carved = carver.Points;
                return (MeshVolume.Of(carver.Points, triangles), moved);
            }
        }

        private static readonly int[] Box =
        {
            0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
        };

        private static double Outside(Vec3 min, Vec3 max, Func<double, bool> keep)
        {
            int kept = 0, samples = 0;
            for (int u = 0; u < 60; u++)
                for (int v = 0; v < 60; v++)
                {
                    double x = min.X + (u + 0.5) / 60 * (max.X - min.X), z = min.Z + (v + 0.5) / 60 * (max.Z - min.Z);
                    samples++;
                    if (keep(Math.Sqrt(x * x + z * z)))
                        kept++;
                }
            return (max.X - min.X) * (max.Y - min.Y) * (max.Z - min.Z) * kept / samples;
        }

        private static void LeavesNothingInside(Tower tower)
        {
            foreach (Vec3 p in tower.Carved)
                Assert.False(tower.InsideEnvelope(p), $"({p.X:0.000}, {p.Y:0.000}, {p.Z:0.000}) is left inside the tower");
        }

        [Theory]
        [InlineData(2.5, -0.5)]
        [InlineData(2.2, 0.3)]
        [InlineData(2.6, 1.2)]
        [InlineData(1.8, 1.9)]
        public void A_curtain_wall_meeting_the_tower_is_cut_flush_with_its_outer_face(double x, double z)
        {
            var tower = new Tower().Course(0);
            var min = new Vec3(x, 0, z);
            var max = new Vec3(x + 2, 1, z + 1);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            double exact = Outside(min, max, r => r > Ring.OuterRadius);
            Assert.True(Math.Abs(volume - exact) < 0.015, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Fact]
        public void A_long_thin_wall_barely_inside_a_tower_is_cut_although_no_corner_reaches_the_stones()
        {
            var ring = new TowerRing(4, 1, Step);
            var tower = new Tower().Course(0, 0, 16, ring).Course(1, Step / 2, 16, ring);
            var min = new Vec3(-0.75, 0, 3.96);
            var max = new Vec3(3.25, 2, 4.46);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            double exact = Outside(min, max, r => r > ring.OuterRadius);
            Assert.True(Math.Abs(volume - exact) < 0.015, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(2, 0.5)]
        [InlineData(4, 1)]
        [InlineData(4, 0.5)]
        [InlineData(5, 1)]
        [InlineData(5, 0.5)]
        public void Walls_are_cut_flush_with_towers_of_every_size(double outer, double thickness)
        {
            var ring = new TowerRing(outer, thickness, Step);
            var tower = new Tower().Course(0, 0, 16, ring).Course(1, Step / 2, 16, ring);
            var min = new Vec3(ring.InnerRadius - 0.3, 0, -0.5);
            var max = new Vec3(ring.InnerRadius + 1.7, 2, 0.5);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            double exact = Outside(min, max, r => r > outer);
            Assert.True(Math.Abs(volume - exact) < 0.03, $"carved {volume:0.000} m³, exact {exact:0.000} m³");

            var inner = new Tower().Course(0, 0, 16, ring);
            var floorMin = new Vec3(ring.InnerRadius - 1.6, 0.25, -1);
            var floorMax = new Vec3(ring.InnerRadius + 0.4, 0.75, 1);
            (double floor, bool cut) = inner.Carve(floorMin, floorMax);
            Assert.True(cut);
            LeavesNothingInside(inner);
            double inside = Outside(floorMin, floorMax, r => r < ring.InnerRadius);
            Assert.True(Math.Abs(floor - inside) < 0.03, $"floor carved {floor:0.000} m³, exact {inside:0.000} m³");
        }

        [Fact]
        public void A_wall_across_the_joint_between_two_tower_stones_is_cut_without_spikes()
        {
            var tower = new Tower().Course(0);
            var min = new Vec3(-0.5, 0, 2.6);
            var max = new Vec3(0.5, 1, 4.6);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            double exact = Outside(min, max, r => r > Ring.OuterRadius);
            Assert.True(Math.Abs(volume - exact) < 0.015, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Fact]
        public void A_wall_two_courses_high_is_cut_through_both_courses()
        {
            var tower = new Tower().Course(0).Course(1, Step / 2);
            var min = new Vec3(2.4, 0, -0.5);
            var max = new Vec3(4.4, 2, 0.5);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            double exact = Outside(min, max, r => r > Ring.OuterRadius);
            Assert.True(Math.Abs(volume - exact) < 0.03, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Theory]
        [InlineData(2.2, -0.25, 0.5)]
        [InlineData(1.6, -0.25, 0.5)]
        [InlineData(1.9, 0.6, 0.5)]
        public void A_curtain_wall_laid_too_deep_still_ends_flush_with_the_outer_face(double x, double z, double thickness)
        {
            var tower = new Tower().Course(0);
            var min = new Vec3(x, 0, z);
            var max = new Vec3(x + 2, 1, z + thickness);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            Assert.DoesNotContain(tower.Carved, p => Math.Sqrt(p.X * p.X + p.Z * p.Z) < Ring.OuterRadius - 1e-6);
            double exact = Outside(min, max, r => r > Ring.OuterRadius);
            Assert.True(Math.Abs(volume - exact) < 0.015, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Fact]
        public void A_thin_wall_meeting_the_tower_at_a_slant_stays_within_its_own_block()
        {
            var tower = new Tower().Course(0);
            var min = new Vec3(1.9, 0, 1.3);
            var max = new Vec3(2.4, 1, 3.3);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            foreach (Vec3 p in tower.Carved)
            {
                Assert.InRange(p.X, min.X - 0.021, max.X + 0.021);
                Assert.InRange(p.Z, min.Z - 0.021, max.Z + 0.021);
            }
            double exact = Outside(min, max, r => r > Ring.OuterRadius);
            Assert.True(Math.Abs(volume - exact) < 0.03, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Fact]
        public void A_long_thin_wall_four_metres_wide_is_cut_as_cleanly_as_a_short_one()
        {
            var tower = new Tower().Course(0).Course(1, Step / 2);
            var min = new Vec3(2.6, 0, -0.25);
            var max = new Vec3(6.6, 2, 0.25);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            double exact = Outside(min, max, r => r > Ring.OuterRadius);
            Assert.True(Math.Abs(volume - exact) < 0.01, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Fact]
        public void A_wall_inside_the_tower_that_pokes_through_ends_flush_with_the_inner_face()
        {
            var tower = new Tower().Course(0);
            var min = new Vec3(0.5, 0, -0.25);
            var max = new Vec3(3.4, 1, 0.25);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            Assert.DoesNotContain(tower.Carved, p => Math.Sqrt(p.X * p.X + p.Z * p.Z) > Ring.InnerRadius + 1e-6);
            double exact = Outside(min, max, r => r < Ring.InnerRadius);
            Assert.True(Math.Abs(volume - exact) < 0.01, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Fact]
        public void A_wall_inside_the_tower_is_cut_flush_with_its_inner_face()
        {
            var tower = new Tower().Course(0);
            var min = new Vec3(0.5, 0, -0.25);
            var max = new Vec3(2.5, 1, 0.25);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            double exact = Outside(min, max, r => r < Ring.InnerRadius);
            Assert.True(Math.Abs(volume - exact) < 0.01, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Theory]
        [InlineData(2, 0, 4, 2)]
        [InlineData(2, 2, 3, 3)]
        [InlineData(3, -1, 4, 0)]
        public void A_thin_wooden_floor_inside_the_tower_ends_flush_with_the_inner_face(double x0, double z0, double x1, double z1)
        {
            var ring = new TowerRing(4, 0.5, Step);
            var tower = new Tower().Course(0, 0, 16, ring);
            var min = new Vec3(x0, 0.5, z0);
            var max = new Vec3(x1, 0.63, z1);
            (double volume, bool moved) = tower.Carve(min, max);
            Assert.True(moved);
            LeavesNothingInside(tower);
            Assert.DoesNotContain(tower.Carved, p => Math.Sqrt(p.X * p.X + p.Z * p.Z) > ring.InnerRadius + 0.003);
            double exact = Outside(min, max, r => r < ring.InnerRadius);
            Assert.True(Math.Abs(volume - exact) < 0.005, $"carved {volume:0.000} m³, exact {exact:0.000} m³");
        }

        [Fact]
        public void Blocks_that_only_touch_a_tower_are_left_whole()
        {
            var corner = new Tower().Course(0, 0, 4);
            (double butt, bool buttMoved) = corner.Carve(new Vec3(-2, 0, 2), new Vec3(0, 1, 3));
            var tower = new Tower().Course(0);
            (double above, bool aboveMoved) = tower.Carve(new Vec3(-0.5, 1, 2), new Vec3(0.5, 2, 3));
            (double beside, bool besideMoved) = tower.Carve(new Vec3(3, 0, -0.5), new Vec3(4, 1, 0.5));
            Assert.False(buttMoved);
            Assert.False(aboveMoved);
            Assert.False(besideMoved);
            Assert.Equal(2.0, butt, 9);
            Assert.Equal(1.0, above, 9);
            Assert.Equal(1.0, beside, 9);
        }

        [Theory]
        [InlineData(2.3, -0.5)]
        [InlineData(1.5, 2.0)]
        public void A_rough_wall_leaves_nothing_inside_the_tower_stones_rough_surface(double x, double z)
        {
            var tower = new Tower(0.08, 0.08, 0.1).Course(0);
            (double volume, bool moved) = tower.Carve(new Vec3(x, 0, z), new Vec3(x + 2, 1, z + 1));
            Assert.True(moved);
            Assert.True(volume < 2);
            LeavesNothingInside(tower);
        }
    }
}
