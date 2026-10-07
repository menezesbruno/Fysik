using System;
using Fysik.Structure;

namespace Fysik.Tests
{
    internal static class Structures
    {
        public static readonly MaterialProps Wood = new MaterialProps
        {
            Name = "wood",
            Density = 500,
            Elasticity = 10e9,
            ShearModulus = 0.6e9,
            CompressiveStrength = 4e6,
            TensileStrength = 4e6,
            ShearStrength = 1e6,
            BucklingSlenderness = 60,
        };

        public static readonly MaterialProps Stone = new MaterialProps
        {
            Name = "stone",
            Density = 2400,
            Elasticity = 30e9,
            ShearModulus = 12e9,
            CompressiveStrength = 20e6,
            TensileStrength = 1.5e6,
            ShearStrength = 2e6,
            BucklingSlenderness = 30,
        };

        public static Body Box(Vec3 center, Vec3 size, MaterialProps material, Vec3? axisX = null, Vec3? axisY = null)
        {
            Vec3 x = axisX ?? Vec3.UnitX;
            Vec3 y = axisY ?? Vec3.UnitY;
            return new Body
            {
                Center = center,
                AxisX = x,
                AxisY = y,
                AxisZ = Vec3.Cross(x, y),
                Size = size,
                Mass = material.Density * size.X * size.Y * size.Z,
                Material = material,
            };
        }

        public static StructureModel Cantilever(int beams, double height = 1)
        {
            var m = new StructureModel();
            for (int k = 0; k < beams; k++)
                m.AddBody(Box(new Vec3(1 + 2 * k, height, 0), new Vec3(2, 0.2, 0.2), Wood));
            m.Ground(0, new Vec3(0, height, 0));
            for (int k = 0; k + 1 < beams; k++)
                m.Connect(k, k + 1, new Vec3(2 * (k + 1), height, 0));
            return m;
        }

        public static StructureModel FixedBeam(int beams)
        {
            StructureModel m = Cantilever(beams);
            m.Ground(beams - 1, new Vec3(2 * beams, 1, 0));
            return m;
        }

        public static StructureModel Column(int posts)
        {
            var m = new StructureModel();
            for (int k = 0; k < posts; k++)
                m.AddBody(Box(new Vec3(0, 1 + 2 * k, 0), new Vec3(0.2, 2, 0.2), Wood));
            m.Ground(0, new Vec3(0, 0, 0));
            for (int k = 0; k + 1 < posts; k++)
                m.Connect(k, k + 1, new Vec3(0, 2 * (k + 1), 0));
            return m;
        }

        public static StructureModel Arch(int blocks, double radius, int placed = -1)
        {
            if (placed < 0)
                placed = blocks;
            var m = new StructureModel();
            double step = Math.PI / blocks;
            double chord = 2 * radius * Math.Sin(step / 2);
            for (int k = 0; k < placed; k++)
            {
                double mid = Math.PI - (k + 0.5) * step;
                var radial = new Vec3(Math.Cos(mid), Math.Sin(mid), 0);
                var tangent = new Vec3(-Math.Sin(mid), Math.Cos(mid), 0);
                m.AddBody(Box(radial * radius, new Vec3(chord, 1, 1), Stone, tangent, radial));
            }
            Vec3 Joint(int k) => new Vec3(Math.Cos(Math.PI - k * step), Math.Sin(Math.PI - k * step), 0) * radius;
            m.Ground(0, Joint(0));
            for (int k = 0; k + 1 < placed; k++)
                m.Connect(k, k + 1, Joint(k + 1));
            if (placed == blocks)
                m.Ground(blocks - 1, Joint(blocks));
            return m;
        }

        public static StructureModel Wall(int columns, int rows)
        {
            var m = new StructureModel();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    m.AddBody(Box(new Vec3(1 + 2 * c, 1 + 2 * r, 0), new Vec3(2, 2, 0.2), Wood));
            int Index(int c, int r) => r * columns + c;
            for (int c = 0; c < columns; c++)
                m.Ground(Index(c, 0), new Vec3(1 + 2 * c, 0, 0));
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    if (c + 1 < columns)
                        m.Connect(Index(c, r), Index(c + 1, r), new Vec3(2 * (c + 1), 1 + 2 * r, 0));
                    if (r + 1 < rows)
                        m.Connect(Index(c, r), Index(c, r + 1), new Vec3(1 + 2 * c, 2 * (r + 1), 0));
                }
            return m;
        }
    }
}
