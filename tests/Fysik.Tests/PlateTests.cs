using System.Linq;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class PlateTests
    {
        private readonly ITestOutputHelper _out;

        public PlateTests(ITestOutputHelper output) => _out = output;

        private static StructureModel IrregularDeck(out int[] planks)
        {
            var m = new StructureModel();
            var grid = new int[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    grid[i, j] = m.AddBody(Structures.Box(new Vec3(2 * i, 2.065, 2 * j), new Vec3(2, 0.13, 2), Structures.Wood));
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    if (i + 1 < 3) m.Connect(grid[i, j], grid[i + 1, j], new Vec3(2 * i + 1, 2.065, 2 * j));
                    if (j + 1 < 3) m.Connect(grid[i, j], grid[i, j + 1], new Vec3(2 * i, 2.065, 2 * j + 1));
                    if (i + 1 < 3 && j + 1 < 3) m.Connect(grid[i, j], grid[i + 1, j + 1], new Vec3(2 * i + 1, 2.065, 2 * j + 1));
                    if (i + 1 < 3 && j > 0) m.Connect(grid[i, j], grid[i + 1, j - 1], new Vec3(2 * i + 1, 2.065, 2 * j - 1));
                }
            void Post(int i, int j, double dx, double dz)
            {
                var top = new Vec3(2 * i + dx, 2.0, 2 * j + dz);
                int p = m.AddBody(Structures.Box(new Vec3(top.X, 1, top.Z), new Vec3(0.4, 2, 0.4), Structures.Wood));
                m.Ground(p, new Vec3(top.X, 0, top.Z));
                m.Connect(grid[i, j], p, top);
            }
            Post(0, 0, -0.8, -0.8);
            Post(2, 0, 0.8, -0.8);
            Post(1, 2, 0, 0.8);
            planks = grid.Cast<int>().ToArray();
            return m;
        }

        [Fact]
        public void Floor_on_irregular_supports_bends_instead_of_shearing()
        {
            StructureModel deck = IrregularDeck(out int[] planks);
            var frame = new FrameAnalysis(deck);
            frame.Solve();
            BodyResult[] r = frame.Evaluate();
            foreach (int p in planks)
                _out.WriteLine($"plank {p}: {r[p].Mode} {r[p].Utilization:P0} (M={r[p].BendingMoment:0}, T={r[p].Torque:0})");

            Assert.All(planks, p => Assert.Equal(StressMode.Bending, r[p].Mode));
            Assert.True(planks.Max(p => r[p].Utilization) < 0.5);
        }

        [Fact]
        public void Beam_twisted_by_a_load_hanging_off_its_side_reports_torsion()
        {
            var m = new StructureModel();
            int beam = m.AddBody(Structures.Box(new Vec3(0.3, 2, 0), new Vec3(0.6, 0.3, 0.3), Structures.Wood));
            m.Ground(beam, new Vec3(0, 2, 0));
            int plank = m.AddBody(Structures.Box(new Vec3(0.6, 2.2, 1.1), new Vec3(2, 0.13, 2), Structures.Wood));
            m.Connect(plank, beam, new Vec3(0.6, 2.15, 0.1));

            var frame = new FrameAnalysis(m);
            frame.Solve();
            BodyResult r = frame.Evaluate()[beam];
            _out.WriteLine($"beam: {r.Mode} {r.Utilization:P0} (M={r.BendingMoment:0}, T={r.Torque:0})");
            Assert.Equal(StressMode.Torsion, r.Mode);
        }
    }
}
