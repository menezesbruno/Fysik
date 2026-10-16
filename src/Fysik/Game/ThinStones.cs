using System;
using System.Collections.Generic;
using System.Linq;
using Fysik.Structure;
using Jotunn.Managers;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class ThinStone
    {
        public const float Thickness = 0.5f;

        public readonly bool Floor;
        public readonly string Size;
        public readonly string Base;
        public readonly string Prefab;
        public readonly string Token;
        public readonly Vector3 Target;

        private ThinStone(bool floor, string size, string source, Vector3 target)
        {
            Floor = floor;
            Size = size;
            Base = source;
            Prefab = $"fysik_thin_stone_{(floor ? "floor" : "wall")}_{size}";
            Token = Prefab;
            Target = target;
        }

        public static ThinStone Wall(string size) => new ThinStone(false, size, "stone_wall_" + size, new Vector3(float.NaN, float.NaN, Thickness));

        public static ThinStone Slab(string size, string source, float width) => new ThinStone(true, size, source, new Vector3(width, Thickness, width));
    }

    internal static class ThinStones
    {
        private static readonly ThinStone[] s_stones =
        {
            ThinStone.Wall("1x1"),
            ThinStone.Wall("2x1"),
            ThinStone.Wall("4x2"),
            ThinStone.Slab("2x2", "stone_floor_2x2", 2),
            ThinStone.Slab("1x1", "stone_floor_2x2", 1),
        };

        public static IEnumerable<ThinStone> All => s_stones;

        private const float TopFacing = 0.7f;
        private const float OnFace = 0.05f;

        private static readonly Dictionary<string, ThinStone> s_byPrefab = new Dictionary<string, ThinStone>();

        public static bool TryGet(GameObject gameObject, out ThinStone stone)
        {
            stone = null;
            return gameObject != null && s_byPrefab.TryGetValue(Utils.GetPrefabName(gameObject), out stone);
        }

        public static void Rest(Player player)
        {
            GameObject ghost = player.m_placementGhost;
            if (ghost == null || !ghost.activeSelf || !TryGet(ghost, out ThinStone floor) || !floor.Floor)
                return;
            if (!player.PieceRayTest(out Vector3 point, out Vector3 normal, out Piece piece, out Heightmap _, out Collider _, false))
                return;
            if (piece == null || normal.y < TopFacing)
                return;
            BoxCollider top = TopOf(piece, point);
            if (top == null)
                return;
            Transform t = top.transform;
            Vector3 hit = t.InverseTransformPoint(point);
            Vector3 eye = t.InverseTransformPoint(GameCamera.instance != null ? GameCamera.instance.transform.position : player.transform.position);
            Vector3 half = top.size * 0.5f;
            bool lengthX = half.x >= half.z;
            float side = lengthX ? eye.z - top.center.z : eye.x - top.center.x;
            Vec3 rest = WallTop.Rest(ToVec(top.center), ToVec(half), floor.Target.x, ThinStone.Thickness, ToVec(hit), side);
            ghost.transform.SetPositionAndRotation(t.TransformPoint(new Vector3((float)rest.X, (float)rest.Y, (float)rest.Z)), t.rotation);
        }

        private static BoxCollider TopOf(Piece piece, Vector3 point)
        {
            foreach (BoxCollider box in piece.GetComponentsInChildren<BoxCollider>())
            {
                if (!box.enabled || box.isTrigger || (box.transform.lossyScale - Vector3.one).sqrMagnitude > 1e-4f)
                    continue;
                Vector3 local = box.transform.InverseTransformPoint(point) - box.center;
                Vector3 half = box.size * 0.5f;
                if (Mathf.Abs(local.y - half.y) > OnFace || Mathf.Abs(local.x) > half.x + OnFace || Mathf.Abs(local.z) > half.z + OnFace)
                    continue;
                if (WallTop.Bears(ToVec(half)))
                    return box;
            }
            return null;
        }

        private static Vec3 ToVec(Vector3 v) => new Vec3(v.x, v.y, v.z);

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
            PieceManager.OnPiecesRegistered += Order;
        }

        private static void Order()
        {
            StonePieces.Place(s_stones.Where(s => !s.Floor).Select(s => s.Prefab), "stone_wall_4x2");
            StonePieces.Place(s_stones.Where(s => s.Floor).Select(s => s.Prefab), "stone_floor_2x2");
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            foreach (ThinStone stone in s_stones)
            {
                try
                {
                    Create(stone);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Thin stone piece {stone.Prefab} not added: {e.Message}");
                }
            }
        }

        private static void Create(ThinStone stone)
        {
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(stone.Prefab, stone.Base);
            if (prefab == null)
                throw new InvalidOperationException($"{stone.Base} not found");
            Transform root = prefab.transform;
            BoxCollider[] boxes = prefab.GetComponentsInChildren<BoxCollider>(true);
            if (boxes.Length == 0)
                throw new InvalidOperationException($"{stone.Base} has no box collider");
            Vector3 extent = Extent(boxes, root);
            var scale = new Vector3(Factor(stone.Target.x, extent.x), Factor(stone.Target.y, extent.y), Factor(stone.Target.z, extent.z));
            Matrix4x4 squeeze = Matrix4x4.Scale(scale);

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    filter.sharedMesh = MeshShaper.Deform(filter.sharedMesh, StonePieces.ToRoot(root, filter.transform),
                                                          stone.Prefab + "_" + filter.sharedMesh.name,
                                                          p => Vector3.Scale(p, scale), p => squeeze);
            foreach (LODGroup group in prefab.GetComponentsInChildren<LODGroup>(true))
                group.RecalculateBounds();
            foreach (BoxCollider box in boxes)
                Squeeze(box, root, scale);
            foreach (Transform child in root)
                if (child.CompareTag("snappoint"))
                    child.localPosition = Vector3.Scale(child.localPosition, scale);

            StonePieces.ScaleRecipe(prefab.GetComponent<Piece>(), scale.x * scale.y * scale.z);
            StonePieces.Add(prefab, stone.Token, StonePieces.Render(prefab, RenderManager.IsometricRotation));
            s_byPrefab[stone.Prefab] = stone;
        }

        private static float Factor(float target, float extent) => float.IsNaN(target) || extent <= 0 ? 1 : target / extent;

        private static Vector3 Extent(IEnumerable<BoxCollider> boxes, Transform root)
        {
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (BoxCollider box in boxes)
            {
                Matrix4x4 m = StonePieces.ToRoot(root, box.transform);
                Vector3 centre = m.MultiplyPoint3x4(box.center);
                Vector3 s = box.size * 0.5f;
                var half = new Vector3(
                    Mathf.Abs(m.m00) * s.x + Mathf.Abs(m.m01) * s.y + Mathf.Abs(m.m02) * s.z,
                    Mathf.Abs(m.m10) * s.x + Mathf.Abs(m.m11) * s.y + Mathf.Abs(m.m12) * s.z,
                    Mathf.Abs(m.m20) * s.x + Mathf.Abs(m.m21) * s.y + Mathf.Abs(m.m22) * s.z);
                min = Vector3.Min(min, centre - half);
                max = Vector3.Max(max, centre + half);
            }
            return max - min;
        }

        private static void Squeeze(BoxCollider box, Transform root, Vector3 scale)
        {
            Matrix4x4 m = StonePieces.ToRoot(root, box.transform);
            box.center = m.inverse.MultiplyPoint3x4(Vector3.Scale(m.MultiplyPoint3x4(box.center), scale));
            Vector3 size = box.size;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 column = m.GetColumn(axis);
                float most = Mathf.Max(Mathf.Abs(column.x), Mathf.Max(Mathf.Abs(column.y), Mathf.Abs(column.z)));
                if (most <= 0)
                    continue;
                size[axis] *= Mathf.Abs(column.x) == most ? scale.x : Mathf.Abs(column.y) == most ? scale.y : scale.z;
            }
            box.size = size;
        }
    }
}
