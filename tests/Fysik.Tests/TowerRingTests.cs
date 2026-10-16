using System;
using System.Linq;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class TowerRingTests
    {
        private const double Step = 22.5;
        private readonly ITestOutputHelper _out;

        public TowerRingTests(ITestOutputHelper output) => _out = output;

        private struct Pose
        {
            public Vec3 Origin;
            public double Yaw;

            public Vec3 Apply(Vec3 local) => Origin + TowerRing.Turn(local, Yaw);

            public Pose Then(TowerRing placed, TowerRing ghost, int side, bool above, bool flush)
            {
                double start = placed.Next(ghost, side, above, flush);
                return new Pose { Origin = Apply(placed.Origin(ghost, start, above)), Yaw = Yaw + start };
            }
        }

        public static TheoryData<double, double> Rings => new TheoryData<double, double>
        {
            { 2, 1 },
            { 2, 0.5 },
            { 3, 1 },
            { 3, 0.5 },
            { 4, 1 },
            { 4, 0.5 },
            { 5, 1 },
            { 5, 0.5 },
        };

        private static void Near(Vec3 expected, Vec3 actual, string what)
        {
            Assert.True((expected - actual).Length < 1e-9, $"{what}: expected {expected}, got {actual}");
        }

        [Theory]
        [MemberData(nameof(Rings))]
        public void Sixteen_stones_laid_one_after_the_other_close_the_ring(double outer, double thickness)
        {
            var ring = new TowerRing(outer, thickness, Step);
            var first = new Pose { Origin = new Vec3(4, 0.5, -7), Yaw = 45 };
            Vec3 centre = first.Apply(ring.Centre);
            Pose pose = first;
            for (int i = 0; i < 16; i++)
            {
                Pose next = pose.Then(ring, ring, 1, false, false);
                Near(pose.Apply(ring.Point(ring.Angle, ring.InnerRadius, 0.5)), next.Apply(ring.Point(0, ring.InnerRadius, 0.5)), "inner corner");
                Near(pose.Apply(ring.Point(ring.Angle, ring.OuterRadius, -0.5)), next.Apply(ring.Point(0, ring.OuterRadius, -0.5)), "outer corner");
                Near(centre, next.Apply(ring.Centre), "tower centre");
                pose = next;
            }
            Near(first.Origin, pose.Origin, "back at the first stone");
            Assert.Equal(first.Yaw + 360, pose.Yaw, 9);
        }

        [Theory]
        [MemberData(nameof(Rings))]
        public void The_ends_of_a_quarter_ring_meet_straight_walls_on_the_grid(double outer, double thickness)
        {
            var ring = new TowerRing(outer, thickness, Step);
            var start = new Pose { Origin = TowerRing.Turn(-ring.Centre, 0) };
            Near(new Vec3(0, -0.5, ring.InnerRadius), start.Apply(ring.Point(0, ring.InnerRadius, -0.5)), "start, inside");
            Near(new Vec3(0, 0.5, outer), start.Apply(ring.Point(0, outer, 0.5)), "start, outside");
            Pose pose = start;
            for (int i = 0; i < 3; i++)
                pose = pose.Then(ring, ring, 1, false, false);
            Near(new Vec3(ring.InnerRadius, -0.5, 0), pose.Apply(ring.Point(ring.Angle, ring.InnerRadius, -0.5)), "end, inside");
            Near(new Vec3(outer, 0.5, 0), pose.Apply(ring.Point(ring.Angle, outer, 0.5)), "end, outside");
            Assert.Equal(0, ring.InnerRadius * 2 % 1, 9);
        }

        [Fact]
        public void A_stone_laid_on_top_straddles_the_joint_below_and_a_half_stone_ends_flush()
        {
            var ring = new TowerRing(3, 1, Step);
            var half = new TowerRing(3, 1, Step / 2);
            Assert.Equal(Step / 2, ring.Next(ring, 1, true, false), 9);
            Assert.Equal(-Step / 2, ring.Next(ring, -1, true, false), 9);
            Assert.Equal(Step / 2, ring.Next(half, 1, true, true), 9);
            Assert.Equal(0, ring.Next(half, -1, true, true), 9);
            Assert.Equal(-Step / 2, ring.Next(half, -1, false, true), 9);

            var below = new Pose { Origin = Vec3.Zero };
            Pose above = below.Then(ring, half, 1, true, true);
            Near(below.Apply(ring.Point(ring.Angle, 3, 0.5)), above.Apply(half.Point(half.Angle, 3, -0.5)), "flush end");
        }

        [Fact]
        public void A_thin_course_on_a_thick_one_keeps_the_outside_flush()
        {
            var thick = new TowerRing(3, 1, Step);
            var thin = new TowerRing(3, 0.5, Step);
            var below = new Pose { Origin = new Vec3(1, 0.5, 2), Yaw = 90 };
            Pose above = below.Then(thick, thin, -1, true, false);
            Near(below.Apply(thick.Centre) + new Vec3(0, 1, 0), above.Apply(thin.Centre), "same centre, one course up");
            Vec3 corner = above.Apply(thin.Point(0, 3, -0.25)) - below.Apply(thick.Centre);
            Assert.Equal(3, Math.Sqrt(corner.X * corner.X + corner.Z * corner.Z), 9);
        }

        [Theory]
        [MemberData(nameof(Rings))]
        public void The_box_weighs_what_the_curved_stone_does(double outer, double thickness)
        {
            var ring = new TowerRing(outer, thickness, Step);
            double exact = Step * Math.PI / 180 / 2 * (outer * outer - ring.InnerRadius * ring.InnerRadius) * ring.Height;
            Assert.Equal(exact, ring.Volume, 9);
        }

        [Fact]
        public void The_side_comes_from_where_the_stone_is_hit()
        {
            var ring = new TowerRing(3, 1, Step);
            Assert.Equal(1, ring.SideOf(ring.Point(20, 2.5, 0)));
            Assert.Equal(-1, ring.SideOf(ring.Point(3, 2.9, 0.4)));
            Assert.Equal(Step / 2, ring.AngleOf(Vec3.Zero), 9);
        }

        private static void Ring(Blocks blocks, TowerRing ring, double start, double course)
        {
            Vec3 centre = TowerRing.Turn(-ring.Centre, start) + new Vec3(0, course + ring.Height / 2, 0);
            blocks.AddYawed(centre, new Vec3(ring.MeanWidth, ring.Height, ring.Thickness), start + ring.BoxTurn);
        }

        private BodyResult[] Run(Blocks blocks, string title)
        {
            StructureModel model = blocks.Build();
            BodyResult[] results = Blocks.Solve(model, out StructureAnalysis analysis);
            _out.WriteLine($"{title}: {results.Length} stones, {analysis.MasonryRounds} rounds, {analysis.Iterations} iterations, " +
                           $"worst {results.Max(r => r.Utilization):P0}");
            foreach (int i in Enumerable.Range(0, results.Length).OrderByDescending(i => results[i].Utilization).Take(5))
                _out.WriteLine($"  #{i} at {model.Bodies[i].Center}: {results[i].Mode} {results[i].Utilization:P0}");
            Assert.True(analysis.MasonrySettled, $"{title}: masonry did not settle");
            return results;
        }

        [Theory]
        [InlineData(2, 1, true, 0.75)]
        [InlineData(2, 0.5, true, 0.5)]
        [InlineData(3, 1, true, 0.5)]
        [InlineData(3, 1, false, 0)]
        [InlineData(3, 0.5, true, 0.5)]
        [InlineData(3, 0.5, false, 0)]
        [InlineData(4, 1, true, 0.5)]
        [InlineData(4, 0.5, true, 0.5)]
        [InlineData(5, 1, true, 0.5)]
        [InlineData(5, 0.5, true, 0.5)]
        public void A_round_tower_stands_in_compression(double outer, double thickness, bool running, double limit)
        {
            var ring = new TowerRing(outer, thickness, Step);
            var blocks = new Blocks();
            for (int course = 0; course < 8; course++)
                for (int i = 0; i < 16; i++)
                    Ring(blocks, ring, i * Step + (running && course % 2 == 1 ? Step / 2 : 0), course);
            BodyResult[] r = Run(blocks, $"tower {2 * outer} m, {thickness} m thick, {(running ? "running" : "stack")} bond");
            Assert.DoesNotContain(r, x => x.Mode == StressMode.Unstable);
            if (limit > 0)
                Assert.True(r.Max(x => x.Utilization) < limit, $"worst {r.Max(x => x.Utilization):P0}");
        }

        [Fact]
        public void A_rounded_corner_joins_two_straight_walls()
        {
            var ring = new TowerRing(3, 1, Step);
            var half = new TowerRing(3, 1, Step / 2);
            var blocks = new Blocks();
            for (int course = 0; course < 4; course++)
            {
                double y = course + 0.5;
                bool offset = course % 2 == 1;
                if (offset)
                {
                    Ring(blocks, half, 0, course);
                    for (int i = 0; i < 3; i++)
                        Ring(blocks, ring, Step / 2 + i * Step, course);
                    Ring(blocks, half, 90 - Step / 2, course);
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                        Ring(blocks, ring, i * Step, course);
                }
                double x = offset ? 1 : 0;
                if (offset)
                {
                    blocks.Add(new Vec3(-0.5, y, 2.5), new Vec3(1, 1, 1));
                    blocks.Add(new Vec3(2.5, y, -0.5), new Vec3(1, 1, 1));
                }
                for (; x + 2 <= 6; x += 2)
                {
                    blocks.Add(new Vec3(-x - 1, y, 2.5), new Vec3(2, 1, 1));
                    blocks.Add(new Vec3(2.5, y, -x - 1), new Vec3(1, 1, 2));
                }
            }
            BodyResult[] r = Run(blocks, "rounded corner between two walls");
            Assert.DoesNotContain(r, x => x.Mode == StressMode.Unstable);
            Assert.True(r.Max(x => x.Utilization) < 0.7, $"worst {r.Max(x => x.Utilization):P0}");
        }
    }
}
