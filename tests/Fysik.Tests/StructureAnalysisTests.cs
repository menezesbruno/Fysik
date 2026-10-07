using System.Linq;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class StructureAnalysisTests
    {
        private readonly ITestOutputHelper _out;

        public StructureAnalysisTests(ITestOutputHelper output) => _out = output;

        private static BodyResult[] Run(StructureModel model, int maxFrameBodies, out SolverKind solver)
        {
            var analysis = new StructureAnalysis(model, maxFrameBodies);
            while (!analysis.Step(32))
            {
            }
            solver = analysis.Solver;
            return analysis.Results;
        }

        [Fact]
        public void Island_without_ground_is_unsupported()
        {
            var model = new StructureModel();
            model.AddBody(Structures.Box(new Vec3(0, 5, 0), new Vec3(2, 0.2, 0.2), Structures.Wood));
            model.AddBody(Structures.Box(new Vec3(2, 5, 0), new Vec3(2, 0.2, 0.2), Structures.Wood));
            model.Connect(0, 1, new Vec3(1, 5, 0));

            BodyResult[] results = Run(model, 500, out SolverKind solver);
            Assert.Equal(SolverKind.None, solver);
            Assert.All(results, r => Assert.Equal(StressMode.Unsupported, r.Mode));
        }

        [Fact]
        public async System.Threading.Tasks.Task Worker_thread_solve_matches_the_stepwise_solve_and_can_be_cancelled()
        {
            StructureModel model = Structures.Arch(13, 5);
            BodyResult[] stepwise = Run(model, 500, out _);

            var background = new StructureAnalysis(model, 500);
            await System.Threading.Tasks.Task.Run(() => background.RunToCompletion(() => false));
            Assert.True(background.Finished);
            for (int i = 0; i < stepwise.Length; i++)
                Assert.Equal(stepwise[i].Utilization, background.Results[i].Utilization);

            var cancelled = new StructureAnalysis(model, 500);
            cancelled.RunToCompletion(() => true);
            Assert.False(cancelled.Finished);
        }

        [Fact]
        public void Large_islands_use_the_load_path_model()
        {
            Run(Structures.Wall(4, 3), maxFrameBodies: 10, out SolverKind solver);
            Assert.Equal(SolverKind.LoadPath, solver);
        }

        [Fact]
        public void Load_path_column_base_carries_the_whole_weight()
        {
            BodyResult[] results = Run(Structures.Column(4), maxFrameBodies: 0, out _);
            double weight = 4 * 500 * 0.2 * 2 * 0.2 * 9.81;
            Assert.Equal(-weight, results[0].AxialForce, weight * 1e-6);
            Assert.Equal(StressMode.Compression, results[0].Mode);
        }

        [Fact]
        public void Load_path_cantilever_root_moment_matches_beam_theory()
        {
            BodyResult[] results = Run(Structures.Cantilever(4), maxFrameBodies: 0, out _);
            double w = 500 * 0.2 * 0.2 * 9.81;
            double expected = w * 8 * 8 / 2;
            _out.WriteLine($"root moment {results[0].BendingMoment:0} (theory {expected:0})");
            Assert.Equal(expected, results[0].BendingMoment, expected * 0.01);
        }

        [Fact]
        public void Load_path_handles_thousands_of_pieces_quickly()
        {
            StructureModel wall = Structures.Wall(100, 50);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            BodyResult[] results = Run(wall, 500, out SolverKind solver);
            sw.Stop();
            _out.WriteLine($"{results.Length} pieces in {sw.Elapsed.TotalMilliseconds:0.0} ms");
            Assert.Equal(SolverKind.LoadPath, solver);
            Assert.True(sw.ElapsedMilliseconds < 200);
        }

        [Fact]
        public void Load_path_and_frame_agree_on_a_plain_wall()
        {
            StructureModel wall = Structures.Wall(6, 4);
            double frame = Run(wall, 1000, out _).Max(r => r.Utilization);
            double path = Run(wall, 0, out _).Max(r => r.Utilization);
            _out.WriteLine($"frame {frame:P1}, load path {path:P1}");
            Assert.InRange(path / frame, 0.5, 2.0);
        }
    }
}
