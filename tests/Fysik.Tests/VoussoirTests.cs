using System;
using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class VoussoirTests
    {
        private struct Pose
        {
            public double X, Y, Angle;

            public (double x, double y) Apply(double x, double y)
            {
                double a = Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
                return (X + x * c - y * s, Y + x * s + y * c);
            }

            public Pose Then(Voussoir v, int side)
            {
                Vec3 next = v.Next(side);
                (double x, double y) = Apply(next.X, next.Y);
                return new Pose { X = x, Y = y, Angle = Angle + v.Turn(side) };
            }

            public static Pose Springer(Voussoir v, double innerEdge)
            {
                var pose = new Pose { Angle = v.SpringerTurn };
                (double x, double y) face = pose.Apply(v.SpringingFace.X, v.SpringingFace.Y);
                pose.X = -(innerEdge + 0.5) - face.x;
                pose.Y = -face.y;
                return pose;
            }
        }

        public static TheoryData<int, double, double> Arches => new TheoryData<int, double, double>
        {
            { 9, 2, 3 },
            { 13, 3, 4 },
            { 15, 4, 5 },
            { 19, 5, 6 },
            { 23, 6, 7 },
            { 25, 7, 8 },
            { 29, 8, 9 },
        };

        [Theory]
        [MemberData(nameof(Arches))]
        public void An_arch_stone_fits_the_building_grid(int stones, double inner, double outer)
        {
            var v = Voussoir.HalfCircle(stones, inner, outer);
            Assert.Equal(stones, v.Stones);
            Assert.Equal(inner, v.InnerCorner, 9);
            Assert.Equal(outer, v.OuterCorner, 9);
            double face = Math.Sqrt(Math.Pow((v.Width - v.InnerWidth) / 2, 2) + v.Height * v.Height);
            Assert.Equal(outer - inner, face, 9);
            Assert.Equal(1.0, v.Narrowing(v.Height / 2), 9);
            Assert.Equal(v.InnerWidth / v.Width, v.Narrowing(-v.Height / 2), 9);
            Assert.InRange(v.Width, 0.9, 1.1);
            Assert.InRange(v.Height, 0.9, 1.1);
        }

        [Theory]
        [MemberData(nameof(Arches))]
        public void Each_stone_laid_against_the_last_shares_its_whole_face(int stones, double inner, double outer)
        {
            var v = Voussoir.HalfCircle(stones, inner, outer);
            foreach (int side in new[] { 1, -1 })
            {
                var pose = new Pose();
                for (int k = 0; k < v.Stones / 2; k++)
                {
                    Pose next = pose.Then(v, side);
                    (double x, double y) outerA = pose.Apply(side * v.Width / 2, v.Height / 2);
                    (double x, double y) innerA = pose.Apply(side * v.InnerWidth / 2, -v.Height / 2);
                    (double x, double y) outerB = next.Apply(-side * v.Width / 2, v.Height / 2);
                    (double x, double y) innerB = next.Apply(-side * v.InnerWidth / 2, -v.Height / 2);
                    Assert.Equal(outerA.x, outerB.x, 9);
                    Assert.Equal(outerA.y, outerB.y, 9);
                    Assert.Equal(innerA.x, innerB.x, 9);
                    Assert.Equal(innerA.y, innerB.y, 9);
                    pose = next;
                }
            }
        }

        [Theory]
        [MemberData(nameof(Arches))]
        public void An_arch_raised_from_a_springer_on_a_wall_lands_on_the_wall_across(int stones, double inner, double outer)
        {
            var v = Voussoir.HalfCircle(stones, inner, outer);
            Pose pose = Pose.Springer(v, inner);
            (double x, double y) springIn = pose.Apply(-v.InnerWidth / 2, -v.Height / 2);
            (double x, double y) springOut = pose.Apply(-v.Width / 2, v.Height / 2);
            Assert.Equal(-inner, springIn.x, 9);
            Assert.Equal(0, springIn.y, 9);
            Assert.Equal(-outer, springOut.x, 9);
            Assert.Equal(0, springOut.y, 9);

            for (int k = 1; k < stones; k++)
                pose = pose.Then(v, 1);
            Assert.Equal(-v.SpringerTurn, pose.Angle, 9);
            (double x, double y) landIn = pose.Apply(v.InnerWidth / 2, -v.Height / 2);
            (double x, double y) landOut = pose.Apply(v.Width / 2, v.Height / 2);
            Assert.Equal(inner, landIn.x, 9);
            Assert.Equal(0, landIn.y, 9);
            Assert.Equal(outer, landOut.x, 9);
            Assert.Equal(0, landOut.y, 9);
        }
    }
}
