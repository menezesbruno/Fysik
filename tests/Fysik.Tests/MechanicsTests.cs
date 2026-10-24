using System;
using System.Collections.Generic;
using System.Linq;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class MechanicsTests
    {
        private readonly ITestOutputHelper _out;

        public MechanicsTests(ITestOutputHelper output) => _out = output;

        [Theory]
        [InlineData(1e-12, -1e-12)]
        [InlineData(-1e-12, 1e-12)]
        [InlineData(1e-12, 1e-12)]
        [InlineData(0, 0)]
        public void Loads_meeting_at_the_centre_of_a_piece_do_not_shear_it(double topNoise, double bottomNoise)
        {
            Body wall = Structures.Box(new Vec3(0, 1, 0), new Vec3(4, 2, 1), Structures.Stone);
            double load = 2e6;
            double weight = wall.Mass * 9.81;
            var contacts = new List<ContactLoad>
            {
                new ContactLoad { Point = new Vec3(topNoise, 2, -topNoise), Force = new Vec3(0, -load, 0) },
                new ContactLoad { Point = new Vec3(bottomNoise, 0, bottomNoise), Force = new Vec3(0, load + weight, 0), Ground = true },
            };

            BodyResult r = Mechanics.Evaluate(wall, contacts, new Vec3(0, -9.81, 0));
            _out.WriteLine($"{r.Mode} {r.Utilization:P1}");
            Assert.NotEqual(StressMode.Shear, r.Mode);
            Assert.True(r.Utilization < 0.15, $"utilization {r.Utilization:P1}");
        }

        [Fact]
        public void A_load_at_the_middle_of_a_beam_still_bends_it_there()
        {
            Body beam = Structures.Box(new Vec3(0, 1, 0), new Vec3(4, 0.2, 0.2), Structures.Wood);
            double load = 1000;
            double weight = beam.Mass * 9.81;
            double reaction = (load + weight) / 2;
            var contacts = new List<ContactLoad>
            {
                new ContactLoad { Point = new Vec3(1e-12, 1.1, 0), Force = new Vec3(0, -load, 0) },
                new ContactLoad { Point = new Vec3(-2, 1, 0), Force = new Vec3(0, reaction, 0), Ground = true },
                new ContactLoad { Point = new Vec3(2, 1, 0), Force = new Vec3(0, reaction, 0), Ground = true },
            };

            BodyResult r = Mechanics.Evaluate(beam, contacts, new Vec3(0, -9.81, 0));
            double moment = load * 4 / 4 + weight * 4 / 8;
            Assert.Equal(StressMode.Bending, r.Mode);
            Assert.Equal(moment, r.BendingMoment, 0.02 * moment);
        }

        [Theory]
        [InlineData(1.98)]
        [InlineData(2.0)]
        [InlineData(2.02)]
        public void A_wall_buckles_under_the_weight_it_carries_whichever_of_its_sides_is_longer(double height)
        {
            Body wall = Structures.Box(new Vec3(0, height / 2, 0), new Vec3(2, height, 0.26), Structures.Wood);
            wall.Mass += 3000;
            double weight = wall.Mass * 9.81;
            var contacts = new List<ContactLoad>
            {
                new ContactLoad
                {
                    Point = Vec3.Zero, Force = new Vec3(0, weight, 0), Ground = true,
                    Span = new[] { -1, 1, -height / 2, -height / 2, -0.13, 0.13 },
                },
            };

            BodyResult r = Mechanics.Evaluate(wall, contacts, new Vec3(0, -9.81, 0));
            double ratio = height / (0.26 / Math.Sqrt(12)) / Structures.Wood.BucklingSlenderness;
            double expected = weight / (2 * 0.26) * (1 + ratio * ratio) / Structures.Wood.CompressiveStrength;
            _out.WriteLine($"{height} m: {r.Mode} {r.Utilization:P2}, expected {expected:P2}");
            Assert.Equal(StressMode.Compression, r.Mode);
            Assert.Equal(expected, r.Utilization, 0.01 * expected);
        }

        [Fact]
        public void A_tall_stack_of_stone_walls_on_the_ground_stays_in_compression()
        {
            var model = new StructureModel();
            int walls = 16;
            for (int k = 0; k < walls; k++)
                model.AddBody(Structures.Box(new Vec3(0, 1 + 2 * k, 0), new Vec3(4, 2, 1), Structures.Stone));
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    model.Ground(0, new Vec3(1.8 * i, 0, 0.45 * j));
            for (int k = 0; k + 1 < walls; k++)
                model.Connect(k, k + 1, new Vec3(k % 2 == 0 ? 1e-9 : -1e-9, 2 * (k + 1), 1e-9));

            var analysis = new StructureAnalysis(model, 500);
            while (!analysis.Step(32))
            {
            }
            BodyResult worst = analysis.Results.OrderByDescending(r => r.Utilization).First();
            _out.WriteLine($"{walls * 2} m: {worst.Mode} {worst.Utilization:P1}");
            Assert.True(worst.Utilization < 0.5, $"worst {worst.Mode} {worst.Utilization:P1}");
        }
    }
}
