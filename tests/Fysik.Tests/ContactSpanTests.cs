using System.Collections.Generic;
using System.Linq;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class ContactSpanTests
    {
        private static readonly Vec3 Gravity = new Vec3(0, -9.81, 0);

        private readonly ITestOutputHelper _out;

        public ContactSpanTests(ITestOutputHelper output) => _out = output;

        private static double[] Span(double x0, double x1, double y0, double y1, double z0, double z1) =>
            new[] { x0, x1, y0, y1, z0, z1 };

        [Theory]
        [InlineData("111111111", 1)]
        [InlineData("101101101", 2)]
        [InlineData("100010001", 1)]
        [InlineData("000000000", 0)]
        [InlineData("100000001", 2)]
        public void Ground_cells_join_into_patches(string cells, int patches)
        {
            bool[] hit = cells.Select(c => c == '1').ToArray();
            var label = new int[9];
            Assert.Equal(patches, ContactSpan.Patches(hit, label));
            for (int k = 0; k < 9; k++)
                Assert.Equal(hit[k], label[k] >= 0);
        }

        [Fact]
        public void A_span_shrinks_by_the_margin_and_stays_on_the_piece()
        {
            var frame = new OrientedBox
            {
                Center = new Vec3(0, 1, 0), AxisX = Vec3.UnitX, AxisY = Vec3.UnitY, AxisZ = Vec3.UnitZ,
                Half = new Vec3(2, 1, 0.5),
            };
            double[] open = ContactSpan.Open();
            Assert.Null(ContactSpan.Close(open, frame, 0.15));

            ContactSpan.Include(open, frame, new List<Vec3> { new Vec3(-2.15, 1.85, -0.65), new Vec3(2.15, 2.15, 0.65) });
            double[] span = ContactSpan.Close(open, frame, 0.15);
            Assert.Equal(new[] { -2.0, 2.0, 1.0, 1.0, -0.5, 0.5 }, span.Select(v => System.Math.Round(v, 9)).ToArray());
        }

        [Fact]
        public void A_load_spread_over_a_bearing_lowers_the_peak_moment_as_beam_theory_says()
        {
            Body beam = Structures.Box(new Vec3(0, 1, 0), new Vec3(4, 0.2, 0.2), Structures.Wood);
            double load = 10000, bearing = 1;
            double weight = beam.Mass * 9.81;
            double reaction = (load + weight) / 2;
            var contacts = new List<ContactLoad>
            {
                new ContactLoad
                {
                    Point = new Vec3(0, 1.1, 0), Force = new Vec3(0, -load, 0),
                    Span = Span(-bearing / 2, bearing / 2, 0.1, 0.1, -0.1, 0.1),
                },
                new ContactLoad { Point = new Vec3(-2, 1, 0), Force = new Vec3(0, reaction, 0), Ground = true },
                new ContactLoad { Point = new Vec3(2, 1, 0), Force = new Vec3(0, reaction, 0), Ground = true },
            };

            BodyResult r = Mechanics.Evaluate(beam, contacts, Gravity);
            double expected = load * 4 / 4 - load * bearing / 8 + weight * 4 / 8;
            _out.WriteLine($"{r.BendingMoment:0} N·m, expected {expected:0}");
            Assert.Equal(expected, r.BendingMoment, 0.01 * expected);
        }

        [Fact]
        public void A_block_pressed_off_centre_inside_its_middle_third_is_only_squeezed()
        {
            Body block = Structures.Box(new Vec3(0, 0.5, 0), new Vec3(2, 1, 1), Structures.Stone);
            double load = 2e6, offset = 0.25;
            double weight = block.Mass * 9.81;
            var top = new ContactLoad
            {
                Point = new Vec3(0, 1, 0), Force = new Vec3(0, -load, 0), Moment = new Vec3(0, 0, -offset * load),
                Span = Span(-1, 1, 0.5, 0.5, -0.5, 0.5),
            };
            var bottom = new ContactLoad
            {
                Point = new Vec3(0, 0, 0), Force = new Vec3(0, load + weight, 0), Moment = new Vec3(0, 0, offset * load),
                Span = Span(-1, 1, -0.5, -0.5, -0.5, 0.5), Ground = true,
            };

            BodyResult spread = Mechanics.Evaluate(block, new List<ContactLoad> { top, bottom }, Gravity);
            bottom.Span = null;
            BodyResult pointBelow = Mechanics.Evaluate(block, new List<ContactLoad> { top, bottom }, Gravity);
            _out.WriteLine($"spread {spread.Mode} {spread.Utilization:P1}, point below {pointBelow.Mode} {pointBelow.Utilization:P1}");
            double squeeze = load / 2 * (1 + 6 * offset / 2) / Structures.Stone.CompressiveStrength;
            Assert.Equal(StressMode.Compression, spread.Mode);
            Assert.Equal(squeeze, spread.Utilization, 0.05 * squeeze);
            Assert.True(pointBelow.Utilization > 3 * spread.Utilization, $"point below {pointBelow.Utilization:P1}");
        }

        [Fact]
        public void A_stone_block_pushed_sideways_bears_on_the_ground_under_its_toe()
        {
            Body block = Structures.Box(new Vec3(0, 0.5, 0), new Vec3(1, 1, 2), Structures.Stone);
            var above = new ContactLoad
            {
                Point = new Vec3(0, 1, 0), Force = new Vec3(0, -200000, 0), Span = Span(-0.5, 0.5, 0.5, 0.5, -1, 1),
            };
            var push = new ContactLoad { Point = new Vec3(0.5, 0.75, 0), Force = new Vec3(-60000, -40000, 0) };
            var groundPoint = new Vec3(0, 0, 0);
            Vec3 groundForce = -above.Force - push.Force - Gravity * block.Mass;
            Vec3 groundMoment = -(Vec3.Cross(push.Point - block.Center, push.Force) +
                                  Vec3.Cross(groundPoint - block.Center, groundForce));
            var ground = new ContactLoad
            {
                Point = groundPoint, Force = groundForce, Moment = groundMoment, Ground = true,
                Span = Span(-0.5, 0.5, -0.5, -0.5, -1, 1),
            };
            double eccentricity = groundMoment.Z / groundForce.Y;

            BodyResult patch = Mechanics.Evaluate(block, new List<ContactLoad> { above, push, ground }, Gravity);
            ground.Span = null;
            BodyResult point = Mechanics.Evaluate(block, new List<ContactLoad> { above, push, ground }, Gravity);
            _out.WriteLine($"eccentricity {eccentricity:0.000} m; patch {patch.Mode} {patch.Utilization:P1}, point {point.Mode} {point.Utilization:P1}");
            Assert.True(System.Math.Abs(eccentricity) < 1.0 / 6);
            Assert.True(patch.Utilization < 0.6 * point.Utilization);
        }

        [Fact]
        public void A_short_contact_keeps_its_moment_where_it_is()
        {
            Body pole = Structures.Box(new Vec3(0, 1, 0), new Vec3(0.4, 2, 0.4), Structures.Wood);
            var top = new ContactLoad
            {
                Point = new Vec3(0, 2, 0), Force = new Vec3(3000, -20000, 0), Moment = new Vec3(0, 0, 5000),
                Span = Span(-0.2, 0.2, 0.99, 1.0, -0.2, 0.2),
            };
            Vec3 weight = Gravity * pole.Mass;
            Vec3 baseForce = -top.Force - weight;
            Vec3 baseMoment = -(top.Moment + Vec3.Cross(top.Point - new Vec3(0, 0, 0), top.Force) +
                                Vec3.Cross(pole.Center - new Vec3(0, 0, 0), weight));
            var bottom = new ContactLoad { Point = new Vec3(0, 0, 0), Force = baseForce, Moment = baseMoment, Ground = true };

            BodyResult spread = Mechanics.Evaluate(pole, new List<ContactLoad> { top, bottom }, Gravity);
            top.Span = null;
            BodyResult point = Mechanics.Evaluate(pole, new List<ContactLoad> { top, bottom }, Gravity);
            _out.WriteLine($"spread {spread.Mode} {spread.Utilization:P1}, point {point.Mode} {point.Utilization:P1}");
            Assert.Equal(point.Utilization, spread.Utilization, 0.05 * point.Utilization);
        }

        [Fact]
        public void A_stack_of_stone_walls_resting_face_to_face_stays_in_compression()
        {
            var model = new StructureModel();
            int walls = 50;
            for (int k = 0; k < walls; k++)
                model.AddBody(Structures.Box(new Vec3(0, 1 + 2 * k, 0), new Vec3(4, 2, 1), Structures.Stone));
            model.Ground(0, new Vec3(0, 0, 0), Span(-2, 2, -1, -1, -0.5, 0.5));
            for (int k = 0; k + 1 < walls; k++)
                model.Connect(k, k + 1, new Vec3(0, 2 * (k + 1), 0), Span(-2, 2, 1, 1, -0.5, 0.5), Span(-2, 2, -1, -1, -0.5, 0.5));

            var analysis = new StructureAnalysis(model, 500);
            while (!analysis.Step(32))
            {
            }
            BodyResult worst = analysis.Results.OrderByDescending(r => r.Utilization).First();
            _out.WriteLine($"{walls * 2} m: {worst.Mode} {worst.Utilization:P1}");
            Assert.Equal(StressMode.Compression, worst.Mode);
            Assert.True(worst.Utilization < 0.15, $"worst {worst.Utilization:P1}");
        }
    }
}
