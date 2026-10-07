using System;
using System.Collections.Generic;

namespace Fysik.Structure
{
    public enum SolverKind : byte
    {
        None,
        Frame,
        LoadPath,
    }

    public sealed class StructureAnalysis
    {
        public const double DefaultTolerance = 1e-6;

        private const int BodiesPerStep = 8;

        private readonly StructureModel _model;
        private readonly FrameAnalysis _frame;
        private List<ContactLoad>[] _loads;
        private BodyResult[] _pending;
        private int _evaluated;

        public SolverKind Solver { get; }
        public BodyResult[] Results { get; private set; }
        public bool Finished => Results != null;
        public int Iterations => _frame?.Iterations ?? 0;
        public bool Converged => _frame?.Converged ?? true;

        public StructureAnalysis(StructureModel model, int maxFrameBodies)
        {
            _model = model;
            bool grounded = false;
            foreach (Link l in model.Links)
                grounded |= l.IsGround;

            if (!grounded || model.Bodies.Count == 0)
            {
                Solver = SolverKind.None;
                Results = new BodyResult[model.Bodies.Count];
                for (int i = 0; i < Results.Length; i++)
                    Results[i] = BodyResult.Unsupported;
            }
            else if (model.Bodies.Count <= maxFrameBodies)
            {
                Solver = SolverKind.Frame;
                _frame = new FrameAnalysis(model, DefaultTolerance);
            }
            else
            {
                Solver = SolverKind.LoadPath;
            }
        }

        public void RunToCompletion(Func<bool> cancelled)
        {
            while (!Finished)
            {
                if (cancelled())
                    return;
                Step(16);
            }
        }

        public bool Step(int units)
        {
            if (Finished)
                return true;
            units = Math.Max(1, units);

            if (Solver == SolverKind.LoadPath)
            {
                Results = LoadPathAnalysis.Run(_model);
                return true;
            }

            if (_loads == null)
            {
                if (!_frame.Iterate(units))
                    return false;
                _loads = _frame.ContactLoads();
                _pending = new BodyResult[_model.Bodies.Count];
                return false;
            }

            int end = Math.Min(_pending.Length, _evaluated + units * BodiesPerStep);
            for (; _evaluated < end; _evaluated++)
                _pending[_evaluated] = Mechanics.Evaluate(_model.Bodies[_evaluated], _loads[_evaluated], _model.Gravity);
            if (_evaluated == _pending.Length)
                Results = _pending;
            return Finished;
        }
    }
}
