using System;
using System.Collections.Generic;
using System.Linq;
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
        public readonly List<LODGroup> Groups = new List<LODGroup>();
        public readonly List<LOD[]> Lods = new List<LOD[]>();
        public readonly List<Renderer> Silenced = new List<Renderer>();

        public bool Cut => Filters.Count > 0 || Added.Count > 0;

        public Mesh Original(MeshFilter filter)
        {
            int i = Filters.IndexOf(filter);
            return i >= 0 ? Originals[i] : filter.sharedMesh;
        }

        public void Restore()
        {
            for (int i = 0; i < Groups.Count; i++)
                if (Groups[i] != null)
                    Groups[i].SetLODs(Lods[i]);
            foreach (Renderer r in Silenced)
                if (r != null)
                    r.enabled = true;
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
            Groups.Clear();
            Lods.Clear();
            Silenced.Clear();
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
        private const double CurvedEdge = 0.1;
        private const double CapEdge = 4;
        private const int MaxJobs = 2;
        private const double RoughSlack = 0.15;
        private const double ExactSlack = 0.02;

        private struct Wedge
        {
            public Matrix4x4 ToLocal;
            public Matrix4x4 ToWorld;
            public ArchStone Stone;
            public TowerStone Tower;
            public OrientedBox Block;
            public bool Outward;
            public bool Open;
            public bool Deep;

            public bool Contains(Vec3 p, bool visual)
            {
                if (Tower != null)
                    return visual
                        ? Tower.Ring.Claims(p, Outward, Margin, Open, Deep, Tower.OuterGrow * RoughShare, Tower.InnerGrow * RoughShare,
                                            Tower.DepthGrow + DepthSlack)
                        : Tower.Ring.Claims(p, Outward, Margin, Open, Deep);
                return visual
                    ? Stone.Shape.Contains(p, Stone.Depth, Margin, Open, Deep, Stone.OuterGrow * RoughShare, Stone.InnerGrow * RoughShare,
                                           Stone.DepthGrow + DepthSlack)
                    : Stone.Shape.Contains(p, Stone.Depth, Margin, Open, Deep);
            }

            public Vec3 ToFace(Vec3 p, bool visual)
            {
                if (Tower != null)
                    return Tower.Ring.ToFaceWithin(p, Block, Outward,
                                                   Gap + (visual ? (Outward ? Tower.OuterGrow : Tower.InnerGrow) * RoughShare : 0),
                                                   visual ? RoughSlack : ExactSlack);
                return Stone.Shape.ToFace(p, Outward, Gap + (visual ? (Outward ? Stone.OuterGrow : Stone.InnerGrow) * RoughShare : 0));
            }
        }

        private static readonly Queue<WearNTear> s_around = new Queue<WearNTear>();
        private static readonly Queue<WearNTear> s_queue = new Queue<WearNTear>();
        private static readonly HashSet<WearNTear> s_queued = new HashSet<WearNTear>();
        private static readonly Collider[] s_hits = new Collider[64];
        private static readonly Dictionary<Mesh, float> s_volumes = new Dictionary<Mesh, float>();
        private static readonly List<Wedge> s_wedges = new List<Wedge>();
        private static readonly HashSet<Transform> s_seen = new HashSet<Transform>();
        private static readonly Dictionary<WearNTear, Job> s_running = new Dictionary<WearNTear, Job>();
        private static readonly HashSet<WearNTear> s_again = new HashSet<WearNTear>();
        private static readonly List<WearNTear> s_done = new List<WearNTear>();
        private static int s_mask;

        private static readonly int[] BoxTriangles =
        {
            0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
        };

        private sealed class Shape
        {
            public MeshFilter Filter;
            public BoxCollider Box;
            public string Name;
            public Matrix4x4 ToWorld, ToLocal;
            public Vector3 Scale;
            public OrientedBox? Keep;
            public Vector3[] Vertices;
            public int[][] Triangles;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Vector2[] Uv, Uv2;
            public Color[] Colors;
            public bool Moved;
            public List<Vector3> CutVertices;
            public List<Vector3> CutNormals;
            public List<Vector4> CutTangents;
            public List<Vector2> CutUv, CutUv2;
            public List<Color> CutColors;
            public List<int>[] CutTriangles;
            public float Volume;
        }

        private sealed class Job
        {
            public WearNTear Piece;
            public Wedge[] Wedges;
            public readonly List<Shape> Shapes = new List<Shape>();
            public System.Threading.Tasks.Task Work;
        }
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
            if (Shaped(wnt))
                s_around.Enqueue(wnt);
            else if (Fits(wnt))
                Queue(wnt);
        }

        public static void Destroyed(WearNTear wnt)
        {
            if (!Dedicated && Shaped(wnt))
                QueueAround(wnt);
        }

        private static bool Shaped(WearNTear wnt) =>
            ArchStones.TryGet(wnt.gameObject, out ArchStone _) || TowerStones.TryGet(wnt.gameObject, out TowerStone _);

        public static void Clear()
        {
            s_around.Clear();
            s_queue.Clear();
            s_queued.Clear();
            s_running.Clear();
            s_again.Clear();
        }

        public static void Tick()
        {
            while (s_around.Count > 0)
            {
                WearNTear stone = s_around.Dequeue();
                if (stone != null)
                    QueueAround(stone);
            }
            Finish();
            long deadline = System.Diagnostics.Stopwatch.GetTimestamp() +
                            (long)(BudgetMs * System.Diagnostics.Stopwatch.Frequency / 1000);
            while (s_queue.Count > 0 && s_running.Count < MaxJobs && System.Diagnostics.Stopwatch.GetTimestamp() < deadline)
            {
                WearNTear wnt = s_queue.Dequeue();
                s_queued.Remove(wnt);
                if (wnt == null || wnt.m_nview == null || !wnt.m_nview.IsValid() || !Eligible(wnt))
                    continue;
                if (s_running.ContainsKey(wnt))
                    s_again.Add(wnt);
                else
                    Begin(wnt);
            }
        }

        private static bool Fits(WearNTear wnt) => PieceNode.HoldsUp(wnt) && (Masonry(wnt) || Timber(wnt)) && !Shaped(wnt);

        private static bool Masonry(WearNTear wnt) => wnt.m_materialType == WearNTear.MaterialType.Stone;

        private static bool Timber(WearNTear wnt)
        {
            if (wnt.m_materialType != WearNTear.MaterialType.Wood && wnt.m_materialType != WearNTear.MaterialType.HardWood ||
                wnt.GetComponent<Door>() != null)
                return false;
            FittedStone fitted = wnt.GetComponent<FittedStone>();
            bool any = false;
            foreach (BoxCollider box in wnt.GetComponentsInChildren<BoxCollider>())
            {
                if (box.isTrigger || !box.enabled && (fitted == null || !fitted.Hidden.Contains(box)))
                    continue;
                if (!Panel.Is(ToVec(Vector3.Scale(box.size, box.transform.lossyScale))))
                    return false;
                any = true;
            }
            return any;
        }

        private static bool Eligible(WearNTear wnt) => Fits(wnt) && wnt.m_piece != null && wnt.m_piece.GetCreator() != 0;

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
                    if (other != null && other != stone && other.m_nview != null && other.m_nview.GetZDO() != null && Fits(other))
                        Queue(other);
                }
            }
        }

        private static void Begin(WearNTear wnt)
        {
            FittedStone fitted = wnt.GetComponent<FittedStone>();
            var boxes = new List<BoxCollider>();
            foreach (BoxCollider box in wnt.GetComponentsInChildren<BoxCollider>())
                if (!box.isTrigger && (box.enabled || fitted != null && fitted.Hidden.Contains(box)))
                    boxes.Add(box);
            var job = new Job { Piece = wnt, Wedges = FindWedges(wnt, boxes) };
            if (job.Wedges.Length > 0)
            {
                foreach (MeshFilter filter in wnt.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh source = fitted != null ? fitted.Original(filter) : filter.sharedMesh;
                    if (source != null && source.isReadable)
                        job.Shapes.Add(Read(filter, source));
                }
                foreach (BoxCollider box in boxes)
                    job.Shapes.Add(Read(box));
            }
            s_running[wnt] = job;
            job.Work = System.Threading.Tasks.Task.Run(() =>
                System.Threading.Tasks.Parallel.ForEach(job.Shapes, shape => Carve(shape, job.Wedges)));
        }

        private static void Finish()
        {
            s_done.Clear();
            foreach (KeyValuePair<WearNTear, Job> running in s_running)
                if (running.Value.Work.IsCompleted)
                    s_done.Add(running.Key);
            foreach (WearNTear wnt in s_done)
            {
                Job job = s_running[wnt];
                s_running.Remove(wnt);
                if (job.Work.IsFaulted)
                    Plugin.Log.LogWarning($"Fysik could not fit {(wnt != null ? wnt.name : "a piece")}: {job.Work.Exception?.InnerException?.Message}");
                else if (wnt != null && wnt.m_nview != null && wnt.m_nview.IsValid())
                    Apply(job);
                if (s_again.Remove(wnt) && wnt != null)
                    Queue(wnt);
            }
        }

        private static void Apply(Job job)
        {
            WearNTear wnt = job.Piece;
            FittedStone fitted = wnt.GetComponent<FittedStone>();
            bool wasCut = fitted != null && fitted.Cut;
            fitted?.Restore();
            bool cut = job.Shapes.Any(s => s.Moved);
            if (cut)
            {
                if (fitted == null)
                    fitted = wnt.gameObject.AddComponent<FittedStone>();
                foreach (Shape shape in job.Shapes)
                {
                    if (shape.Filter == null)
                        continue;
                    Mesh mesh = Build(shape);
                    fitted.Filters.Add(shape.Filter);
                    fitted.Originals.Add(shape.Filter.sharedMesh);
                    shape.Filter.sharedMesh = mesh;
                    fitted.Created.Add(mesh);
                }
                foreach (LODGroup group in wnt.GetComponentsInChildren<LODGroup>(true))
                {
                    KeepNear(group, fitted);
                    group.RecalculateBounds();
                }
                foreach (Shape shape in job.Shapes)
                {
                    if (shape.Box == null)
                        continue;
                    Mesh mesh = Build(shape);
                    shape.Box.enabled = false;
                    fitted.Hidden.Add(shape.Box);
                    var collider = shape.Box.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    fitted.Added.Add(collider);
                    fitted.Created.Add(mesh);
                    s_volumes[mesh] = shape.Volume;
                }
            }
            if (wasCut || cut)
                StructureManager.Instance.Reshape(wnt);
        }

        private static Wedge[] FindWedges(WearNTear wnt, List<BoxCollider> boxes)
        {
            EnsureMask();
            s_wedges.Clear();
            s_seen.Clear();
            bool masonry = Masonry(wnt);
            var corners = new List<Vector3>();
            Vector3 middle = Vector3.zero;
            foreach (BoxCollider box in boxes)
            {
                corners.AddRange(Corners(box));
                middle += box.transform.TransformPoint(box.center) / boxes.Count;
            }
            foreach (BoxCollider box in boxes)
            {
                Transform t = box.transform;
                Vector3 centre = t.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f;
                int count = Physics.OverlapBoxNonAlloc(centre, Abs(half), s_hits, t.rotation, s_mask);
                for (int i = 0; i < count; i++)
                {
                    Collider hit = s_hits[i];
                    TowerStone tower = null;
                    bool arch = ArchStones.TryGet(hit, out ArchStone stone);
                    if (arch && !masonry || !arch && !TowerStones.TryGet(hit, out tower))
                        continue;
                    if (!s_seen.Add(hit.transform))
                        continue;
                    WearNTear owner = hit.GetComponentInParent<WearNTear>();
                    if (owner == null || owner == wnt)
                        continue;
                    Matrix4x4 toLocal = hit.transform.worldToLocalMatrix;
                    var wedge = new Wedge { ToLocal = toLocal, ToWorld = hit.transform.localToWorldMatrix, Stone = stone, Tower = tower };
                    if (arch)
                    {
                        Vec3 local = ToVec(toLocal.MultiplyPoint3x4(centre));
                        wedge.Outward = stone.Shape.Beyond(local);
                        wedge.Open = stone.Shape.InSector(local);
                        wedge.Deep = Voussoir.InDepth(local, stone.Depth);
                    }
                    else
                    {
                        Vec3 local = ToVec(toLocal.MultiplyPoint3x4(middle));
                        List<Vec3> locals = corners.Select(c => ToVec(toLocal.MultiplyPoint3x4(c))).ToList();
                        wedge.Outward = tower.Ring.Beyond(local);
                        wedge.Open = tower.Ring.Opens(local, locals);
                        wedge.Deep = tower.Ring.Reaches(locals);
                        Vector3 size = Abs(Vector3.Scale(box.size, box.transform.lossyScale));
                        wedge.Block = new OrientedBox
                        {
                            Center = local,
                            AxisX = ToVec(toLocal.MultiplyVector(box.transform.right)),
                            AxisY = ToVec(toLocal.MultiplyVector(box.transform.up)),
                            AxisZ = ToVec(toLocal.MultiplyVector(box.transform.forward)),
                            Half = ToVec(size * 0.5f),
                        };
                    }
                    s_wedges.Add(wedge);
                }
            }
            return s_wedges.ToArray();
        }

        private static IEnumerable<Vector3> Corners(BoxCollider box)
        {
            Vector3 c = box.center, h = box.size * 0.5f;
            for (int i = 0; i < 8; i++)
                yield return box.transform.TransformPoint(c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y,
                                                                          (i & 4) == 0 ? -h.z : h.z));
        }

        private static Vec3 ToVec(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static void KeepNear(LODGroup group, FittedStone fitted)
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length < 2 || !lods[0].renderers.Any(r => Carved(r, fitted)))
                return;
            LOD near = lods[0];
            var far = new List<Renderer>();
            for (int i = 1; i < lods.Length; i++)
                foreach (Renderer r in lods[i].renderers)
                    if (r != null && !near.renderers.Contains(r) && !far.Contains(r))
                        far.Add(r);
            if (far.All(r => Carved(r, fitted)))
                return;
            fitted.Groups.Add(group);
            fitted.Lods.Add(lods);
            near.screenRelativeTransitionHeight = lods[lods.Length - 1].screenRelativeTransitionHeight;
            group.SetLODs(new[] { near });
            foreach (Renderer r in far)
                if (r.enabled)
                {
                    r.enabled = false;
                    fitted.Silenced.Add(r);
                }
        }

        private static bool Carved(Renderer renderer, FittedStone fitted)
        {
            if (renderer == null)
                return false;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null && fitted.Filters.Contains(filter);
        }

        private static Shape Read(MeshFilter filter, Mesh source)
        {
            var shape = new Shape
            {
                Filter = filter, Name = source.name, ToWorld = filter.transform.localToWorldMatrix,
                ToLocal = filter.transform.worldToLocalMatrix, Vertices = source.vertices, Normals = source.normals,
                Tangents = source.tangents, Uv = source.uv, Uv2 = source.uv2, Colors = source.colors,
                Triangles = new int[source.subMeshCount][],
            };
            for (int k = 0; k < source.subMeshCount; k++)
                shape.Triangles[k] = source.GetTriangles(k);
            return shape;
        }

        private static Shape Read(BoxCollider box)
        {
            Vector3 c = box.center, h = box.size * 0.5f;
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            return new Shape
            {
                Box = box, Name = box.name, ToWorld = box.transform.localToWorldMatrix, ToLocal = box.transform.worldToLocalMatrix,
                Scale = box.transform.lossyScale, Keep = Bounds(box), Vertices = vertices, Triangles = new[] { BoxTriangles },
            };
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

        private static void Carve(Shape shape, Wedge[] wedges)
        {
            bool visual = shape.Box == null;
            var world = new List<Vec3>(shape.Vertices.Length);
            foreach (Vector3 v in shape.Vertices)
                world.Add(ToVec(shape.ToWorld.MultiplyPoint3x4(v)));
            int WedgeOf(Vec3 p)
            {
                Vector3 point = ToVector(p);
                for (int i = 0; i < wedges.Length; i++)
                    if (wedges[i].Contains(ToVec(wedges[i].ToLocal.MultiplyPoint3x4(point)), visual))
                        return i;
                return -1;
            }
            Vec3 Push(Vec3 p, int k)
            {
                Wedge w = wedges[k];
                Vec3 q = w.ToFace(ToVec(w.ToLocal.MultiplyPoint3x4(ToVector(p))), visual);
                return ToVec(w.ToWorld.MultiplyPoint3x4(ToVector(q)));
            }
            var carver = new MeshCarver(world, WedgeOf, MaxDepth, w => wedges[w].Tower != null ? CurvedEdge : 0, CapEdge);
            shape.CutTriangles = new List<int>[shape.Triangles.Length];
            for (int k = 0; k < shape.Triangles.Length; k++)
                shape.CutTriangles[k] = carver.Split(shape.Triangles[k]);
            shape.Moved = carver.Push(Push);
            if (shape.Keep.HasValue)
                carver.Clamp(shape.Keep.Value);
            shape.CutVertices = new List<Vector3>(carver.Points.Count);
            foreach (Vec3 p in carver.Points)
                shape.CutVertices.Add(shape.ToLocal.MultiplyPoint3x4(ToVector(p)));
            shape.CutNormals = Grow(shape.Normals, carver, (a, b) => (a + b).normalized);
            shape.CutTangents = Grow(shape.Tangents, carver, (a, b) =>
            {
                Vector3 t = ((Vector3)a + (Vector3)b).normalized;
                return new Vector4(t.x, t.y, t.z, a.w);
            });
            shape.CutUv = Grow(shape.Uv, carver, (a, b) => (a + b) * 0.5f);
            shape.CutUv2 = Grow(shape.Uv2, carver, (a, b) => (a + b) * 0.5f);
            shape.CutColors = Grow(shape.Colors, carver, (a, b) => Color.Lerp(a, b, 0.5f));
            if (visual)
                return;
            var points = new List<Vec3>(shape.CutVertices.Count);
            foreach (Vector3 v in shape.CutVertices)
                points.Add(new Vec3(v.x * shape.Scale.x, v.y * shape.Scale.y, v.z * shape.Scale.z));
            shape.Volume = (float)MeshVolume.Of(points, shape.CutTriangles[0]);
        }

        private static Mesh Build(Shape shape)
        {
            var mesh = new Mesh { name = shape.Name + "_fitted" };
            if (shape.CutVertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(shape.CutVertices);
            if (shape.CutNormals != null)
                mesh.SetNormals(shape.CutNormals);
            if (shape.CutTangents != null)
                mesh.SetTangents(shape.CutTangents);
            if (shape.CutUv != null)
                mesh.SetUVs(0, shape.CutUv);
            if (shape.CutUv2 != null)
                mesh.SetUVs(1, shape.CutUv2);
            if (shape.CutColors != null)
                mesh.SetColors(shape.CutColors);
            mesh.subMeshCount = shape.CutTriangles.Length;
            for (int k = 0; k < shape.CutTriangles.Length; k++)
                mesh.SetTriangles(shape.CutTriangles[k], k);
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

        private static Vector3 ToVector(Vec3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
