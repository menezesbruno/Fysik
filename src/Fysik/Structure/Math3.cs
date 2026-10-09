using System;

namespace Fysik.Structure
{
    public readonly struct Vec3
    {
        public readonly double X, Y, Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 UnitX = new Vec3(1, 0, 0);
        public static readonly Vec3 UnitY = new Vec3(0, 1, 0);
        public static readonly Vec3 UnitZ = new Vec3(0, 0, 1);

        public double this[int i] => i == 0 ? X : i == 1 ? Y : Z;

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, double s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public double LengthSquared => X * X + Y * Y + Z * Z;
        public double Length => Math.Sqrt(LengthSquared);

        public Vec3 Normalized()
        {
            double len = Length;
            return len > 1e-12 ? this / len : Zero;
        }

        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }

    public struct Mat3
    {
        public double M00, M01, M02, M10, M11, M12, M20, M21, M22;

        public static readonly Mat3 Identity = new Mat3 { M00 = 1, M11 = 1, M22 = 1 };

        public double this[int r, int c]
        {
            get
            {
                switch (r * 3 + c)
                {
                    case 0: return M00;
                    case 1: return M01;
                    case 2: return M02;
                    case 3: return M10;
                    case 4: return M11;
                    case 5: return M12;
                    case 6: return M20;
                    case 7: return M21;
                    default: return M22;
                }
            }
        }

        public static Mat3 Skew(Vec3 r) => new Mat3
        {
            M01 = -r.Z, M02 = r.Y,
            M10 = r.Z, M12 = -r.X,
            M20 = -r.Y, M21 = r.X,
        };

        public static Mat3 operator -(Mat3 a) => new Mat3
        {
            M00 = -a.M00, M01 = -a.M01, M02 = -a.M02,
            M10 = -a.M10, M11 = -a.M11, M12 = -a.M12,
            M20 = -a.M20, M21 = -a.M21, M22 = -a.M22,
        };
    }
}
