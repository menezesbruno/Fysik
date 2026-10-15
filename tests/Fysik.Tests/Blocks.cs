using System;
using System.Collections.Generic;
using Fysik.Structure;

namespace Fysik.Tests
{
    internal sealed class Blocks
    {
        public const double Margin = 0.15;

        public static readonly MaterialProps Masonry = new MaterialProps
        {
            Name = "masonry",
            Density = 2400,
            Elasticity = 30e9,
            ShearModulus = 12e9,
            CompressiveStrength = 20e6,
            TensileStrength = 0.8e6,
            ShearStrength = 2e6,
            BucklingSlenderness = 30,
            Masonry = true,
        };

        private readonly List<Body> _bodies = new List<Body>();
        private readonly List<int> _removed = new List<int>();

        private int[] _index;

        public int Count => _bodies.Count;

        public int this[int block] => _index[block];

        public int Add(Vec3 centre, Vec3 size, MaterialProps material = null, double turn = 0, bool fitted = false)
        {
            double a = turn * Math.PI / 180;
            var x = new Vec3(Math.Cos(a), Math.Sin(a), 0);
            var y = new Vec3(-Math.Sin(a), Math.Cos(a), 0);
            Body body = Structures.Box(centre, size, material ?? Masonry, x, y);
            body.Fitted = fitted;
            _bodies.Add(body);
            return _bodies.Count - 1;
        }

        public void Remove(int body) => _removed.Add(body);

        public StructureModel Build(double ground = 0)
        {
            var model = new StructureModel();
            _index = new int[_bodies.Count];
            var boxes = new List<OrientedBox>();
            for (int i = 0; i < _bodies.Count; i++)
            {
                _index[i] = -1;
                if (_removed.Contains(i))
                    continue;
                _index[i] = model.AddBody(_bodies[i]);
                boxes.Add(BoxOf(_bodies[i], 0));
            }

            var region = new List<Vec3>();
            var slab = new OrientedBox
            {
                Center = new Vec3(0, ground - 50, 0), AxisX = Vec3.UnitX, AxisY = Vec3.UnitY, AxisZ = Vec3.UnitZ,
                Half = new Vec3(1000, 50, 1000),
            };
            for (int i = 0; i < model.Bodies.Count; i++)
            {
                OrientedBox frame = boxes[i];
                for (int j = i + 1; j < model.Bodies.Count; j++)
                {
                    if (!Overlap.Of(Grown(frame), Grown(boxes[j]), out Vec3 centroid, out double _, region))
                        continue;
                    double[] spanA = ContactSpan.Open(), spanB = ContactSpan.Open();
                    ContactSpan.Include(spanA, frame, region);
                    ContactSpan.Include(spanB, boxes[j], region);
                    model.Connect(i, j, centroid, ContactSpan.Close(spanA, frame, Margin), ContactSpan.Close(spanB, boxes[j], Margin));
                }
                if (Overlap.Of(Grown(frame), slab, out Vec3 point, out double _, region))
                {
                    double[] span = ContactSpan.Open();
                    ContactSpan.Include(span, frame, region);
                    model.Ground(i, point, ContactSpan.Close(span, frame, Margin));
                }
            }
            return model;
        }

        public static BodyResult[] Solve(StructureModel model, out StructureAnalysis analysis)
        {
            analysis = new StructureAnalysis(model, 5000);
            analysis.RunToCompletion(() => false);
            return analysis.Results;
        }

        private static OrientedBox BoxOf(Body body, double grow) => new OrientedBox
        {
            Center = body.Center, AxisX = body.AxisX, AxisY = body.AxisY, AxisZ = body.AxisZ,
            Half = body.Size * 0.5 + new Vec3(grow, grow, grow),
        };

        private static OrientedBox Grown(OrientedBox box) => new OrientedBox
        {
            Center = box.Center, AxisX = box.AxisX, AxisY = box.AxisY, AxisZ = box.AxisZ,
            Half = box.Half + new Vec3(Margin, Margin, Margin),
        };
    }
}
