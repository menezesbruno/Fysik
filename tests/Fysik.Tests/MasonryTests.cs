using System;
using System.Linq;
using Fysik.Structure;
using Xunit;
using Xunit.Abstractions;

namespace Fysik.Tests
{
    public class MasonryTests
    {
        private static readonly Vec3 Cube = new Vec3(1, 1, 1);
        private readonly ITestOutputHelper _out;

        public MasonryTests(ITestOutputHelper output) => _out = output;

        private BodyResult[] Run(Blocks blocks, string title)
        {
            StructureModel model = blocks.Build();
            BodyResult[] results = Blocks.Solve(model, out StructureAnalysis analysis);
            _out.WriteLine($"{title}: {analysis.MasonryRounds} rounds, {analysis.Iterations} iterations, converged {analysis.Converged}");
            for (int i = 0; i < results.Length; i++)
                _out.WriteLine($"  #{i} {results[i].Mode} {results[i].Utilization:P1}");
            Assert.True(analysis.MasonrySettled, $"{title}: masonry did not settle in {analysis.MasonryRounds} rounds");
            return results;
        }

        private static Blocks Overhang(double offset, MaterialProps material = null)
        {
            var blocks = new Blocks();
            blocks.Add(new Vec3(0, 0.5, 0), Cube, material);
            blocks.Add(new Vec3(offset, 1.5, 0), Cube, material);
            return blocks;
        }

        [Fact]
        public void A_block_resting_with_its_weight_over_the_one_below_stays()
        {
            BodyResult[] r = Run(Overhang(0.3), "offset 0.3");
            Assert.All(r, x => Assert.True(x.Utilization < 1, $"{x.Mode} {x.Utilization:P0}"));
            Assert.Equal(StressMode.Tipping, r[1].Mode);
            Assert.Equal(0.15 / 0.35, r[1].Utilization, 0.02);
        }

        [Fact]
        public void A_block_with_its_weight_past_the_edge_below_tips_over()
        {
            BodyResult[] r = Run(Overhang(0.6), "offset 0.6");
            Assert.Equal(StressMode.Unstable, r[1].Mode);
            Assert.True(r[1].Utilization >= 1);
        }

        [Fact]
        public void Wooden_blocks_stay_joined_whatever_the_overhang()
        {
            BodyResult[] r = Run(Overhang(0.6, Structures.Wood), "wood, offset 0.6");
            Assert.DoesNotContain(r, x => x.Mode == StressMode.Unstable || x.Mode == StressMode.Tipping);
        }

        [Fact]
        public void A_stack_of_blocks_stands_while_each_part_has_its_weight_over_what_holds_it()
        {
            var stable = new Blocks();
            stable.Add(new Vec3(0, 0.5, 0), Cube);
            stable.Add(new Vec3(0.2, 1.5, 0), Cube);
            stable.Add(new Vec3(0.6, 2.5, 0), Cube);
            BodyResult[] r = Run(stable, "stack 0.2 + 0.4");
            Assert.DoesNotContain(r, x => x.Utilization >= 1);

            var leaning = new Blocks();
            leaning.Add(new Vec3(0, 0.5, 0), Cube);
            leaning.Add(new Vec3(0.35, 1.5, 0), Cube);
            leaning.Add(new Vec3(0.8, 2.5, 0), Cube);
            r = Run(leaning, "stack 0.35 + 0.45");
            Assert.Equal(StressMode.Unstable, r[1].Mode);
            Assert.Equal(StressMode.Unstable, r[2].Mode);
        }

        private static Blocks Arch(Voussoir v, int piers, bool keystone = true)
        {
            var blocks = new Blocks();
            double outer = v.OuterCorner, inner = v.InnerCorner;
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < piers; k++)
                    blocks.Add(new Vec3(side * (inner + outer) / 2, k + 0.5, 0), new Vec3(outer - inner, 1, 1));
            double radius = v.OuterRadius - v.Height / 2;
            for (int k = 0; k < v.Stones; k++)
            {
                double angle = 180 - (k + 0.5) * v.Angle;
                double a = angle * Math.PI / 180;
                int stone = blocks.Add(new Vec3(Math.Cos(a) * radius, piers + Math.Sin(a) * radius, 0),
                                       new Vec3(v.MeanWidth, v.Height, 1), null, angle - 90);
                if (!keystone && k == v.Stones / 2)
                    blocks.Remove(stone);
            }
            return blocks;
        }

        [Fact]
        public void A_half_built_arch_stands_on_wooden_centering()
        {
            Voussoir v = Voussoir.HalfCircle(15, 4, 5);
            Blocks blocks = Arch(v, 2, keystone: false);
            double radius = v.InnerRadius;
            for (int k = 3; k < v.Stones - 3; k++)
            {
                if (k == v.Stones / 2)
                    continue;
                double a = (180 - (k + 0.5) * v.Angle) * Math.PI / 180;
                double x = Math.Cos(a) * radius * 0.9, top = 2 + Math.Sin(a) * radius * 0.97;
                blocks.Add(new Vec3(x, top / 2, 0), new Vec3(0.3, top, 0.3), Structures.Wood);
            }
            BodyResult[] r = Run(blocks, "8 m arch without keystone on centering");
            Assert.DoesNotContain(r, x => x.Mode == StressMode.Unstable);
        }

        [Fact]
        public void An_arch_stands_and_falls_without_its_keystone()
        {
            Voussoir v = Voussoir.HalfCircle(15, 4, 5);
            BodyResult[] r = Run(Arch(v, 2), "8 m arch");
            Assert.DoesNotContain(r, x => x.Utilization >= 1);

            r = Run(Arch(v, 2, keystone: false), "8 m arch without keystone");
            Assert.Contains(r, x => x.Mode == StressMode.Unstable);
        }

        private static Blocks Wall(bool running, int width, int height)
        {
            var blocks = new Blocks();
            for (int row = 0; row < height; row++)
            {
                double x = 0;
                if (running && row % 2 == 1)
                {
                    blocks.Add(new Vec3(0.5, row + 0.5, 0), Cube);
                    x = 1;
                }
                while (x < width)
                {
                    double length = running && x + 2 <= width ? 2 : 1;
                    blocks.Add(new Vec3(x + length / 2, row + 0.5, 0), new Vec3(length, 1, 1));
                    x += length;
                }
            }
            return blocks;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void A_plain_wall_stands_easily(bool running)
        {
            BodyResult[] r = Run(Wall(running, 6, 4), running ? "running bond" : "stack bond");
            Assert.DoesNotContain(r, x => x.Mode == StressMode.Unstable);
            Assert.True(r.Max(x => x.Utilization) < 0.5, $"worst {r.Max(x => x.Utilization):P0}");
        }

        [Fact]
        public void A_block_cut_to_fit_rests_on_the_true_face_of_the_stone_below()
        {
            double turn = 25, a = turn * Math.PI / 180;
            var normal = new Vec3(-Math.Sin(a), Math.Cos(a), 0);
            var blocks = new Blocks();
            blocks.Add(new Vec3(0, 1, 0), new Vec3(3, 1, 1), null, turn);
            Vec3 face = new Vec3(0, 1, 0) + normal * 0.5;
            blocks.Add(face + normal * 0.45, Cube, null, 0, fitted: true);
            BodyResult[] r = Run(blocks, "block cut to fit a 25° slope");
            Assert.Equal(StressMode.Sliding, r[1].Mode);
            Assert.Equal(Math.Tan(a) / 0.6 * Math.Cos(a), r[1].Utilization, 0.02);
        }

        [Fact]
        public void A_block_cut_to_fit_does_not_hang_on_the_edge_of_a_stone_beside_it()
        {
            var alone = new Blocks();
            alone.Add(new Vec3(1, 0.5, 0), Cube);
            alone.Add(new Vec3(1, 1.5, 0), Cube, null, 0, fitted: true);
            BodyResult[] a = Run(alone, "cut block on a block");

            var beside = new Blocks();
            beside.Add(new Vec3(1, 0.5, 0), Cube);
            beside.Add(new Vec3(1, 1.5, 0), Cube, null, 0, fitted: true);
            beside.Add(new Vec3(0, 0.5, 0), Cube);
            BodyResult[] b = Run(beside, "the same, its edge touching the edge of a stone beside it");
            Assert.Equal(a[1].Utilization, b[1].Utilization, 2);
            Assert.True(b[2].Utilization < 0.05, $"the stone beside reads {b[2].Utilization:P0}");
        }

        [Fact]
        public void A_column_of_blocks_stands_in_compression()
        {
            var blocks = new Blocks();
            for (int k = 0; k < 10; k++)
                blocks.Add(new Vec3(0, k + 0.5, 0), Cube);
            BodyResult[] r = Run(blocks, "column");
            Assert.DoesNotContain(r, x => x.Mode == StressMode.Unstable || x.Utilization > 0.2);
        }

        [Fact]
        public void A_slab_held_only_by_the_side_of_a_wall_falls()
        {
            var blocks = new Blocks();
            for (int k = 0; k < 3; k++)
                blocks.Add(new Vec3(0, k + 0.5, 0), Cube);
            int slab = blocks.Add(new Vec3(1, 2.75, 0), new Vec3(1, 0.5, 1));
            BodyResult[] r = Run(blocks, "slab on the side of a wall");
            Assert.Equal(StressMode.Unstable, r[slab].Mode);
            Assert.DoesNotContain(r.Take(3), x => x.Mode == StressMode.Unstable);
        }

        [Fact]
        public void A_wooden_beam_on_the_side_of_a_stone_wall_stays()
        {
            var blocks = new Blocks();
            for (int k = 0; k < 3; k++)
                blocks.Add(new Vec3(0, k + 0.5, 0), Cube);
            int beam = blocks.Add(new Vec3(1.5, 2.5, 0), new Vec3(2, 0.2, 0.2), Structures.Wood);
            BodyResult[] r = Run(blocks, "wooden beam on the side of a wall");
            Assert.DoesNotContain(r, x => x.Mode == StressMode.Unstable);
            Assert.True(r[beam].Utilization < 1);
        }
    }
}
