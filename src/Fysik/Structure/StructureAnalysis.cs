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
        private readonly Masonry _masonry;
        private FrameAnalysis _frame;
        private List<ContactLoad>[] _loads;
        private BodyResult[] _pending;
        private int _evaluated;
        private int _earlierIterations;

        public SolverKind Solver { get; }
        public BodyResult[] Results { get; private set; }
        public bool Finished => Results != null;
        public int Iterations => _earlierIterations + (_frame?.Iterations ?? 0);
        public bool Converged => _frame?.Converged ?? true;
        public int MasonryRounds => _masonry?.Rounds ?? 0;
        public bool MasonrySettled => _masonry?.Settled ?? true;

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
                var masonry = new Masonry(model);
                if (masonry.Active)
                    _masonry = masonry;
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
                if (_masonry != null && _masonry.Next(_frame))
                {
                    _earlierIterations += _frame.Iterations;
                    _frame = new FrameAnalysis(_model, DefaultTolerance, 0, _masonry.Releases, _frame.Solution,
                                               _masonry.Residual, _masonry.Gone);
                    return false;
                }
                _loads = _frame.ContactLoads();
                _pending = new BodyResult[_model.Bodies.Count];
                return false;
            }

            int end = Math.Min(_pending.Length, _evaluated + units * BodiesPerStep);
            for (; _evaluated < end; _evaluated++)
                _pending[_evaluated] = Mechanics.Evaluate(_model.Bodies[_evaluated], _loads[_evaluated], _model.Gravity);
            if (_evaluated == _pending.Length)
            {
                _masonry?.Finish(_frame, _pending);
                Results = _pending;
            }
            return Finished;
        }
    }
}
