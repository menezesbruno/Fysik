using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public sealed class Masonry
    {
        private const int MaxSolves = 40;
        private const double JointLimit = 0.99;
        private const double UnstableUtilization = 1.5;
        private const double Suspicion = 5;
        private const double ProbeShare = 0.1;
        private const double Mechanism = 3;
        private const double Tolerance = 0.01;
        private const double Bite = 0.01;
        private const double Pull = 1e6;
        private const double ThinShare = 0.3;
        private const double EdgeShare = 0.05;
        private const double OuterShare = 0.25;
        private const double EmbeddedVolume = 0.001;

        private struct State
        {
            public bool Open, Slip;
            public int Side1, Side2;
        }

        private sealed class Joint
        {
            public int Link;
            public Vec3 Normal, T1, T2, Centre;
            public double A1, A2;
            public double Friction;
            public double Reference;
            public bool Edges, Held;
            public State Now, Before, Released;
            public double Rank;

            public double Force, Shear, Turning, Tipping, Sliding, E1, E2;
        }

        private readonly StructureModel _model;
        private readonly List<Joint> _joints = new List<Joint>();
        private readonly List<int>[] _bodyLinks;
        private readonly List<Joint> _applied = new List<Joint>();
        private double[] _start;
        private double[] _steady;
        private double[] _normal;
        private bool _probing;
        private bool _settling;
        private bool _edgesOpened;

        public JointRelease[] Releases { get; }
        public double Residual { get; private set; } = FrameAnalysis.DefaultResidual;
        public bool[] Gone { get; }
        public int Rounds { get; private set; }
        public bool Settled { get; private set; } = true;
        public bool Active => _joints.Count > 0;

        public Masonry(StructureModel model)
        {
            _model = model;
            Releases = new JointRelease[model.Links.Count];
            Gone = new bool[model.Bodies.Count];
            _bodyLinks = new List<int>[model.Bodies.Count];
            for (int i = 0; i < _bodyLinks.Length; i++)
                _bodyLinks[i] = new List<int>();
            for (int li = 0; li < model.Links.Count; li++)
            {
                Link link = model.Links[li];
                _bodyLinks[link.A].Add(li);
                if (!link.IsGround)
                    _bodyLinks[link.B].Add(li);
                Joint joint = Build(model, li);
                if (joint != null)
                    _joints.Add(joint);
            }
        }

        public bool Next(FrameAnalysis frame)
        {
            if (_settling)
                return false;
            if (_start == null)
            {
                _start = new double[_model.Bodies.Count];
                for (int i = 0; i < _start.Length; i++)
                    _start[i] = Motion(frame, i);
                _steady = (double[])_start.Clone();
            }
            else if (_probing)
            {
                _probing = false;
                Residual = FrameAnalysis.DefaultResidual;
                if (Moving(frame))
                {
                    if (Rounds < MaxSolves && (Hold(frame) || Retry(frame)))
                    {
                        Rounds++;
                        return true;
                    }
                    Collapse(frame);
                    _settling = true;
                    Rounds++;
                    return true;
                }
                _steady = _normal;
            }
            else
            {
                _normal = new double[_model.Bodies.Count];
                for (int i = 0; i < _normal.Length; i++)
                    _normal[i] = Motion(frame, i);
                if (Rounds < MaxSolves && Suspicious())
                {
                    _probing = true;
                    Residual = FrameAnalysis.DefaultResidual * ProbeShare;
                    Rounds++;
                    return true;
                }
                _steady = _normal;
            }
            if (Rounds >= MaxSolves)
            {
                Settled = false;
                return false;
            }
            if (!_edgesOpened)
            {
                _edgesOpened = true;
                if (OpenEdges())
                {
                    Rounds++;
                    return true;
                }
            }
            if (!Decide(frame))
                return false;
            Rounds++;
            return true;
        }

        public void Finish(FrameAnalysis frame, BodyResult[] results)
        {
            if (_start == null)
                return;
            foreach (Joint joint in _joints)
            {
                Link link = _model.Links[joint.Link];
                if (joint.Now.Open || joint.Edges || Gone[link.A] || Gone[link.B])
                    continue;
                Measure(joint, frame.LinkLoad(joint.Link));
                if (joint.Force <= 0)
                    continue;
                double sliding = joint.Now.Slip ? 0 : joint.Sliding;
                double weight = Math.Min(1, joint.Force / joint.Reference);
                StressMode mode = joint.Tipping >= sliding ? StressMode.Tipping : StressMode.Sliding;
                double utilization = Math.Min(JointLimit, Math.Max(joint.Tipping, sliding) * weight);
                Raise(results, link.A, utilization, mode);
                Raise(results, link.B, utilization, mode);
            }

            for (int i = 0; i < results.Length; i++)
            {
                if (!Gone[i] || results[i].Mode == StressMode.Unsupported)
                    continue;
                results[i].Utilization = Math.Max(results[i].Utilization, UnstableUtilization);
                results[i].Mode = StressMode.Unstable;
            }
        }

        private bool OpenEdges()
        {
            _applied.Clear();
            bool any = false;
            foreach (Joint joint in _joints)
            {
                if (!joint.Edges || joint.Now.Open)
                    continue;
                joint.Now = new State { Open = true };
                Releases[joint.Link] = Release(joint);
                any = true;
            }
            return any;
        }

        private bool Decide(FrameAnalysis frame)
        {
            foreach (Joint joint in _joints)
            {
                Link link = _model.Links[joint.Link];
                joint.Rank = 0;
                joint.Released = joint.Now;
                if (joint.Edges || joint.Now.Open || Gone[link.A] || Gone[link.B])
                    continue;

                double slack = Tolerance * joint.Reference;
                Measure(joint, frame.LinkLoad(joint.Link));
                if (joint.Held)
                {
                    if (!joint.Now.Slip && joint.Force > slack && (joint.Sliding - 1) * joint.Friction * joint.Force > slack)
                    {
                        joint.Released = new State { Slip = true, Side1 = joint.Now.Side1, Side2 = joint.Now.Side2 };
                        joint.Rank = joint.Sliding;
                    }
                    continue;
                }
                if (joint.Force < -slack ||
                    joint.Force <= slack && joint.Turning > slack * Math.Max(joint.A1, joint.A2))
                {
                    joint.Released = new State { Open = true };
                    joint.Rank = Pull - joint.Force / joint.Reference;
                    continue;
                }
                if (joint.Force <= slack)
                {
                    if (!joint.Now.Slip && joint.Shear > slack)
                    {
                        joint.Released = new State { Slip = true, Side1 = joint.Now.Side1, Side2 = joint.Now.Side2 };
                        joint.Rank = Pull;
                    }
                    continue;
                }

                State released = joint.Now;
                if (released.Side1 == 0 && (Math.Abs(joint.E1) - joint.A1) * joint.Force > slack * joint.A1)
                {
                    released.Side1 = Math.Sign(joint.E1);
                    joint.Rank = joint.Tipping;
                }
                if (released.Side2 == 0 && (Math.Abs(joint.E2) - joint.A2) * joint.Force > slack * joint.A2)
                {
                    released.Side2 = Math.Sign(joint.E2);
                    joint.Rank = joint.Tipping;
                }
                if (!released.Slip && (joint.Sliding - 1) * joint.Friction * joint.Force > slack)
                {
                    released.Slip = true;
                    joint.Rank = Math.Max(joint.Rank, joint.Sliding);
                }
                joint.Released = released;
            }

            _applied.Clear();
            foreach (Joint joint in _joints)
            {
                if (joint.Rank <= 0)
                    continue;
                joint.Before = joint.Now;
                joint.Now = joint.Released;
                Releases[joint.Link] = Release(joint);
                _applied.Add(joint);
            }
            return _applied.Count > 0;
        }

        private bool Hold(FrameAnalysis frame)
        {
            bool any = false;
            foreach (Joint joint in _joints)
            {
                Link link = _model.Links[joint.Link];
                if (joint.Edges || joint.Held || Gone[link.A] || Gone[link.B])
                    continue;
                if (!joint.Now.Open && joint.Now.Side1 == 0 && joint.Now.Side2 == 0)
                    continue;
                if (!Unstable(frame, link.A) && !Unstable(frame, link.B))
                    continue;
                frame.LinkMotion(joint.Link, out Vec3 delta, out Vec3 turn);
                Vec3 point = frame.LinkLoad(joint.Link).Point;
                double scale = Motion(frame, link.A) + Motion(frame, link.B);
                int pressed = 0, first = -1, last = -1;
                for (int c = 0; c < 4; c++)
                {
                    Vec3 corner = joint.Centre + joint.T1 * (Sign(c, 1) * joint.A1) + joint.T2 * (Sign(c, 2) * joint.A2);
                    if (Vec3.Dot(joint.Normal, delta + Vec3.Cross(turn, corner - point)) >= -Bite * scale)
                        continue;
                    pressed++;
                    if (first < 0)
                        first = c;
                    last = c;
                }
                if (pressed == 0)
                    continue;
                var held = new State { Slip = joint.Now.Slip };
                if (joint.Now.Open && pressed == 1)
                {
                    held.Side1 = Sign(first, 1);
                    held.Side2 = Sign(first, 2);
                }
                else if (joint.Now.Open && pressed == 2 && (first & 1) == (last & 1))
                    held.Side1 = Sign(first, 1);
                else if (joint.Now.Open && pressed == 2 && (first & 2) == (last & 2))
                    held.Side2 = Sign(first, 2);
                joint.Now = held;
                joint.Held = true;
                Releases[joint.Link] = Release(joint);
                any = true;
            }
            if (any)
                _applied.Clear();
            return any;
        }

        private static int Sign(int corner, int bit) => (corner & bit) == 0 ? -1 : 1;

        private bool Retry(FrameAnalysis frame)
        {
            if (_applied.Count < 2)
                return false;
            Joint best = null;
            foreach (Joint joint in _applied)
            {
                Link link = _model.Links[joint.Link];
                if (!Unstable(frame, link.A) && !Unstable(frame, link.B))
                    continue;
                joint.Now = joint.Before;
                Releases[joint.Link] = Release(joint);
                if (best == null || joint.Rank > best.Rank)
                    best = joint;
            }
            _applied.Clear();
            if (best == null)
                return false;
            best.Now = best.Released;
            Releases[best.Link] = Release(best);
            _applied.Add(best);
            return true;
        }

        private bool Suspicious()
        {
            for (int i = 0; i < Gone.Length; i++)
                if (!Gone[i] && _normal[i] > Suspicion * _steady[i] + 1e-9)
                    return true;
            return false;
        }

        private bool Moving(FrameAnalysis frame)
        {
            for (int i = 0; i < Gone.Length; i++)
                if (!Gone[i] && Unstable(frame, i))
                    return true;
            return false;
        }

        private bool Unstable(FrameAnalysis frame, int body) =>
            !Gone[body] && Motion(frame, body) > Mechanism * _normal[body] + 1e-9;

        private void Collapse(FrameAnalysis frame)
        {
            var fallen = new List<int>();
            for (int i = 0; i < Gone.Length; i++)
                if (Unstable(frame, i))
                    fallen.Add(i);
            foreach (int i in fallen)
            {
                Gone[i] = true;
                foreach (int li in _bodyLinks[i])
                    Releases[li] = new JointRelease { Open = true };
            }
            _applied.Clear();
        }

        private static double Share(double e, double a) => a > 1e-6 ? Math.Abs(e) / a : 0;

        private static void Raise(BodyResult[] results, int body, double utilization, StressMode mode)
        {
            if (results[body].Mode == StressMode.Unsupported || utilization <= results[body].Utilization)
                return;
            results[body].Utilization = utilization;
            results[body].Mode = mode;
        }

        private double Motion(FrameAnalysis frame, int body)
        {
            Body b = _model.Bodies[body];
            return frame.TranslationOf(body).Length + frame.RotationOf(body).Length * b.Size.Length / 2;
        }

        private static void Measure(Joint joint, ContactLoad load)
        {
            Vec3 moment = load.Moment + Vec3.Cross(load.Point - joint.Centre, load.Force);
            double normal = -Vec3.Dot(load.Force, joint.Normal);
            joint.Force = normal;
            joint.Shear = (load.Force + joint.Normal * normal).Length;
            joint.Turning = (moment - joint.Normal * Vec3.Dot(moment, joint.Normal)).Length;
            if (normal <= 0)
            {
                joint.E1 = joint.E2 = joint.Tipping = joint.Sliding = 0;
                return;
            }
            joint.E1 = Vec3.Dot(moment, joint.T2) / normal;
            joint.E2 = -Vec3.Dot(moment, joint.T1) / normal;
            joint.Tipping = Math.Max(joint.Now.Side1 != 0 ? 0 : Share(joint.E1, joint.A1),
                                     joint.Now.Side2 != 0 ? 0 : Share(joint.E2, joint.A2));
            joint.Sliding = joint.Shear / (joint.Friction * normal);
        }

        private JointRelease Release(Joint joint)
        {
            State state = joint.Now;
            if (state.Open)
                return new JointRelease { Open = true };
            var release = new JointRelease();
            if (state.Side1 != 0 || state.Side2 != 0)
            {
                Vec3 edge = joint.Centre + joint.T1 * (state.Side1 * joint.A1) + joint.T2 * (state.Side2 * joint.A2);
                Vec3 reach = (state.Side1 != 0 ? Vec3.Zero : joint.T1 * joint.A1) +
                             (state.Side2 != 0 ? Vec3.Zero : joint.T2 * joint.A2);
                Link link = _model.Links[joint.Link];
                release.Moved = true;
                release.Point = edge;
                release.HingeA = state.Side1 != 0 ? joint.T2 : Vec3.Zero;
                release.HingeB = state.Side2 != 0 ? joint.T1 : Vec3.Zero;
                release.SpanA = LocalSpan(_model.Bodies[link.A], edge - reach, edge + reach);
                release.SpanB = LocalSpan(_model.Bodies[link.B], edge - reach, edge + reach);
            }
            if (state.Slip)
            {
                release.SlipA = joint.T1;
                release.SlipB = joint.T2;
            }
            return release;
        }

        private static double[] LocalSpan(Body body, Vec3 from, Vec3 to)
        {
            var span = new double[6];
            for (int k = 0; k < 3; k++)
            {
                double half = body.Size[k] / 2;
                double a = Vec3.Dot(from - body.Center, body.Axis(k)), b = Vec3.Dot(to - body.Center, body.Axis(k));
                span[2 * k] = Math.Max(-half, Math.Min(half, Math.Min(a, b)));
                span[2 * k + 1] = Math.Max(-half, Math.Min(half, Math.Max(a, b)));
            }
            return span;
        }

        private static Joint Build(StructureModel model, int li)
        {
            Link link = model.Links[li];
            if (link.IsGround || link.SpanA == null || link.SpanB == null)
                return null;
            Body a = model.Bodies[link.A], b = model.Bodies[link.B];
            if (a.Material == null || b.Material == null || !a.Material.Masonry || !b.Material.Masonry)
                return null;
            bool faceA = Face(a, link.SpanA, out Vec3 outA, out int thinA);
            bool faceB = Face(b, link.SpanB, out Vec3 outB, out int thinB);
            Vec3 n;
            bool edges = false;
            bool cut = a.Fitted != b.Fitted && Embedded(a, b);
            if (cut && a.Fitted)
            {
                if (!faceB)
                    return null;
                n = -outB;
            }
            else if (cut)
            {
                if (!faceA)
                    return null;
                n = outA;
            }
            else
            {
                if (!faceA || !faceB)
                    return null;
                n = thinA < thinB ? outA : thinB < thinA ? -outB : outA - outB;
                edges = thinA > 1 && thinB > 1;
            }
            if (n.LengthSquared < 1e-12)
                return null;
            n = n.Normalized();
            int pick = 0;
            double least = double.MaxValue;
            for (int k = 0; k < 3; k++)
            {
                double d = Math.Abs(Vec3.Dot(a.Axis(k), n));
                if (d < least - 1e-9)
                {
                    least = d;
                    pick = k;
                }
            }
            Vec3 t1 = (a.Axis(pick) - n * Vec3.Dot(a.Axis(pick), n)).Normalized();
            Vec3 t2 = Vec3.Cross(n, t1);

            Project(a, link.SpanA, t1, out double lo1, out double hi1);
            Project(b, link.SpanB, t1, out double lo1B, out double hi1B);
            Project(a, link.SpanA, t2, out double lo2, out double hi2);
            Project(b, link.SpanB, t2, out double lo2B, out double hi2B);
            Project(a, link.SpanA, n, out double loN, out double hiN);
            Project(b, link.SpanB, n, out double loNB, out double hiNB);
            Overlap(ref lo1, ref hi1, lo1B, hi1B);
            Overlap(ref lo2, ref hi2, lo2B, hi2B);

            return new Joint
            {
                Link = li,
                Normal = n,
                T1 = t1,
                T2 = t2,
                Centre = t1 * ((lo1 + hi1) / 2) + t2 * ((lo2 + hi2) / 2) + n * ((loN + hiN + loNB + hiNB) / 4),
                A1 = (hi1 - lo1) / 2,
                A2 = (hi2 - lo2) / 2,
                Friction = Math.Min(a.Material.Friction, b.Material.Friction),
                Reference = Math.Max(1e-6, Math.Min(a.Mass, b.Mass) * model.Gravity.Length),
                Edges = edges,
            };
        }

        private static bool Embedded(Body a, Body b) =>
            Structure.Overlap.Of(Box(a), Box(b), out Vec3 _, out double volume) && volume > EmbeddedVolume;

        private static OrientedBox Box(Body body) => new OrientedBox
        {
            Center = body.Center, AxisX = body.AxisX, AxisY = body.AxisY, AxisZ = body.AxisZ, Half = body.Size * 0.5,
        };

        private static bool Face(Body body, double[] span, out Vec3 outward, out int thin)
        {
            outward = Vec3.Zero;
            thin = 0;
            int primary = -1;
            double best = double.MaxValue;
            for (int k = 0; k < 3; k++)
            {
                double share = (span[2 * k + 1] - span[2 * k]) / Math.Max(body.Size[k], 1e-6);
                if (share < best - 1e-9)
                {
                    best = share;
                    primary = k;
                }
            }
            if (best > ThinShare)
                return false;
            for (int k = 0; k < 3; k++)
            {
                double share = (span[2 * k + 1] - span[2 * k]) / Math.Max(body.Size[k], 1e-6);
                if (k != primary && share > EdgeShare)
                    continue;
                double mid = (span[2 * k] + span[2 * k + 1]) / 2;
                if (Math.Abs(mid) < OuterShare * body.Size[k])
                {
                    if (k == primary)
                        return false;
                    continue;
                }
                outward += body.Axis(k) * Math.Sign(mid);
                thin++;
            }
            outward = outward.Normalized();
            return true;
        }

        private static void Project(Body body, double[] span, Vec3 direction, out double lo, out double hi)
        {
            lo = hi = Vec3.Dot(body.Center, direction);
            for (int k = 0; k < 3; k++)
            {
                double d = Vec3.Dot(body.Axis(k), direction);
                double a = d * span[2 * k], b = d * span[2 * k + 1];
                lo += Math.Min(a, b);
                hi += Math.Max(a, b);
            }
        }

        private static void Overlap(ref double lo, ref double hi, double otherLo, double otherHi)
        {
            lo = Math.Max(lo, otherLo);
            hi = Math.Min(hi, otherHi);
            if (lo > hi)
                lo = hi = (lo + hi) / 2;
        }
    }
}
