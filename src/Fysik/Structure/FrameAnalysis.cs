using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public sealed class FrameAnalysis
    {
        private const int Dof = 6;
        private const int Block = Dof * Dof;

        private readonly StructureModel _model;
        private readonly int _n;
        private readonly LinkData[] _links;

        private readonly double[] _diagonal;
        private readonly double[] _offDiagonal;
        private readonly double[] _preconditioner;

        private readonly double[] _x, _r, _z, _p, _q, _f;
        private readonly double _tolerance;
        private readonly int _maxIterations;
        private double _rz;
        private bool _finished;

        private const int LinksPerStep = 16;
        private int _assembled;
        private bool _ready;
        private readonly double[] _ta = new double[Block], _tb = new double[Block], _d = new double[Block],
                                  _dta = new double[Block], _dtb = new double[Block], _tmp = new double[Block];

        public int Iterations { get; private set; }
        public bool Converged { get; private set; }
        public double RelativeResidual { get; private set; }

        public double[] Displacements => _x;

        private struct LinkData
        {
            public int A, B;
            public Vec3 ArmA, ArmB;
            public Mat3 Translational, Rotational;
        }

        public FrameAnalysis(StructureModel model, double tolerance = 1e-6, int maxIterations = 0)
        {
            _model = model;
            _n = model.Bodies.Count;
            _tolerance = tolerance;
            _maxIterations = maxIterations > 0 ? maxIterations : Math.Max(500, 4 * Dof * _n);

            _links = new LinkData[model.Links.Count];
            _diagonal = new double[_n * Block];
            _offDiagonal = new double[model.Links.Count * Block];
            _preconditioner = new double[_n * Block];
            _x = new double[_n * Dof];
            _r = new double[_n * Dof];
            _z = new double[_n * Dof];
            _p = new double[_n * Dof];
            _q = new double[_n * Dof];
            _f = new double[_n * Dof];

            for (int i = 0; i < _n; i++)
            {
                Vec3 w = model.Gravity * model.Bodies[i].Mass;
                _f[i * Dof + 0] = w.X;
                _f[i * Dof + 1] = w.Y;
                _f[i * Dof + 2] = w.Z;
            }
        }

        public bool Iterate(int steps)
        {
            if (!_ready)
            {
                int end = Math.Min(_links.Length, _assembled + steps * LinksPerStep);
                for (; _assembled < end; _assembled++)
                    AssembleLink(_assembled);
                if (_assembled < _links.Length)
                    return false;
                Initialize();
                return _finished;
            }

            for (int s = 0; s < steps && !_finished; s++)
            {
                Multiply(_p, _q);
                double pq = Dot(_p, _q);
                if (pq <= 0 || double.IsNaN(pq))
                {
                    _finished = true;
                    break;
                }
                double alpha = _rz / pq;
                for (int k = 0; k < _x.Length; k++)
                {
                    _x[k] += alpha * _p[k];
                    _r[k] -= alpha * _q[k];
                }
                Iterations++;
                UpdateResidual();
                if (_finished)
                    break;

                ApplyPreconditioner(_r, _z);
                double rzNew = Dot(_r, _z);
                double beta = rzNew / _rz;
                _rz = rzNew;
                for (int k = 0; k < _p.Length; k++)
                    _p[k] = _z[k] + beta * _p[k];
            }
            return _finished;
        }

        public void Solve()
        {
            while (!Iterate(64))
            {
            }
        }

        public List<ContactLoad>[] ContactLoads()
        {
            var loads = new List<ContactLoad>[_n];
            for (int i = 0; i < _n; i++)
                loads[i] = new List<ContactLoad>();

            foreach (LinkData l in _links)
            {
                Vec3 ua = Translation(l.A), ta = Rotation(l.A);
                Vec3 delta = -(ua + Vec3.Cross(ta, l.ArmA));
                Vec3 dTheta = -ta;
                if (l.B >= 0)
                {
                    Vec3 ub = Translation(l.B), tb = Rotation(l.B);
                    delta += ub + Vec3.Cross(tb, l.ArmB);
                    dTheta += tb;
                }
                Vec3 force = l.Translational * delta;
                Vec3 moment = l.Rotational * dTheta;
                Vec3 point = _model.Bodies[l.A].Center + l.ArmA;

                loads[l.A].Add(new ContactLoad { Point = point, Force = force, Moment = moment });
                if (l.B >= 0)
                    loads[l.B].Add(new ContactLoad { Point = point, Force = -force, Moment = -moment });
            }
            return loads;
        }

        public BodyResult[] Evaluate()
        {
            List<ContactLoad>[] loads = ContactLoads();
            var results = new BodyResult[_n];
            for (int i = 0; i < _n; i++)
                results[i] = Mechanics.Evaluate(_model.Bodies[i], loads[i], _model.Gravity);
            return results;
        }

        private Vec3 Translation(int i) => new Vec3(_x[i * Dof], _x[i * Dof + 1], _x[i * Dof + 2]);
        private Vec3 Rotation(int i) => new Vec3(_x[i * Dof + 3], _x[i * Dof + 4], _x[i * Dof + 5]);

        private void UpdateResidual()
        {
            double fNorm = Math.Sqrt(Dot(_f, _f));
            RelativeResidual = fNorm > 0 ? Math.Sqrt(Dot(_r, _r)) / fNorm : 0;
            if (RelativeResidual <= _tolerance)
            {
                Converged = true;
                _finished = true;
            }
            else if (Iterations >= _maxIterations)
            {
                _finished = true;
            }
        }

        private void AssembleLink(int li)
        {
            Link link = _model.Links[li];
            Body a = _model.Bodies[link.A];
            var data = new LinkData { A = link.A, B = link.B, ArmA = link.Point - a.Center };

            Mechanics.SegmentStiffness(a, link.Point, out Mat3 ktA, out Mat3 krA);
            if (link.IsGround)
            {
                data.Translational = ktA;
                data.Rotational = krA;
            }
            else
            {
                Body b = _model.Bodies[link.B];
                data.ArmB = link.Point - b.Center;
                Mechanics.SegmentStiffness(b, link.Point, out Mat3 ktB, out Mat3 krB);
                data.Translational = Mechanics.Series(ktA, ktB);
                data.Rotational = Mechanics.Series(krA, krB);
            }
            _links[li] = data;

            Array.Clear(_ta, 0, Block);
            SetBlock(_ta, 0, 0, -Mat3.Identity);
            SetBlock(_ta, 0, 3, Mat3.Skew(data.ArmA));
            SetBlock(_ta, 3, 3, -Mat3.Identity);
            Array.Clear(_d, 0, Block);
            SetBlock(_d, 0, 0, data.Translational);
            SetBlock(_d, 3, 3, data.Rotational);

            Mul6(_d, _ta, _dta);
            MulT6(_ta, _dta, _tmp);
            Add6(_diagonal, link.A * Block, _tmp);

            if (!link.IsGround)
            {
                Array.Clear(_tb, 0, Block);
                SetBlock(_tb, 0, 0, Mat3.Identity);
                SetBlock(_tb, 0, 3, -Mat3.Skew(data.ArmB));
                SetBlock(_tb, 3, 3, Mat3.Identity);
                Mul6(_d, _tb, _dtb);
                MulT6(_tb, _dtb, _tmp);
                Add6(_diagonal, link.B * Block, _tmp);
                MulT6(_ta, _dtb, _tmp);
                Array.Copy(_tmp, 0, _offDiagonal, li * Block, Block);
            }
        }

        private void Initialize()
        {
            for (int i = 0; i < _n; i++)
                Cholesky6(_diagonal, i * Block, _preconditioner, i * Block);

            Multiply(_x, _q);
            for (int k = 0; k < _r.Length; k++)
                _r[k] = _f[k] - _q[k];
            ApplyPreconditioner(_r, _z);
            Array.Copy(_z, _p, _z.Length);
            _rz = Dot(_r, _z);
            _ready = true;
            UpdateResidual();
        }

        private void Multiply(double[] x, double[] y)
        {
            for (int i = 0; i < _n; i++)
                MulVec6(_diagonal, i * Block, x, i * Dof, y, i * Dof, overwrite: true);
            for (int li = 0; li < _links.Length; li++)
            {
                int a = _links[li].A, b = _links[li].B;
                if (b < 0)
                    continue;
                MulVec6(_offDiagonal, li * Block, x, b * Dof, y, a * Dof, overwrite: false);
                MulVecT6(_offDiagonal, li * Block, x, a * Dof, y, b * Dof);
            }
        }

        private void ApplyPreconditioner(double[] r, double[] z)
        {
            for (int i = 0; i < _n; i++)
                CholeskySolve6(_preconditioner, i * Block, r, z, i * Dof);
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0;
            for (int k = 0; k < a.Length; k++)
                s += a[k] * b[k];
            return s;
        }

        private static void SetBlock(double[] m, int row, int col, Mat3 b)
        {
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    m[(row + r) * Dof + col + c] = b[r, c];
        }

        private static void Mul6(double[] a, double[] b, double[] result)
        {
            for (int r = 0; r < Dof; r++)
                for (int c = 0; c < Dof; c++)
                {
                    double s = 0;
                    for (int k = 0; k < Dof; k++)
                        s += a[r * Dof + k] * b[k * Dof + c];
                    result[r * Dof + c] = s;
                }
        }

        private static void MulT6(double[] a, double[] b, double[] result)
        {
            for (int r = 0; r < Dof; r++)
                for (int c = 0; c < Dof; c++)
                {
                    double s = 0;
                    for (int k = 0; k < Dof; k++)
                        s += a[k * Dof + r] * b[k * Dof + c];
                    result[r * Dof + c] = s;
                }
        }

        private static void Add6(double[] target, int offset, double[] m)
        {
            for (int k = 0; k < Block; k++)
                target[offset + k] += m[k];
        }

        private static void MulVec6(double[] m, int mo, double[] x, int xo, double[] y, int yo, bool overwrite)
        {
            for (int r = 0; r < Dof; r++)
            {
                int row = mo + r * Dof;
                double s = m[row] * x[xo] + m[row + 1] * x[xo + 1] + m[row + 2] * x[xo + 2]
                           + m[row + 3] * x[xo + 3] + m[row + 4] * x[xo + 4] + m[row + 5] * x[xo + 5];
                y[yo + r] = overwrite ? s : y[yo + r] + s;
            }
        }

        private static void MulVecT6(double[] m, int mo, double[] x, int xo, double[] y, int yo)
        {
            for (int c = 0; c < Dof; c++)
            {
                double s = 0;
                for (int r = 0; r < Dof; r++)
                    s += m[mo + r * Dof + c] * x[xo + r];
                y[yo + c] += s;
            }
        }

        private static void Cholesky6(double[] a, int ao, double[] l, int lo)
        {
            for (int i = 0; i < Dof; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double s = a[ao + i * Dof + j];
                    for (int k = 0; k < j; k++)
                        s -= l[lo + i * Dof + k] * l[lo + j * Dof + k];
                    if (i == j)
                        l[lo + i * Dof + i] = Math.Sqrt(Math.Max(s, 1e-30));
                    else
                        l[lo + i * Dof + j] = s / l[lo + j * Dof + j];
                }
            }
        }

        private static void CholeskySolve6(double[] l, int lo, double[] b, double[] x, int o)
        {
            for (int i = 0; i < Dof; i++)
            {
                double s = b[o + i];
                for (int k = 0; k < i; k++)
                    s -= l[lo + i * Dof + k] * x[o + k];
                x[o + i] = s / l[lo + i * Dof + i];
            }
            for (int i = Dof - 1; i >= 0; i--)
            {
                double s = x[o + i];
                for (int k = i + 1; k < Dof; k++)
                    s -= l[lo + k * Dof + i] * x[o + k];
                x[o + i] = s / l[lo + i * Dof + i];
            }
        }
    }
}
