using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public sealed class MaterialProps
    {
        public string Name = "default";

        public double Density = 500;

        public double Elasticity = 10e9;

        public double ShearModulus = 0.6e9;

        public double CompressiveStrength = 4e6;
        public double TensileStrength = 4e6;
        public double ShearStrength = 1e6;

        public double BucklingSlenderness = 60;
    }

    public sealed class Body
    {
        public Vec3 Center;

        public Vec3 AxisX = Vec3.UnitX, AxisY = Vec3.UnitY, AxisZ = Vec3.UnitZ;

        public Vec3 Size;

        public double Mass;

        public MaterialProps Material;

        public double StrengthFactor = 1;

        public Vec3 Axis(int i) => i == 0 ? AxisX : i == 1 ? AxisY : AxisZ;

        public double Extent(int i) => Size[i];
    }

    public readonly struct Link
    {
        public const int Ground = -1;

        public readonly int A;
        public readonly int B;

        public readonly Vec3 Point;

        public Link(int a, int b, Vec3 point)
        {
            if (a < 0)
                throw new ArgumentOutOfRangeException(nameof(a), "The first body of a link cannot be the ground");
            A = a;
            B = b;
            Point = point;
        }

        public bool IsGround => B == Ground;
    }

    public sealed class StructureModel
    {
        public readonly List<Body> Bodies = new List<Body>();
        public readonly List<Link> Links = new List<Link>();
        public Vec3 Gravity = new Vec3(0, -9.81, 0);

        public int AddBody(Body body)
        {
            Bodies.Add(body);
            return Bodies.Count - 1;
        }

        public void Connect(int a, int b, Vec3 point) => Links.Add(new Link(a, b, point));

        public void Ground(int a, Vec3 point) => Links.Add(new Link(a, Link.Ground, point));
    }

    public enum StressMode : byte
    {
        None,
        Compression,
        Tension,
        Bending,
        Shear,
        Unsupported,

        Torsion,
    }

    public struct BodyResult
    {
        public double Utilization;
        public StressMode Mode;

        public double AxialForce;

        public double BendingMoment;

        public double Torque;

        public static BodyResult Unsupported => new BodyResult
        {
            Utilization = double.PositiveInfinity,
            Mode = StressMode.Unsupported,
        };
    }
}
