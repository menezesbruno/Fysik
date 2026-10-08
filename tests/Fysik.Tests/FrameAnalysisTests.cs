using System;
using System.Diagnostics;
using System.Linq;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class FrameAnalysisTests
    {
        private const double G = 9.81;
        private readonly ITestOutputHelper _out;

        public FrameAnalysisTests(ITestOutputHelper output) => _out = output;

        private static (FrameAnalysis frame, BodyResult[] results) Solve(StructureModel model)
        {
            var frame = new FrameAnalysis(model);
            frame.Solve();
            Assert.True(frame.Converged, $"CG did not converge ({frame.Iterations} iterations, residual {frame.RelativeResidual:E2})");
            return (frame, frame.Evaluate());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        public void Cantilever_root_moment_matches_beam_theory(int beams)
        {
            var (_, results) = Solve(Structures.Cantilever(beams));

            double w = 500 * 0.2 * 0.2 * G;
            double length = 2.0 * beams;
            double expected = w * length * length / 2;
            _out.WriteLine($"{beams} beams: root moment {results[0].BendingMoment:0} N·m (theory {expected:0}), utilization {results[0].Utilization:P0}");

            Assert.Equal(expected, results[0].BendingMoment, expected * 0.01);
            Assert.Equal(StressMode.Bending, results[0].Mode);
        }

        [Fact]
        public void Cantilever_stress_grows_toward_the_root()
        {
            var (_, results) = Solve(Structures.Cantilever(5));
            for (int k = 0; k + 1 < results.Length; k++)
                Assert.True(results[k].Utilization > results[k + 1].Utilization);
        }

        [Fact]
        public void Block_resting_on_its_whole_base_is_barely_stressed()
        {
            Body Block() => Structures.Box(new Vec3(0, 0.5, 0), new Vec3(2, 1, 1), Structures.Stone);

            var corner = new StructureModel();
            corner.AddBody(Block());
            corner.Ground(0, new Vec3(-0.9, 0, -0.45));

            var spread = new StructureModel();
            spread.AddBody(Block());
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    spread.Ground(0, new Vec3(0.9 * i, 0, 0.45 * j));

            double cornerU = Solve(corner).results[0].Utilization;
            double spreadU = Solve(spread).results[0].Utilization;
            _out.WriteLine($"one corner {cornerU:P0}, whole base {spreadU:P1}");
            Assert.True(spreadU < 0.05);
            Assert.True(cornerU > 4 * spreadU);
        }

        [Fact]
        public void Damaged_piece_is_proportionally_weaker()
        {
            StructureModel healthy = Structures.Cantilever(3);
            StructureModel damaged = Structures.Cantilever(3);
            damaged.Bodies[0].StrengthFactor = 0.5;

            double before = Solve(healthy).results[0].Utilization;
            double after = Solve(damaged).results[0].Utilization;
            Assert.Equal(2 * before, after, before * 1e-9);
        }

        [Fact]
        public void Column_base_carries_the_whole_weight_in_compression()
        {
            const int posts = 4;
            var (_, results) = Solve(Structures.Column(posts));

            double weight = posts * 500 * 0.2 * 2 * 0.2 * G;
            _out.WriteLine($"base axial {results[0].AxialForce:0} N, total weight {weight:0} N");
            Assert.Equal(-weight, results[0].AxialForce, weight * 0.01);
            Assert.Equal(StressMode.Compression, results[0].Mode);
            Assert.True(results[0].BendingMoment < 1e-3 * weight);
        }

        [Fact]
        public void Ground_reactions_balance_the_total_weight()
        {
            StructureModel model = Structures.Arch(9, 4);
            var (frame, _) = Solve(model);

            Vec3 reactions = frame.ContactLoads().SelectMany(l => l).Aggregate(Vec3.Zero, (s, c) => s + c.Force);
            double weight = model.Bodies.Sum(b => b.Mass) * G;
            Assert.Equal(weight, reactions.Y, weight * 1e-6);
            Assert.True(Math.Abs(reactions.X) < weight * 1e-6);
        }

        [Fact]
        public void Beam_fixed_at_both_ends_is_far_less_stressed_than_a_cantilever_of_the_same_span()
        {
            double cantilever = Solve(Structures.Cantilever(4)).results.Max(r => r.Utilization);
            double fixedBeam = Solve(Structures.FixedBeam(4)).results.Max(r => r.Utilization);
            _out.WriteLine($"cantilever {cantilever:P0}, fixed-fixed {fixedBeam:P0} (beam theory ratio 6)");
            Assert.True(cantilever > 4 * fixedBeam);
        }

        [Theory]
        [InlineData(2, 3.0 / 32, 1.0 / 32)]
        [InlineData(4, 11.0 / 128, 5.0 / 128)]
        public void Fixed_beam_moments_match_beam_theory_for_the_lumped_weights(int beams, double end, double middle)
        {
            var (frame, _) = Solve(Structures.FixedBeam(beams));
            ContactLoad[] loads = frame.ContactLoads().SelectMany(l => l).ToArray();
            double MomentAt(double x) => Math.Abs(loads.First(c => Math.Abs(c.Point.X - x) < 1e-9).Moment.Z);

            double w = 500 * 0.2 * 0.2 * G;
            double length = 2.0 * beams;
            double wl2 = w * length * length;
            _out.WriteLine($"{beams} beams: end {MomentAt(0) / wl2:0.0000} wL² (theory {end:0.0000}), " +
                           $"middle {MomentAt(length / 2) / wl2:0.0000} wL² (theory {middle:0.0000})");

            Assert.Equal(end, MomentAt(0) / wl2, 1e-3);
            Assert.Equal(middle, MomentAt(length / 2) / wl2, 1e-3);
        }

        [Fact]
        public void Thin_floor_glued_on_a_thick_beam_leaves_the_load_to_the_beam()
        {
            var m = new StructureModel();
            Body beam = Structures.Box(new Vec3(0, 0, 0), new Vec3(4, 0.5, 0.5), Structures.Wood);
            beam.Mass = 2000;
            Body floor = Structures.Box(new Vec3(0, 0.315, 0), new Vec3(4, 0.13, 2), Structures.Wood);
            m.AddBody(beam);
            m.AddBody(floor);
            m.Ground(0, new Vec3(-2, 0, 0));
            m.Ground(0, new Vec3(2, 0, 0));
            m.Ground(1, new Vec3(-2, 0.315, 0));
            m.Ground(1, new Vec3(2, 0.315, 0));
            m.Connect(0, 1, new Vec3(0, 0.25, 0));

            var (frame, _) = Solve(m);
            var loads = frame.ContactLoads();
            double floorShare = (loads[1][0].Force.Y + loads[1][1].Force.Y) / ((beam.Mass + floor.Mass) * G);

            double Stiffness(double width, double depth)
            {
                double inertia = width * depth * depth * depth / 12;
                double half = 2;
                double compliance = half * half * half / (12 * Structures.Wood.Elasticity * inertia)
                                    + half / (5.0 / 6 * Structures.Wood.ShearModulus * width * depth);
                return 2 / compliance;
            }
            double expected = Stiffness(2, 0.13) / (Stiffness(2, 0.13) + Stiffness(0.5, 0.5));
            _out.WriteLine($"floor carries {floorShare:P1} (Timoshenko {expected:P1})");

            Assert.Equal(expected, floorShare, expected * 0.1);
        }

        [Fact]
        public void Unfinished_arch_bends_like_a_cantilever_and_the_closed_arch_works_in_compression()
        {
            const int blocks = 11;
            const double radius = 4;
            BodyResult[] open = Solve(Structures.Arch(blocks, radius, placed: blocks / 2 + 1)).results;
            BodyResult[] closed = Solve(Structures.Arch(blocks, radius)).results;

            double openMax = open.Max(r => r.Utilization);
            double closedMax = closed.Max(r => r.Utilization);
            _out.WriteLine($"open (half + keystone): {openMax:P0} {open[0].Mode}; closed: {closedMax:P0}");
            for (int k = 0; k < closed.Length; k++)
                _out.WriteLine($"  voussoir {k}: {closed[k].Utilization:P1} {closed[k].Mode} N={closed[k].AxialForce:0} M={closed[k].BendingMoment:0}");

            Assert.Equal(StressMode.Bending, open[0].Mode);
            Assert.True(openMax > 5 * closedMax);
            Assert.All(closed, r => Assert.True(r.AxialForce < 0));
        }

        [Fact]
        public void Diagonal_brace_relieves_a_cantilever()
        {
            StructureModel plain = Structures.Cantilever(2, height: 3);
            StructureModel braced = Structures.Cantilever(2, height: 3);
            var bottom = new Vec3(0, 1, 0);
            var top = new Vec3(2, 3, 0);
            Vec3 dir = (top - bottom).Normalized();
            int brace = braced.AddBody(Structures.Box((bottom + top) / 2, new Vec3((top - bottom).Length, 0.2, 0.2),
                                                      Structures.Wood, dir, Vec3.Cross(Vec3.UnitZ, dir)));
            braced.Ground(brace, bottom);
            braced.Connect(brace, 0, top);

            double before = Solve(plain).results.Max(r => r.Utilization);
            BodyResult[] after = Solve(braced).results;
            _out.WriteLine($"plain {before:P0}, braced beam {after[0].Utilization:P0}, brace {after[brace].Utilization:P0} {after[brace].Mode}");
            Assert.True(after[0].Utilization < before / 2);
        }

        [Fact]
        public void Solving_in_small_steps_gives_the_same_bits_as_solving_at_once()
        {
            StructureModel model = Structures.Arch(13, 5);
            BodyResult[] atOnce = Solve(model).results;

            var stepwise = new StructureAnalysis(model, 500);
            int steps = 0;
            while (!stepwise.Step(1))
                steps++;
            _out.WriteLine($"{steps} steps");
            Assert.True(steps > 10);
            for (int i = 0; i < atOnce.Length; i++)
                Assert.Equal(atOnce[i].Utilization, stepwise.Results[i].Utilization);
        }

        [Fact]
        public void Results_are_deterministic()
        {
            BodyResult[] a = Solve(Structures.Wall(12, 8)).results;
            BodyResult[] b = Solve(Structures.Wall(12, 8)).results;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].Utilization), BitConverter.DoubleToInt64Bits(b[i].Utilization));
                Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].AxialForce), BitConverter.DoubleToInt64Bits(b[i].AxialForce));
            }
        }

        [Theory]
        [InlineData(25, 20)]
        [InlineData(50, 20)]
        [InlineData(50, 40)]
        [InlineData(100, 50)]
        public void Large_wall_converges(int columns, int rows)
        {
            StructureModel wall = Structures.Wall(columns, rows);
            var sw = Stopwatch.StartNew();
            var (frame, results) = Solve(wall);
            sw.Stop();
            _out.WriteLine($"{wall.Bodies.Count} panels: {frame.Iterations} iterations, {sw.ElapsedMilliseconds} ms, max {results.Max(r => r.Utilization):P0}");
        }

        [Fact]
        public void Long_cantilever_converges()
        {
            var (frame, results) = Solve(Structures.Cantilever(40));
            _out.WriteLine($"{frame.Iterations} iterations, root {results[0].Utilization:P0}");
        }
    }
}
