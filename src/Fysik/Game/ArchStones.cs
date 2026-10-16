using System;
using System.Collections.Generic;
using System.Linq;
using Fysik.Structure;
using Jotunn.Managers;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class ArchStone
    {
        public string Prefab;
        public string Base;
        public string Token;
        public int Span;
        public Voussoir Shape;
        public float Depth;
        public float OuterGrow, InnerGrow, DepthGrow;

        public ArchStone(int span, int stones)
        {
            Span = span;
            Prefab = $"fysik_arch_stone_{span}m";
            Base = "stone_wall_1x1";
            Token = $"fysik_arch_stone_{span}m";
            Shape = Voussoir.HalfCircle(stones, span / 2.0, span / 2.0 + 1);
            Depth = 1;
        }

        public Vector3 BoxSize => new Vector3((float)Shape.MeanWidth, (float)Shape.Height, Depth);

        public float Narrowing(float y) => (float)Shape.Narrowing(y);
    }

    internal static class ArchStones
    {
        private const string Neighbour = "stone_arch";

        private static readonly ArchStone[] s_stones =
        {
            new ArchStone(4, 9),
            new ArchStone(6, 13),
            new ArchStone(8, 15),
            new ArchStone(10, 19),
            new ArchStone(12, 23),
            new ArchStone(14, 25),
            new ArchStone(16, 29),
        };

        public static IEnumerable<ArchStone> All => s_stones;

        private static readonly Dictionary<string, ArchStone> s_byPrefab = new Dictionary<string, ArchStone>();
        private static readonly Dictionary<Mesh, ArchStone> s_byCollider = new Dictionary<Mesh, ArchStone>();
        private static readonly List<Piece> s_nearby = new List<Piece>();
        private static readonly List<Transform> s_snaps = new List<Transform>();
        private const float SnapDistance = 0.5f;
        private const double YawStep = 22.5;
        private const double IconNarrowest = 0.2;
        private const double IconWidest = 0.8;
        private static readonly Quaternion IconView = Quaternion.Euler(0, 20, 0);

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
            PieceManager.OnPiecesRegistered += Order;
        }

        private static void Order() => StonePieces.Place(s_stones.Select(s => s.Prefab), Neighbour, s_stones[0].Base);

        public static bool TryGet(GameObject gameObject, out ArchStone stone)
        {
            stone = null;
            return gameObject != null && s_byPrefab.TryGetValue(Utils.GetPrefabName(gameObject), out stone);
        }

        public static bool TryGet(Collider collider, out ArchStone stone)
        {
            stone = null;
            return collider is MeshCollider mesh && mesh.sharedMesh != null && s_byCollider.TryGetValue(mesh.sharedMesh, out stone);
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            foreach (ArchStone stone in s_stones)
            {
                try
                {
                    Create(stone);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Arch stone {stone.Prefab} not added: {e.Message}");
                }
            }
        }

        private static void Create(ArchStone stone)
        {
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(stone.Prefab, stone.Base);
            if (prefab == null)
                throw new InvalidOperationException($"{stone.Base} not found");
            Transform root = prefab.transform;
            BoxCollider[] boxes = prefab.GetComponents<BoxCollider>();
            if (boxes.Length == 0)
                throw new InvalidOperationException($"{stone.Base} has no box collider");
            Vector3 cube = boxes[0].size;

            List<MeshFilter> filters = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToList();
            List<Mesh> originals = filters.Select(f => f.sharedMesh).ToList();
            foreach (MeshFilter filter in filters)
                filter.sharedMesh = Reshape(filter.sharedMesh, stone, cube, root, filter.transform);
            foreach (LODGroup group in prefab.GetComponentsInChildren<LODGroup>(true))
                group.RecalculateBounds();
            Measure(prefab, stone);
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                if (child.CompareTag("snappoint"))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);

            foreach (BoxCollider box in boxes)
                UnityEngine.Object.DestroyImmediate(box);
            Mesh shape = Wedge(stone);
            var collider = prefab.AddComponent<MeshCollider>();
            collider.sharedMesh = shape;
            collider.convex = true;
            s_byCollider[shape] = stone;

            ArchStone icon = IconShape(stone);
            List<Mesh> shapes = filters.Select((f, i) => Reshape(originals[i], icon, cube, root, f.transform)).ToList();
            StonePieces.Add(prefab, stone.Token, StonePieces.Render(prefab, IconView, filters, shapes));
            s_byPrefab[stone.Prefab] = stone;
        }

        private static ArchStone IconShape(ArchStone stone)
        {
            int i = Array.IndexOf(s_stones, stone);
            double inner = IconNarrowest + (IconWidest - IconNarrowest) * i / (s_stones.Length - 1);
            Voussoir real = stone.Shape;
            double outerRadius = real.Height / (1 - inner);
            double angle = 2 * Math.Atan(real.Width / (2 * outerRadius)) * 180 / Math.PI;
            return new ArchStone(stone.Span, real.Stones) { Shape = new Voussoir(angle, real.Width, real.Height) };
        }

        private static void Measure(GameObject prefab, ArchStone stone)
        {
            Transform root = prefab.transform;
            float top = (float)stone.Shape.Height / 2, half = stone.Depth / 2;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                    continue;
                Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p = toRoot.MultiplyPoint3x4(v);
                    stone.OuterGrow = Mathf.Max(stone.OuterGrow, p.y - top);
                    stone.InnerGrow = Mathf.Max(stone.InnerGrow, -top - p.y);
                    stone.DepthGrow = Mathf.Max(stone.DepthGrow, Mathf.Abs(p.z) - half);
                }
            }
        }

        private static Vector3 Reshape(Vector3 p, ArchStone stone, Vector3 cube)
        {
            float y = p.y / cube.y * (float)stone.Shape.Height;
            float x = p.x / cube.x * (float)stone.Shape.Width * stone.Narrowing(y);
            return new Vector3(x, y, p.z / cube.z * stone.Depth);
        }

        private static Mesh Reshape(Mesh source, ArchStone stone, Vector3 cube, Transform root, Transform part)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix * part.localToWorldMatrix;
            Matrix4x4 fromRoot = toRoot.inverse;
            Mesh mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = stone.Prefab + "_" + source.name;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            float b = (float)stone.Shape.Height / cube.y, d = stone.Depth / cube.z;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = toRoot.MultiplyPoint3x4(vertices[i]);
                Vector3 w = Reshape(v, stone, cube);
                float a = (float)stone.Shape.Width / cube.x * stone.Narrowing(w.y);
                float c = v.x / cube.x * (float)stone.Shape.Width * b / (float)stone.Shape.OuterRadius;
                vertices[i] = fromRoot.MultiplyPoint3x4(w);
                if (normals.Length == vertices.Length)
                {
                    Vector3 n = toRoot.MultiplyVector(normals[i]);
                    n = new Vector3(n.x / a, n.y / b - c * n.x / (a * b), n.z / d);
                    normals[i] = fromRoot.MultiplyVector(n).normalized;
                }
                if (tangents.Length == vertices.Length)
                {
                    Vector4 t4 = tangents[i];
                    Vector3 t = toRoot.MultiplyVector(new Vector3(t4.x, t4.y, t4.z));
                    t = new Vector3(a * t.x + c * t.y, b * t.y, d * t.z);
                    t = fromRoot.MultiplyVector(t).normalized;
                    tangents[i] = new Vector4(t.x, t.y, t.z, t4.w);
                }
            }
            mesh.vertices = vertices;
            if (normals.Length == vertices.Length)
                mesh.normals = normals;
            if (tangents.Length == vertices.Length)
                mesh.tangents = tangents;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Wedge(ArchStone stone)
        {
            float top = (float)stone.Shape.Height / 2, outer = (float)stone.Shape.Width / 2;
            float inner = (float)stone.Shape.InnerWidth / 2, depth = stone.Depth / 2;
            var mesh = new Mesh { name = stone.Prefab + "_collider" };
            mesh.vertices = new[]
            {
                new Vector3(-inner, -top, -depth), new Vector3(inner, -top, -depth),
                new Vector3(outer, top, -depth), new Vector3(-outer, top, -depth),
                new Vector3(-inner, -top, depth), new Vector3(inner, -top, depth),
                new Vector3(outer, top, depth), new Vector3(-outer, top, depth),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Straighten(Transform t, ArchStone stone)
        {
            Vector3 e = t.rotation.eulerAngles;
            if (Angles.TrySnap(e.x, 0, 360, out double x) && Angles.TrySnap(e.y, 0, YawStep, out double y) &&
                Angles.TrySnap(e.z, stone.Shape.SpringerTurn, stone.Shape.Angle, out double z))
                t.rotation = Quaternion.Euler((float)x, (float)y, (float)z);
        }

        public static void Continue(Player player)
        {
            GameObject ghost = player.m_placementGhost;
            if (ghost == null || !ghost.activeSelf || !TryGet(ghost, out ArchStone stone))
                return;
            if (!player.PieceRayTest(out Vector3 point, out Vector3 _, out Piece piece, out Heightmap _, out Collider _, false))
                return;
            if (piece == null || !TryGet(piece.gameObject, out ArchStone placed) || placed != stone)
            {
                Spring(player, ghost, stone, point);
                return;
            }
            Transform t = piece.transform;
            int side = t.InverseTransformPoint(point).x >= 0 ? 1 : -1;
            Vec3 next = stone.Shape.Next(side);
            Quaternion rotation = Quaternion.AngleAxis((float)stone.Shape.Turn(side), Vector3.forward);
            ghost.transform.SetPositionAndRotation(t.TransformPoint(new Vector3((float)next.X, (float)next.Y, (float)next.Z)),
                                                   t.rotation * rotation);
        }

        private static void Spring(Player player, GameObject ghost, ArchStone stone, Vector3 point)
        {
            Quaternion rotation = ghost.transform.rotation * Quaternion.AngleAxis((float)stone.Shape.SpringerTurn, Vector3.forward);
            Vec3 face = stone.Shape.SpringingFace;
            Vector3 position = point - rotation * new Vector3((float)face.X, (float)face.Y, (float)face.Z);
            s_snaps.Clear();
            s_nearby.Clear();
            Piece.GetSnapPoints(point, 3f, s_snaps, s_nearby);
            float inner = (float)stone.Shape.InnerWidth / 2, outer = (float)stone.Shape.Width / 2;
            float top = (float)stone.Shape.Height / 2, depth = stone.Depth / 2;
            Vector3 shift = Vector3.zero;
            float best = SnapDistance;
            foreach (Vector3 corner in new[]
                     {
                         new Vector3(-inner, -top, -depth), new Vector3(-inner, -top, depth),
                         new Vector3(-outer, top, -depth), new Vector3(-outer, top, depth),
                     })
            {
                Vector3 world = position + rotation * corner;
                foreach (Transform snap in s_snaps)
                {
                    if (snap == null)
                        continue;
                    float distance = Vector3.Distance(snap.position, world);
                    if (distance < best)
                    {
                        best = distance;
                        shift = snap.position - world;
                    }
                }
            }
            ghost.transform.SetPositionAndRotation(position + shift, rotation);
        }
    }
}
