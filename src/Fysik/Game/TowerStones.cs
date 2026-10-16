using System;
using System.Collections.Generic;
using System.Linq;
using Fysik.Structure;
using Jotunn.Managers;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class TowerStone
    {
        public const double Step = 22.5;
        public const int PerRing = 16;
        private const double LongStone = 1.5;

        public readonly int Diameter;
        public readonly bool Thin;
        public readonly bool Half;
        public readonly TowerRing Ring;
        public readonly string Prefab;
        public readonly string Token;
        public readonly string Base;
        public float OuterGrow, InnerGrow, DepthGrow;

        public TowerStone(int diameter, bool thin, bool half)
        {
            Diameter = diameter;
            Thin = thin;
            Half = half;
            Ring = new TowerRing(diameter / 2.0, thin ? 0.5 : 1, half ? Step / 2 : Step);
            Prefab = $"fysik{(thin ? "_thin" : "")}_tower_stone_{diameter}m{(half ? "_half" : "")}";
            Token = Prefab;
            Base = Ring.MeanWidth > LongStone ? "stone_wall_2x1" : "stone_wall_1x1";
        }

        public Vector3 BoxSize => new Vector3((float)Ring.MeanWidth, (float)Ring.Height, (float)Ring.BoxDepth);

        public Quaternion BoxRotation => Quaternion.Euler(0, (float)Ring.BoxTurn, 0);

        public Vector3 BoxOffset => BoxRotation * new Vector3(0, 0, (float)Ring.BoxShift);

        public float Fill => (float)(Ring.Thickness / Ring.BoxDepth);

        public bool SameRing(TowerStone other) => other.Diameter == Diameter && other.Thin == Thin;
    }

    internal static class TowerStones
    {
        private const int ArcPoints = 4;
        private const int SubdivisionsPerStone = 8;
        private const float TopFacing = 0.7f;
        private const float Reach = 1f;
        private const double IconMostTurn = 90;
        private const double IconLeastTurn = 30;

        private static readonly int[] s_diameters = { 4, 6, 8, 10 };

        private static readonly TowerStone[] s_stones = s_diameters
            .SelectMany(d => new[]
            {
                new TowerStone(d, false, false), new TowerStone(d, false, true), new TowerStone(d, true, false), new TowerStone(d, true, true),
            })
            .ToArray();

        public static IEnumerable<TowerStone> All => s_stones;

        private static readonly Dictionary<string, TowerStone> s_byPrefab = new Dictionary<string, TowerStone>();
        private static readonly Dictionary<Mesh, TowerStone> s_byCollider = new Dictionary<Mesh, TowerStone>();
        private static readonly Collider[] s_hits = new Collider[64];
        private static int s_mask;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
            PieceManager.OnPiecesRegistered += Order;
        }

        private static void Order()
        {
            var after = ArchStones.All.Reverse().Select(a => a.Prefab).ToList();
            after.Add("stone_arch");
            StonePieces.Place(s_stones.Select(s => s.Prefab), after.ToArray());
        }

        public static bool TryGet(GameObject gameObject, out TowerStone stone)
        {
            stone = null;
            return gameObject != null && s_byPrefab.TryGetValue(Utils.GetPrefabName(gameObject), out stone);
        }

        public static bool TryGet(Collider collider, out TowerStone stone)
        {
            stone = null;
            return collider is MeshCollider mesh && mesh.sharedMesh != null && s_byCollider.TryGetValue(mesh.sharedMesh, out stone);
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            foreach (TowerStone stone in s_stones)
            {
                try
                {
                    Create(stone);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Tower stone {stone.Prefab} not added: {e.Message}");
                }
            }
        }

        private static void Create(TowerStone stone)
        {
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(stone.Prefab, stone.Base);
            if (prefab == null)
                throw new InvalidOperationException($"{stone.Base} not found");
            Transform root = prefab.transform;
            BoxCollider[] boxes = prefab.GetComponents<BoxCollider>();
            if (boxes.Length == 0)
                throw new InvalidOperationException($"{stone.Base} has no box collider");
            Vector3 cube = boxes[0].size;
            List<Transform> snaps = root.Cast<Transform>().Where(c => c.CompareTag("snappoint")).ToList();
            if (snaps.Count == 0)
                throw new InvalidOperationException($"{stone.Base} has no snap points");

            List<MeshFilter> filters = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToList();
            List<Mesh> originals = filters.Select(f => f.sharedMesh).ToList();
            foreach (MeshFilter filter in filters)
                filter.sharedMesh = Shape(filter.sharedMesh, stone.Prefab, stone.Ring, cube, root, filter.transform);
            foreach (LODGroup group in prefab.GetComponentsInChildren<LODGroup>(true))
                group.RecalculateBounds();
            Measure(filters, root, stone);

            foreach (Vec3 corner in Corners(stone.Ring))
            {
                Transform snap = UnityEngine.Object.Instantiate(snaps[0], root);
                snap.name = snaps[0].name;
                snap.localPosition = ToVector(corner);
                snap.localRotation = Quaternion.identity;
            }
            foreach (Transform old in snaps)
                UnityEngine.Object.DestroyImmediate(old.gameObject);

            foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
                UnityEngine.Object.DestroyImmediate(box);
            Mesh shape = Hull(stone);
            var collider = prefab.AddComponent<MeshCollider>();
            collider.sharedMesh = shape;
            collider.convex = true;
            s_byCollider[shape] = stone;

            StonePieces.ScaleRecipe(prefab.GetComponent<Piece>(), stone.Ring.Volume / (cube.x * cube.y * cube.z));
            TowerRing ring = stone.Ring;
            TowerRing icon = IconRing(stone);
            List<Mesh> shapes = filters.Select((f, i) => Shape(originals[i], stone.Prefab + "_icon", icon, cube, root, f.transform)).ToList();
            Quaternion view = Quaternion.Euler(55, 0, 0) * Quaternion.Euler(0, -(float)icon.Angle / 2, 0);
            StonePieces.Add(prefab, stone.Token, StonePieces.Render(prefab, view, filters, shapes));
            s_byPrefab[stone.Prefab] = stone;
        }

        private static TowerRing IconRing(TowerStone stone)
        {
            int i = Array.IndexOf(s_diameters, stone.Diameter);
            double turn = IconMostTurn - (IconMostTurn - IconLeastTurn) * i / (s_diameters.Length - 1);
            if (stone.Half)
                turn /= 2;
            TowerRing ring = stone.Ring;
            double midRadius = ring.MeanWidth / (turn * Math.PI / 180);
            return new TowerRing(midRadius + ring.Thickness / 2, ring.Thickness, turn, ring.Height);
        }

        private static void Measure(List<MeshFilter> filters, Transform root, TowerStone stone)
        {
            TowerRing ring = stone.Ring;
            foreach (MeshFilter filter in filters)
            {
                Matrix4x4 toRoot = StonePieces.ToRoot(root, filter.transform);
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p = toRoot.MultiplyPoint3x4(v);
                    double r = ring.RadiusOf(new Vec3(p.x, p.y, p.z));
                    stone.OuterGrow = Mathf.Max(stone.OuterGrow, (float)(r - ring.OuterRadius));
                    stone.InnerGrow = Mathf.Max(stone.InnerGrow, (float)(ring.InnerRadius - r));
                    stone.DepthGrow = Mathf.Max(stone.DepthGrow, Mathf.Abs(p.y) - (float)ring.Height / 2);
                }
            }
        }

        private static IEnumerable<Vec3> Corners(TowerRing ring)
        {
            foreach (double angle in new[] { 0, ring.Angle })
                foreach (double radius in new[] { ring.InnerRadius, ring.OuterRadius })
                    foreach (double y in new[] { -ring.Height / 2, ring.Height / 2 })
                        yield return ring.Point(angle, radius, y);
        }

        private static Vector3 ToVector(Vec3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        private static Mesh Shape(Mesh source, string name, TowerRing ring, Vector3 cube, Transform root, Transform part) =>
            MeshShaper.Deform(source, StonePieces.ToRoot(root, part), name + "_" + source.name,
                              p => Bend(p, ring, cube), p => Jacobian(p, ring, cube), cube.x / SubdivisionsPerStone);

        private static double AngleAt(Vector3 p, TowerRing ring, Vector3 cube) => (p.x / cube.x + 0.5) * ring.Angle;

        private static double RadiusAt(Vector3 p, TowerRing ring, Vector3 cube) => ring.MidRadius + p.z / cube.z * ring.Thickness;

        private static Vector3 Bend(Vector3 p, TowerRing ring, Vector3 cube) =>
            ToVector(ring.Point(AngleAt(p, ring, cube), RadiusAt(p, ring, cube), p.y / cube.y * ring.Height));

        private static Matrix4x4 Jacobian(Vector3 p, TowerRing ring, Vector3 cube)
        {
            double a = AngleAt(p, ring, cube) * Math.PI / 180, r = RadiusAt(p, ring, cube);
            double along = r * ring.Angle * Math.PI / 180 / cube.x, across = ring.Thickness / cube.z;
            var tangent = new Vector3((float)(Math.Cos(a) * along), 0, (float)(-Math.Sin(a) * along));
            var up = new Vector3(0, (float)(ring.Height / cube.y), 0);
            var radial = new Vector3((float)(Math.Sin(a) * across), 0, (float)(Math.Cos(a) * across));
            return MeshShaper.Columns(tangent, up, radial);
        }

        private static Mesh Hull(TowerStone stone)
        {
            TowerRing ring = stone.Ring;
            var plan = new List<Vec3> { ring.Point(0, ring.InnerRadius, 0) };
            for (int i = 0; i <= ArcPoints; i++)
                plan.Add(ring.Point(ring.Angle * i / ArcPoints, ring.OuterRadius, 0));
            plan.Add(ring.Point(ring.Angle, ring.InnerRadius, 0));
            int n = plan.Count;
            var vertices = new Vector3[2 * n];
            float half = (float)ring.Height / 2;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = ToVector(plan[i]);
                vertices[i] = p + Vector3.down * half;
                vertices[n + i] = p + Vector3.up * half;
            }
            var triangles = new List<int>();
            for (int i = 1; i < n - 1; i++)
            {
                triangles.AddRange(new[] { 0, i, i + 1 });
                triangles.AddRange(new[] { n, n + i + 1, n + i });
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                triangles.AddRange(new[] { i, n + i, j });
                triangles.AddRange(new[] { j, n + i, n + j });
            }
            var mesh = new Mesh { name = stone.Prefab + "_collider", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Continue(Player player)
        {
            GameObject ghost = player.m_placementGhost;
            if (ghost == null || !ghost.activeSelf || !TryGet(ghost, out TowerStone stone))
                return;
            if (!player.PieceRayTest(out Vector3 point, out Vector3 normal, out Piece piece, out Heightmap _, out Collider _, false))
                return;
            Transform t;
            TowerStone placed;
            int side;
            bool above = false;
            if (piece != null && TryGet(piece.gameObject, out placed))
            {
                t = piece.transform;
                above = t.InverseTransformDirection(normal).y > TopFacing;
                if (!above && !placed.SameRing(stone))
                    return;
                Vector3 hit = t.InverseTransformPoint(point);
                side = placed.Ring.SideOf(new Vec3(hit.x, hit.y, hit.z));
            }
            else if (!Beside(stone, point, out t, out placed, out side))
            {
                return;
            }
            double start = placed.Ring.Next(stone.Ring, side, above, stone.Half);
            Vec3 origin = placed.Ring.Origin(stone.Ring, start, above);
            ghost.transform.SetPositionAndRotation(t.TransformPoint(ToVector(origin)), t.rotation * Quaternion.Euler(0, (float)start, 0));
        }

        private static bool Beside(TowerStone stone, Vector3 point, out Transform near, out TowerStone placed, out int side)
        {
            near = null;
            placed = null;
            side = 0;
            if (s_mask == 0)
                s_mask = LayerMask.GetMask("piece");
            float best = Reach;
            int count = Physics.OverlapSphereNonAlloc(point, Reach + (float)stone.Ring.MeanWidth, s_hits, s_mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!TryGet(s_hits[i], out TowerStone other) || !other.SameRing(stone))
                    continue;
                Transform t = s_hits[i].transform;
                foreach (int s in new[] { -1, 1 })
                {
                    Vec3 end = other.Ring.Point(s > 0 ? other.Ring.Angle : 0, other.Ring.MidRadius, -other.Ring.Height / 2);
                    float distance = Vector3.Distance(t.TransformPoint(ToVector(end)), point);
                    if (distance >= best)
                        continue;
                    best = distance;
                    near = t;
                    placed = other;
                    side = s;
                }
            }
            return near != null;
        }

        public static void Straighten(Transform t)
        {
            Vector3 e = t.rotation.eulerAngles;
            if (Angles.TrySnap(e.x, 0, 360, out double x) && Angles.TrySnap(e.y, 0, TowerStone.Step / 2, out double y) &&
                Angles.TrySnap(e.z, 0, 360, out double z))
                t.rotation = Quaternion.Euler((float)x, (float)y, (float)z);
        }
    }
}
