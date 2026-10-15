using System;
using System.Collections.Generic;
using Fysik.Structure;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fysik.Game
{
    internal sealed class FittedStone : MonoBehaviour
    {
        public readonly List<MeshFilter> Filters = new List<MeshFilter>();
        public readonly List<Mesh> Originals = new List<Mesh>();
        public readonly List<Collider> Hidden = new List<Collider>();
        public readonly List<MeshCollider> Added = new List<MeshCollider>();
        public readonly List<Mesh> Created = new List<Mesh>();

        public bool Cut => Filters.Count > 0 || Added.Count > 0;

        public void Restore()
        {
            for (int i = 0; i < Filters.Count; i++)
                if (Filters[i] != null)
                    Filters[i].sharedMesh = Originals[i];
            foreach (Collider c in Hidden)
                if (c != null)
                    c.enabled = true;
            foreach (MeshCollider c in Added)
                if (c != null)
                    DestroyImmediate(c);
            Release();
            Filters.Clear();
            Originals.Clear();
            Hidden.Clear();
            Added.Clear();
        }

        private void Release()
        {
            foreach (Mesh m in Created)
            {
                ArchFit.Forget(m);
                if (m != null)
                    Destroy(m);
            }
            Created.Clear();
        }

        private void OnDestroy() => Release();
    }

    internal static class ArchFit
    {
        private const float Margin = 0.002f;
        private const float Gap = 0.005f;
        private const int MaxDepth = 4;
        private const double BudgetMs = 2;

        private struct Wedge
        {
            public Matrix4x4 ToLocal;
            public Matrix4x4 ToWorld;
            public ArchStone Stone;
            public bool Outward;
            public bool Open;
            public bool Deep;
        }

        private static readonly Queue<WearNTear> s_around = new Queue<WearNTear>();
        private static readonly Queue<WearNTear> s_queue = new Queue<WearNTear>();
        private static readonly HashSet<WearNTear> s_queued = new HashSet<WearNTear>();
        private static readonly Collider[] s_hits = new Collider[64];
        private static readonly Dictionary<Mesh, float> s_volumes = new Dictionary<Mesh, float>();
        private static readonly List<Wedge> s_wedges = new List<Wedge>();
        private static readonly HashSet<Transform> s_seen = new HashSet<Transform>();
        private static int s_mask;
        private static bool s_visual;
        private const float DepthSlack = 0.02f;
        private const float RoughShare = 0.5f;

        public static bool TryVolume(Collider collider, out float volume)
        {
            volume = 0;
            return collider is MeshCollider mesh && mesh.sharedMesh != null && s_volumes.TryGetValue(mesh.sharedMesh, out volume);
        }

        public static void Forget(Mesh mesh)
        {
            if (mesh != null)
                s_volumes.Remove(mesh);
        }

        private static bool Dedicated => ZNet.instance != null && ZNet.instance.IsDedicated();

        public static void Appeared(WearNTear wnt)
        {
            if (Dedicated)
                return;
            if (ArchStones.TryGet(wnt.gameObject, out ArchStone _))
                s_around.Enqueue(wnt);
            else if (Stone(wnt))
                Queue(wnt);
        }

        public static void Destroyed(WearNTear wnt)
        {
            if (!Dedicated && ArchStones.TryGet(wnt.gameObject, out ArchStone _))
                QueueAround(wnt);
        }

        public static void Clear()
        {
            s_around.Clear();
            s_queue.Clear();
            s_queued.Clear();
        }

        public static void Tick()
        {
            while (s_around.Count > 0)
            {
                WearNTear stone = s_around.Dequeue();
                if (stone != null)
                    QueueAround(stone);
            }
            long deadline = System.Diagnostics.Stopwatch.GetTimestamp() +
                            (long)(BudgetMs * System.Diagnostics.Stopwatch.Frequency / 1000);
            while (s_queue.Count > 0 && System.Diagnostics.Stopwatch.GetTimestamp() < deadline)
            {
                WearNTear wnt = s_queue.Dequeue();
                s_queued.Remove(wnt);
                if (wnt != null && wnt.m_nview != null && wnt.m_nview.IsValid() && Eligible(wnt))
                    Fit(wnt);
            }
        }

        private static bool Stone(WearNTear wnt) =>
            wnt.m_materialType == WearNTear.MaterialType.Stone && wnt.m_supports && !ArchStones.TryGet(wnt.gameObject, out ArchStone _);

        private static bool Eligible(WearNTear wnt) => Stone(wnt) && wnt.m_piece != null && wnt.m_piece.GetCreator() != 0;

        private static void Queue(WearNTear wnt)
        {
            if (s_queued.Add(wnt))
                s_queue.Enqueue(wnt);
        }

        private static void EnsureMask()
        {
            if (s_mask == 0)
                s_mask = LayerMask.GetMask("piece");
        }

        private static void QueueAround(WearNTear stone)
        {
            EnsureMask();
            foreach (Collider c in stone.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger)
                    continue;
                Bounds b = c.bounds;
                int count = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * 0.1f, s_hits, Quaternion.identity, s_mask);
                for (int i = 0; i < count; i++)
                {
                    WearNTear other = s_hits[i].GetComponentInParent<WearNTear>();
                    if (other != null && other != stone && other.m_nview != null && other.m_nview.GetZDO() != null && Stone(other))
                        Queue(other);
                }
            }
        }

        private static void Fit(WearNTear wnt)
        {
            FittedStone fitted = wnt.GetComponent<FittedStone>();
            bool wasCut = fitted != null && fitted.Cut;
            fitted?.Restore();
            FindWedges(wnt);
            bool cut = false;
            if (s_wedges.Count > 0)
            {
                if (fitted == null)
                    fitted = wnt.gameObject.AddComponent<FittedStone>();
                cut = Cut(wnt, fitted);
            }
            if (wasCut || cut)
                StructureManager.Instance.Reshape(wnt);
        }

        private static void FindWedges(WearNTear wnt)
        {
            EnsureMask();
            s_wedges.Clear();
            s_seen.Clear();
            foreach (BoxCollider box in wnt.GetComponentsInChildren<BoxCollider>())
            {
                if (!box.enabled || box.isTrigger)
                    continue;
                Transform t = box.transform;
                Vector3 centre = t.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f;
                int count = Physics.OverlapBoxNonAlloc(centre, Abs(half), s_hits, t.rotation, s_mask);
                for (int i = 0; i < count; i++)
                {
                    Collider hit = s_hits[i];
                    if (!ArchStones.TryGet(hit, out ArchStone stone) || !s_seen.Add(hit.transform))
                        continue;
                    WearNTear owner = hit.GetComponentInParent<WearNTear>();
                    if (owner == null || owner == wnt)
                        continue;
                    Matrix4x4 toLocal = hit.transform.worldToLocalMatrix;
                    Vector3 p = toLocal.MultiplyPoint3x4(centre);
                    var local = new Vec3(p.x, p.y, p.z);
                    s_wedges.Add(new Wedge
                    {
                        ToLocal = toLocal,
                        ToWorld = hit.transform.localToWorldMatrix,
                        Stone = stone,
                        Outward = stone.Shape.Beyond(local),
                        Open = stone.Shape.InSector(local),
                        Deep = Voussoir.InDepth(local, stone.Depth),
                    });
                }
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static bool Cut(WearNTear wnt, FittedStone fitted)
        {
            bool moved = false;
            var meshes = new List<MeshFilter>();
            var cuts = new List<Mesh>();
            foreach (MeshFilter filter in wnt.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                if (source == null || !source.isReadable)
                    continue;
                meshes.Add(filter);
                s_visual = true;
                cuts.Add(Carve(source, filter.transform.localToWorldMatrix, ref moved));
            }
            var boxes = new List<BoxCollider>();
            var shapes = new List<Mesh>();
            foreach (BoxCollider box in wnt.GetComponentsInChildren<BoxCollider>())
            {
                if (!box.enabled || box.isTrigger)
                    continue;
                Mesh solid = Solid(box);
                boxes.Add(box);
                s_visual = false;
                shapes.Add(Carve(solid, box.transform.localToWorldMatrix, ref moved, Bounds(box)));
                UnityEngine.Object.Destroy(solid);
            }
            if (!moved)
            {
                foreach (Mesh m in cuts)
                    UnityEngine.Object.Destroy(m);
                foreach (Mesh m in shapes)
                    UnityEngine.Object.Destroy(m);
                return false;
            }

            for (int i = 0; i < meshes.Count; i++)
            {
                fitted.Filters.Add(meshes[i]);
                fitted.Originals.Add(meshes[i].sharedMesh);
                meshes[i].sharedMesh = cuts[i];
                fitted.Created.Add(cuts[i]);
            }
            foreach (LODGroup group in wnt.GetComponentsInChildren<LODGroup>(true))
                group.RecalculateBounds();
            for (int i = 0; i < boxes.Count; i++)
            {
                boxes[i].enabled = false;
                fitted.Hidden.Add(boxes[i]);
                var collider = boxes[i].gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = shapes[i];
                fitted.Added.Add(collider);
                fitted.Created.Add(shapes[i]);
                s_volumes[shapes[i]] = Volume(shapes[i], boxes[i].transform.lossyScale);
            }
            return true;
        }

        private static float Volume(Mesh mesh, Vector3 scale)
        {
            Vector3[] vertices = mesh.vertices;
            var points = new List<Vec3>(vertices.Length);
            foreach (Vector3 v in vertices)
                points.Add(new Vec3(v.x * scale.x, v.y * scale.y, v.z * scale.z));
            return (float)MeshVolume.Of(points, mesh.triangles);
        }

        private static Mesh Solid(BoxCollider box)
        {
            Vector3 c = box.center, h = box.size * 0.5f;
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            var mesh = new Mesh { vertices = vertices };
            mesh.triangles = new[]
            {
                0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
            };
            return mesh;
        }

        private static OrientedBox Bounds(BoxCollider box)
        {
            Transform t = box.transform;
            Vector3 c = t.TransformPoint(box.center), h = Vector3.Scale(box.size, t.lossyScale) * 0.5f;
            Vector3 x = t.right, y = t.up, z = t.forward;
            return new OrientedBox
            {
                Center = new Vec3(c.x, c.y, c.z),
                AxisX = new Vec3(x.x, x.y, x.z),
                AxisY = new Vec3(y.x, y.y, y.z),
                AxisZ = new Vec3(z.x, z.y, z.z),
                Half = new Vec3(Mathf.Abs(h.x), Mathf.Abs(h.y), Mathf.Abs(h.z)),
            };
        }

        private static Mesh Carve(Mesh source, Matrix4x4 toWorld, ref bool moved, OrientedBox? keep = null)
        {
            var local = new List<Vector3>();
            source.GetVertices(local);
            var world = new List<Vec3>(local.Count);
            foreach (Vector3 v in local)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(v);
                world.Add(new Vec3(w.x, w.y, w.z));
            }
            var carver = new MeshCarver(world, WedgeOf, MaxDepth);
            var submeshes = new List<List<int>>();
            for (int s = 0; s < source.subMeshCount; s++)
                submeshes.Add(carver.Split(source.GetTriangles(s)));
            moved |= carver.Push(Push);
            if (keep.HasValue)
                carver.Clamp(keep.Value);

            Matrix4x4 toLocal = toWorld.inverse;
            int count = carver.Points.Count;
            var vertices = new List<Vector3>(count);
            foreach (Vec3 p in carver.Points)
                vertices.Add(toLocal.MultiplyPoint3x4(new Vector3((float)p.X, (float)p.Y, (float)p.Z)));
            var mesh = new Mesh { name = source.name + "_fitted" };
            if (count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            List<Vector3> normals = Grow(source.normals, carver, (a, b) => (a + b).normalized);
            if (normals != null)
                mesh.SetNormals(normals);
            List<Vector4> tangents = Grow(source.tangents, carver, (a, b) =>
            {
                Vector3 t = ((Vector3)a + (Vector3)b).normalized;
                return new Vector4(t.x, t.y, t.z, a.w);
            });
            if (tangents != null)
                mesh.SetTangents(tangents);
            List<Vector2> uv = Grow(source.uv, carver, (a, b) => (a + b) * 0.5f);
            if (uv != null)
                mesh.SetUVs(0, uv);
            List<Vector2> uv2 = Grow(source.uv2, carver, (a, b) => (a + b) * 0.5f);
            if (uv2 != null)
                mesh.SetUVs(1, uv2);
            List<Color> colors = Grow(source.colors, carver, (a, b) => Color.Lerp(a, b, 0.5f));
            if (colors != null)
                mesh.SetColors(colors);
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++)
                mesh.SetTriangles(submeshes[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<T> Grow<T>(T[] values, MeshCarver carver, Func<T, T, T> middle)
        {
            if (values == null || values.Length != carver.OriginalCount)
                return null;
            var grown = new List<T>(values);
            for (int i = 0; i < carver.FirstParent.Count; i++)
                grown.Add(middle(grown[carver.FirstParent[i]], grown[carver.SecondParent[i]]));
            return grown;
        }

        private static int WedgeOf(Vec3 world)
        {
            var point = new Vector3((float)world.X, (float)world.Y, (float)world.Z);
            for (int i = 0; i < s_wedges.Count; i++)
            {
                Wedge w = s_wedges[i];
                Vector3 p = w.ToLocal.MultiplyPoint3x4(point);
                bool inside = s_visual
                    ? w.Stone.Shape.Contains(new Vec3(p.x, p.y, p.z), w.Stone.Depth, Margin, w.Open, w.Deep,
                                             w.Stone.OuterGrow * RoughShare, w.Stone.InnerGrow * RoughShare,
                                             w.Stone.DepthGrow + DepthSlack)
                    : w.Stone.Shape.Contains(new Vec3(p.x, p.y, p.z), w.Stone.Depth, Margin, w.Open, w.Deep);
                if (inside)
                    return i;
            }
            return -1;
        }

        private static Vec3 Push(Vec3 world, int wedge)
        {
            Wedge w = s_wedges[wedge];
            Vector3 p = w.ToLocal.MultiplyPoint3x4(new Vector3((float)world.X, (float)world.Y, (float)world.Z));
            float grow = s_visual ? (w.Outward ? w.Stone.OuterGrow : w.Stone.InnerGrow) * RoughShare : 0;
            Vec3 q = w.Stone.Shape.ToFace(new Vec3(p.x, p.y, p.z), w.Outward, Gap + grow);
            Vector3 pushed = w.ToWorld.MultiplyPoint3x4(new Vector3((float)q.X, (float)q.Y, (float)q.Z));
            return new Vec3(pushed.x, pushed.y, pushed.z);
        }
    }
}
