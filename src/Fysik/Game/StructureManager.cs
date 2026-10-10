using System;
using System.Collections.Generic;
using System.Diagnostics;
using Fysik.Structure;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class PieceNode
    {
        public readonly WearNTear Wnt;
        public readonly ZDOID Id;
        public readonly string Name;
        public readonly PieceGeometry Geometry;
        public readonly bool Structural;

        public readonly bool WorldBuilt;

        public readonly bool Attachment;

        private readonly Recipe _recipe;

        public readonly Container Container;

        public double AttachmentMass => Attachment ? _recipe.Mass : 0;

        public double AttachmentLoad => Attachment ? _recipe.Mass + Contents + Riding : 0;

        public double Contents;

        public double Riding;

        public double Snow;

        public int SnowLevel;

        public readonly List<Obb> SupportBoxes;

        public readonly Collider[] OwnColliders;

        public bool OnGround;

        public bool InWatchList;

        public readonly List<PieceNode> Attached = new List<PieceNode>();

        public readonly List<PieceNode> Dependents = new List<PieceNode>();

        public bool ContactsValid;

        public bool ScanTrusted;

        public readonly List<Contact> Contacts = new List<Contact>();
        public readonly List<Vector3> GroundPoints = new List<Vector3>();

        public bool Grounded
        {
            get
            {
                if (GroundPoints.Count > 0)
                    return true;
                foreach (Contact c in Contacts)
                    if (c.Other.Anchored && c.Other.Alive)
                        return true;
                return false;
            }
        }

        public Island Island;
        public bool HasResult;
        public BodyResult Result;
        public double Mass;
        public double Carried;
        public double Holds;
        public string MaterialName;

        public double Strength = 1;

        public long CrackTicks;

        public bool FellByFysik;

        public bool Watched;

        public PieceNode(WearNTear wnt)
        {
            Wnt = wnt;
            Id = wnt.m_nview.GetZDO().m_uid;
            Name = wnt.gameObject.name.Replace("(Clone)", "");
            Geometry = PieceGeometry.Of(wnt);
            Structural = wnt.m_supports && Geometry.IsValid;
            WorldBuilt = IsWorldBuilt(wnt);
            Container = wnt.GetComponentInChildren<Container>();
            if (!wnt.m_supports && wnt.m_noSupportWear)
            {
                OwnColliders = wnt.GetComponentsInChildren<Collider>(true);
                SupportBoxes = PieceGeometry.VanillaSupportBoxes(OwnColliders);
                Attachment = SupportBoxes.Count > 0;
                if (Attachment)
                    _recipe = Recipe.Of(wnt.GetComponent<Piece>());
            }
        }

        public bool Alive => Wnt != null;

        public bool Anchored => WorldBuilt && FysikConfig.WorldBuildings.Value == WorldBuildingMode.Vanilla;

        public static bool IsWorldBuilt(WearNTear wnt) => wnt.m_piece == null || wnt.m_piece.GetCreator() == 0;

        public static bool KeepsVanilla(WearNTear wnt) =>
            FysikConfig.WorldBuildings.Value == WorldBuildingMode.Vanilla && IsWorldBuilt(wnt);

        public bool Standing => Wnt != null && Wnt.m_nview != null && Wnt.m_nview.IsValid();

        public bool HasSupport
        {
            get
            {
                if (OnGround)
                    return true;
                foreach (Contact c in Contacts)
                    if (c.Other.Standing)
                        return true;
                return false;
            }
        }

        public static int Compare(PieceNode a, PieceNode b)
        {
            int c = a.Id.UserID.CompareTo(b.Id.UserID);
            return c != 0 ? c : a.Id.ID.CompareTo(b.Id.ID);
        }
    }

    internal struct Contact
    {
        public PieceNode Other;
        public Vector3 Point;
    }

    internal sealed class Island
    {
        public readonly List<PieceNode> Members;
        public bool Stale;
        public bool LoadsChanged;
        public SolverKind Solver;

        public float CascadeUntil;

        public float LastFall = -1;

        public bool Cascading => Time.time < CascadeUntil;

        public Island(List<PieceNode> members) => Members = members;
    }

    internal readonly struct GhostPose : IEquatable<GhostPose>
    {
        private readonly string _prefab;
        private readonly int _x, _y, _z, _rx, _ry, _rz, _rw;

        public GhostPose(GameObject ghost)
        {
            _prefab = ghost.name;
            Vector3 p = ghost.transform.position;
            Quaternion r = ghost.transform.rotation;
            _x = Mathf.RoundToInt(p.x * 100);
            _y = Mathf.RoundToInt(p.y * 100);
            _z = Mathf.RoundToInt(p.z * 100);
            _rx = Mathf.RoundToInt(r.x * 1000);
            _ry = Mathf.RoundToInt(r.y * 1000);
            _rz = Mathf.RoundToInt(r.z * 1000);
            _rw = Mathf.RoundToInt(r.w * 1000);
        }

        public bool Equals(GhostPose o) =>
            _x == o._x && _y == o._y && _z == o._z && _rx == o._rx && _ry == o._ry && _rz == o._rz && _rw == o._rw &&
            string.Equals(_prefab, o._prefab);

        public override bool Equals(object obj) => obj is GhostPose o && Equals(o);

        public override int GetHashCode() => ((_x * 397 ^ _y) * 397 ^ _z) * 397 ^ _rw;
    }

    internal sealed class PreviewResult
    {
        public BodyResult Ghost;
        public double GhostMass;
        public Vector3 GhostSize;
        public string MaterialName;
        public SolverKind Solver;
        public PieceNode WorstOther;
        public BodyResult WorstOtherResult;
    }

    internal sealed class StructureManager
    {
        private const float ContactMargin = 0.15f;
        private const int MaxIslandScan = 20000;

        private const float CascadeSeconds = 3f;

        private const float CascadeMemorySeconds = 5f;

        public static StructureManager Instance { get; } = new StructureManager();

        private readonly Dictionary<WearNTear, PieceNode> _nodes = new Dictionary<WearNTear, PieceNode>();
        private readonly Queue<WearNTear> _appeared = new Queue<WearNTear>();
        private readonly Queue<PieceNode> _requests = new Queue<PieceNode>();
        private readonly HashSet<PieceNode> _requested = new HashSet<PieceNode>();
        private Job _job;

        private readonly Queue<PieceNode> _attachmentChecks = new Queue<PieceNode>();
        private readonly HashSet<PieceNode> _attachmentQueued = new HashSet<PieceNode>();

        private readonly List<PieceNode> _watch = new List<PieceNode>();
        private int _watchNext;
        private const double WatchBudgetMs = 0.25;

        private int _snowNext;
        private const int SnowChecksPerFrame = 200;

        private Job _previewJob;
        private GhostPose? _previewPose;

        public PreviewResult Preview { get; private set; }

        public bool PreviewPending => _previewJob != null;

        private static readonly Collider[] s_hits = new Collider[128];

        private const float BuriedDepth = 2f;

        private const int MaxGroundPoints = 16;

        private static int s_terrainMask;
        private static readonly HashSet<Collider> s_groundColliders = new HashSet<Collider>();
        private readonly List<Vector3> _scratchGround = new List<Vector3>();
        private static readonly HashSet<string> s_describedPrefabs = new HashSet<string>();
        private static int s_rayMask;
        private static int s_terrainLayer = -1;

        public int NodeCount => _nodes.Count;
        public int PendingRequests => _requests.Count + (_job != null ? 1 : 0);
        public string LastSolve { get; private set; } = "none yet";

        public void OnPieceAppeared(WearNTear wnt)
        {
            _appeared.Enqueue(wnt);
        }

        public void OnPieceDestroyed(WearNTear wnt)
        {
            if (!_nodes.TryGetValue(wnt, out PieceNode node))
                return;
            if (node.Attachment)
            {
                _nodes.Remove(wnt);
                foreach (Contact c in node.Contacts)
                {
                    c.Other.Attached.Remove(node);
                    if (node.AttachmentLoad > 0)
                        Reload(c.Other);
                }
                return;
            }
            foreach (PieceNode attachment in node.Attached)
                Invalidate(attachment);
            node.Attached.Clear();
            if (node.Island != null && (node.FellByFysik || node.CrackTicks != 0 || node.Island.Cascading))
            {
                node.Island.LastFall = Time.time;
                node.Island.CascadeUntil = Time.time + CascadeSeconds;
            }
            foreach (Contact c in node.Contacts)
            {
                Invalidate(c.Other);
                if (c.Other.WorldBuilt)
                    c.Other.Dependents.Remove(node);
            }
            foreach (PieceNode dependent in node.Dependents)
                if (dependent.Alive)
                    Invalidate(dependent);
            Invalidate(node);
            _nodes.Remove(wnt);
            foreach (Contact c in node.Contacts)
                if (c.Other.Alive)
                    Request(c.Other.Wnt);
            foreach (PieceNode dependent in node.Dependents)
                if (dependent.Alive)
                    Request(dependent.Wnt);
            node.Dependents.Clear();
        }

        public void OnPieceHealthChanged(WearNTear wnt)
        {
            if (!FysikConfig.DamageWeakens.Value || !_nodes.TryGetValue(wnt, out PieceNode node) ||
                Damage.StrengthFactor(wnt.GetHealthPercentage()) == node.Strength)
                return;
            if (node.Island != null)
                node.Island.Stale = true;
            if (_job != null && _job.Visited.Contains(node))
                _job.MarkDirty();
        }

        public bool TryGetNode(WearNTear wnt, out PieceNode node) => _nodes.TryGetValue(wnt, out node);

        public void InvalidateAllResults()
        {
            foreach (PieceNode node in _nodes.Values)
                if (node.Island != null)
                    node.Island.Stale = true;
            _job?.MarkDirty();
        }

        public void AbortJob()
        {
            _job?.Cancel();
            _job = null;
        }

        public void Clear()
        {
            _nodes.Clear();
            _appeared.Clear();
            _requests.Clear();
            _requested.Clear();
            _job?.Cancel();
            _job = null;
            _attachmentChecks.Clear();
            _attachmentQueued.Clear();
            _watch.Clear();
            _watchNext = 0;
            _snowNext = 0;
            ClearPreview();
        }

        public void InvalidateContacts(PieceNode node) => Invalidate(node);

        private void Reload(PieceNode node)
        {
            if (!node.Alive || !node.Structural || node.Anchored)
                return;
            if (node.Island != null)
                node.Island.Stale = true;
            if (_job != null && _job.Visited.Contains(node))
                _job.MarkDirty();
            Request(node.Wnt);
        }

        public void ReloadLoads(PieceNode node)
        {
            if (node == null || !node.Alive)
                return;
            if (node.Attachment)
            {
                foreach (Contact c in node.Contacts)
                    ReloadLoads(c.Other);
                return;
            }
            if (!node.Structural || node.Anchored)
                return;
            if (_job != null && _job.Model != null && _job.Visited.Contains(node))
                _job.LoadsChanged = true;
            if (node.Island != null)
                node.Island.LoadsChanged = true;
            if (_requested.Add(node))
                _requests.Enqueue(node);
        }

        public void OnContainerChanged(Container container)
        {
            ZNetView root = container.m_rootObjectOverride;
            WearNTear wnt = root != null ? root.GetComponent<WearNTear>() : container.GetComponent<WearNTear>();
            if (wnt == null || !_nodes.TryGetValue(wnt, out PieceNode node) || node.Container == null)
                return;
            double contents = ExtraLoads.ContentsOf(node.Container);
            if (contents == node.Contents)
                return;
            node.Contents = contents;
            ReloadLoads(node);
        }

        private static readonly List<PieceNode> s_carried = new List<PieceNode>();

        private static double CarriedBy(PieceNode node)
        {
            s_carried.Clear();
            foreach (PieceNode a in node.Attached)
            {
                if (!a.Alive || !a.ContactsValid)
                    continue;
                a.Contents = ExtraLoads.ContentsOf(a.Container);
                if (a.AttachmentLoad > 0)
                    s_carried.Add(a);
            }
            s_carried.Sort(PieceNode.Compare);
            double mass = 0;
            foreach (PieceNode a in s_carried)
            {
                int shares = a.OnGround ? 1 : 0;
                foreach (Contact c in a.Contacts)
                    if (c.Other.Alive)
                        shares++;
                if (shares > 0)
                    mass += a.AttachmentLoad / shares;
            }
            return mass;
        }

        private void Invalidate(PieceNode node)
        {
            node.ContactsValid = false;
            if (node.Attachment)
            {
                QueueAttachment(node);
                return;
            }
            if (node.Island != null)
                node.Island.Stale = true;
            if (_job != null && _job.Visited.Contains(node))
                _job.MarkDirty();
            if (_previewJob != null && _previewJob.Visited.Contains(node))
                _previewJob.MarkDirty();
            else if (Preview != null)
                _previewPose = null;
        }

        public bool Knows(WearNTear wnt) => _nodes.ContainsKey(wnt);

        public PieceNode GetNode(WearNTear wnt)
        {
            if (wnt == null)
                return null;
            if (_nodes.TryGetValue(wnt, out PieceNode node))
                return node;
            if (wnt.m_nview == null || wnt.m_nview.GetZDO() == null)
                return null;
            node = new PieceNode(wnt);
            _nodes[wnt] = node;
            if (FysikConfig.LogGeometry.Value && s_describedPrefabs.Add(node.Name))
            {
                MaterialProps material = MaterialTable.For(wnt);
                Plugin.Log.LogInfo(node.Structural
                    ? $"Piece {node.Name}: {node.Geometry.Summary}; {material.Name}, {node.Geometry.MassFor(material):0} kg" +
                      (node.Geometry.MaterialsMass > 0 ? " (from its materials)" : "")
                    : node.Attachment
                        ? $"Piece {node.Name}: attachment, {node.AttachmentMass:0} kg (from its materials) on what holds it"
                        : $"Piece {node.Name}: ignored");
            }
            return node;
        }

        public PieceNode Request(WearNTear wnt)
        {
            PieceNode node = GetNode(wnt);
            if (node != null && node.Attachment && !node.ContactsValid)
                QueueAttachment(node);
            if (node == null || !node.Structural || node.Anchored)
                return node;
            bool current = node.HasResult && node.Island != null && !node.Island.Stale;
            if (!current && _requested.Add(node))
                _requests.Enqueue(node);
            return node;
        }

        public void Tick(float budgetMs)
        {
            long start = Stopwatch.GetTimestamp();
            long deadline = start + (long)(budgetMs * Stopwatch.Frequency / 1000.0);

            while (_appeared.Count > 0 && Stopwatch.GetTimestamp() < deadline)
                ProcessAppeared(_appeared.Dequeue());

            while (_attachmentChecks.Count > 0 && Stopwatch.GetTimestamp() < deadline)
            {
                PieceNode attachment = _attachmentChecks.Dequeue();
                _attachmentQueued.Remove(attachment);
                if (attachment.Alive && _nodes.ContainsKey(attachment.Wnt))
                    ScanAttachment(attachment);
            }

            if (_previewJob != null && Stopwatch.GetTimestamp() < deadline && _previewJob.Run(this, deadline))
            {
                FinishPreview(_previewJob);
                _previewJob = null;
            }

            long watchDeadline = Math.Min(deadline, Stopwatch.GetTimestamp() + (long)(WatchBudgetMs * Stopwatch.Frequency / 1000.0));
            WatchGround(watchDeadline);
            WatchSnow();

            while (Stopwatch.GetTimestamp() < deadline)
            {
                if (_job == null && !StartNextJob())
                    break;
                long t0 = Stopwatch.GetTimestamp();
                bool done = _job.Run(this, deadline);
                _job.ComputeTicks += Stopwatch.GetTimestamp() - t0;
                if (done)
                {
                    Finish(_job);
                    _job = null;
                }
                else if (_job.OnWorker)
                {
                    break;
                }
            }
        }

        private bool StartNextJob()
        {
            while (_requests.Count > 0)
            {
                PieceNode seed = _requests.Dequeue();
                _requested.Remove(seed);
                if (!seed.Alive || !_nodes.ContainsKey(seed.Wnt) || seed.Anchored)
                    continue;
                if (seed.HasResult && seed.Island != null && !seed.Island.Stale && !seed.Island.LoadsChanged)
                    continue;
                _job = new Job(seed);
                return true;
            }
            return false;
        }

        private void ProcessAppeared(WearNTear wnt)
        {
            if (wnt == null || !wnt.m_supports || _nodes.Count == 0 || wnt.m_nview == null || wnt.m_nview.GetZDO() == null)
                return;
            PieceGeometry geometry = PieceGeometry.Of(wnt);
            EnsureMasks();
            foreach (Obb box in geometry.Boxes)
            {
                int count = Physics.OverlapBoxNonAlloc(box.Center, box.Size * 0.5f + Vector3.one * ContactMargin,
                                                       s_hits, box.Rotation, s_rayMask);
                for (int i = 0; i < count; i++)
                {
                    WearNTear other = s_hits[i].GetComponentInParent<WearNTear>();
                    if (other != null && other != wnt && _nodes.TryGetValue(other, out PieceNode node))
                        Invalidate(node);
                }
            }
        }

        public void RequestPreview(WearNTear ghost)
        {
            var pose = new GhostPose(ghost.gameObject);
            if (_previewPose.HasValue && _previewPose.Value.Equals(pose))
                return;
            _previewPose = pose;
            Preview = null;
            _previewJob?.Cancel();
            _previewJob = null;
            if (!ghost.m_supports)
                return;
            PieceGeometry geometry = PieceGeometry.Of(ghost);
            if (!geometry.IsValid)
                return;

            var g = new GhostPiece { Geometry = geometry, Material = MaterialTable.For(ghost) };
            FindContacts(geometry, ghost, g.Contacts, g.GroundPoints);
            var seeds = new List<PieceNode>();
            foreach (Contact c in g.Contacts)
                seeds.Add(c.Other);
            _previewJob = new Job(seeds, g);
        }

        public void ClearPreview()
        {
            _previewPose = null;
            Preview = null;
            _previewJob?.Cancel();
            _previewJob = null;
        }

        private void FinishPreview(Job job)
        {
            if (job.Dirty || !job.Analysis.Finished)
            {
                _previewPose = null;
                return;
            }
            BodyResult[] results = job.Analysis.Results;
            int ghost = job.Members.Count;
            var preview = new PreviewResult
            {
                Ghost = results[ghost],
                GhostMass = job.Model.Bodies[ghost].Mass,
                GhostSize = new Vector3((float)job.Model.Bodies[ghost].Size.X, (float)job.Model.Bodies[ghost].Size.Y,
                                        (float)job.Model.Bodies[ghost].Size.Z),
                MaterialName = job.Model.Bodies[ghost].Material.Name,
                Solver = job.Analysis.Solver,
            };
            for (int i = 0; i < ghost; i++)
            {
                if (preview.WorstOther == null || results[i].Utilization > preview.WorstOtherResult.Utilization)
                {
                    preview.WorstOther = job.Members[i];
                    preview.WorstOtherResult = results[i];
                }
            }
            Preview = preview;
        }

        private void Finish(Job job)
        {
            if (job.Dirty || !job.Analysis.Finished)
            {
                if (job.Seed.Alive && _requested.Add(job.Seed))
                    _requests.Enqueue(job.Seed);
                return;
            }

            var island = new Island(job.Members) { Solver = job.Analysis.Solver };
            foreach (PieceNode node in job.Members)
                if (node.Island != null)
                    island.LastFall = Mathf.Max(island.LastFall, node.Island.LastFall);
            if (island.LastFall >= 0 && Time.time - island.LastFall < CascadeMemorySeconds)
                island.CascadeUntil = Time.time + CascadeSeconds;
            BodyResult[] results = job.Analysis.Results;
            int worst = -1;
            for (int i = 0; i < job.Members.Count; i++)
            {
                PieceNode node = job.Members[i];
                node.Island = island;
                node.Result = results[i];
                if (!node.HasResult)
                    _watch.Add(node);
                node.HasResult = true;
                node.Mass = job.Model.Bodies[i].Mass - job.Carried[i];
                node.Carried = job.Carried[i];
                node.Snow = job.Snow[i];
                node.SnowLevel = job.SnowLevels[i];
                node.Holds = results[i].HeldWeight / job.Model.Gravity.Length + job.Carried[i];
                node.MaterialName = job.Model.Bodies[i].Material.Name;
                if (worst < 0 || results[i].Utilization > results[worst].Utilization)
                    worst = i;
            }
            if (job.LoadsChanged)
            {
                island.LoadsChanged = true;
                if (job.Seed.Alive && _requested.Add(job.Seed))
                    _requests.Enqueue(job.Seed);
            }

            double ms = job.ComputeTicks * 1000.0 / Stopwatch.Frequency;
            double wall = (Stopwatch.GetTimestamp() - job.Started) * 1000.0 / Stopwatch.Frequency;
            string worstText = worst < 0 ? "" :
                $"; worst {job.Members[worst].Name} {Describe(results[worst])}";
            LastSolve = $"{job.Members.Count} pieces, {island.Solver}" +
                        (island.Solver == SolverKind.Frame ? $" ({job.Analysis.Iterations} iterations{(job.Analysis.Converged ? "" : ", not converged")})" : "") +
                        $", {ms:0.0} ms of frame time over {job.Frames} frames, {wall:0} ms in all{worstText}";
            if (FysikConfig.LogSolves.Value)
                Plugin.Log.LogInfo("Solved " + LastSolve);
        }

        internal static string Describe(BodyResult r) =>
            r.Mode == StressMode.Unsupported ? "unsupported" : $"{r.Mode} {r.Utilization:P0}";

        private static void EnsureMasks()
        {
            if (s_rayMask != 0)
                return;
            s_rayMask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");
            s_terrainLayer = LayerMask.NameToLayer("terrain");
            s_terrainMask = 1 << s_terrainLayer;
        }

        private struct WeightedPoint
        {
            public Vec3 Point;
            public double Weight;
        }

        private readonly Dictionary<PieceNode, List<WeightedPoint>> _contactPoints = new Dictionary<PieceNode, List<WeightedPoint>>();

        private void ScanContacts(PieceNode node)
        {
            node.ScanTrusted = Readiness.Settled(node.Wnt);
            FindContacts(node.Geometry, node.Wnt, node.Contacts, node.GroundPoints);
            node.ContactsValid = true;
            if (node.WorldBuilt)
                return;
            foreach (Contact c in node.Contacts)
                if (c.Other.WorldBuilt && !c.Other.Dependents.Contains(node))
                    c.Other.Dependents.Add(node);
        }

        private void QueueAttachment(PieceNode node)
        {
            if (_attachmentQueued.Add(node))
                _attachmentChecks.Enqueue(node);
        }

        private static readonly List<PieceNode> s_oldSupporters = new List<PieceNode>();

        private void ScanAttachment(PieceNode node)
        {
            EnsureMasks();
            node.ScanTrusted = Readiness.Settled(node.Wnt);
            s_oldSupporters.Clear();
            foreach (Contact c in node.Contacts)
            {
                c.Other.Attached.Remove(node);
                s_oldSupporters.Add(c.Other);
            }
            bool wasOnGround = node.OnGround;
            node.Contacts.Clear();
            node.OnGround = false;

            foreach (Obb box in node.SupportBoxes)
            {
                int count = Physics.OverlapBoxNonAlloc(box.Center, box.Size * 0.5f, s_hits, box.Rotation, s_rayMask);
                for (int i = 0; i < count; i++)
                {
                    Collider c = s_hits[i];
                    if (c.isTrigger || c.attachedRigidbody != null || Array.IndexOf(node.OwnColliders, c) >= 0)
                        continue;
                    WearNTear other = c.gameObject.layer == s_terrainLayer ? null : c.GetComponentInParent<WearNTear>();
                    if (other == null)
                    {
                        node.OnGround = true;
                        continue;
                    }
                    if (other == node.Wnt || !other.m_supports)
                        continue;
                    PieceNode supporter = GetNode(other);
                    if (supporter == null || node.Contacts.Exists(k => k.Other == supporter))
                        continue;
                    node.Contacts.Add(new Contact { Other = supporter, Point = box.Center });
                    supporter.Attached.Add(node);
                }
            }
            node.ContactsValid = true;
            if (node.AttachmentLoad > 0 && (wasOnGround != node.OnGround || s_oldSupporters.Count != node.Contacts.Count ||
                                            node.Contacts.Exists(k => !s_oldSupporters.Contains(k.Other))))
            {
                foreach (PieceNode old in s_oldSupporters)
                    Reload(old);
                foreach (Contact c in node.Contacts)
                    Reload(c.Other);
            }
            if (!node.InWatchList)
            {
                node.InWatchList = true;
                _watch.Add(node);
            }
        }

        private void WatchGround(long deadline)
        {
            int checkedCount = 0;
            while (_watch.Count > 0 && checkedCount < _watch.Count && Stopwatch.GetTimestamp() < deadline)
            {
                if (_watchNext >= _watch.Count)
                    _watchNext = 0;
                PieceNode node = _watch[_watchNext];
                if (!node.Alive || !_nodes.ContainsKey(node.Wnt))
                {
                    _watch[_watchNext] = _watch[_watch.Count - 1];
                    _watch.RemoveAt(_watch.Count - 1);
                    continue;
                }
                _watchNext++;
                checkedCount++;
                if (node.Attachment)
                {
                    if (node.ContactsValid)
                        ScanAttachment(node);
                }
                else if (node.ContactsValid && CountGround(node.Geometry) != node.GroundPoints.Count)
                {
                    Invalidate(node);
                    Request(node.Wnt);
                }
            }
        }

        private void WatchSnow()
        {
            if (!FysikConfig.LiveLoads.Value || FysikConfig.SnowWeight.Value <= 0)
                return;
            int count = Math.Min(SnowChecksPerFrame, _watch.Count);
            for (int k = 0; k < count; k++)
            {
                if (_snowNext >= _watch.Count)
                    _snowNext = 0;
                PieceNode node = _watch[_snowNext++];
                if (node.Alive && node.Structural && node.HasResult && ExtraLoads.SnowLevel(node.Wnt) != node.SnowLevel)
                    ReloadLoads(node);
            }
        }

        private int CountGround(PieceGeometry geometry)
        {
            EnsureMasks();
            _scratchGround.Clear();
            s_groundColliders.Clear();
            Vector3 center = geometry.Main.Center;
            foreach (Obb box in geometry.Boxes)
            {
                AddBottomSamples(box, null, _scratchGround);
                int count = Physics.OverlapBoxNonAlloc(box.Center, box.Size * 0.5f + Vector3.one * ContactMargin,
                                                       s_hits, box.Rotation, s_rayMask);
                for (int i = 0; i < count; i++)
                {
                    Collider c = s_hits[i];
                    if (c.isTrigger || c.attachedRigidbody != null || geometry.Colliders.Contains(c) ||
                        c.gameObject.layer == s_terrainLayer || c.GetComponentInParent<WearNTear>() != null)
                        continue;
                    AddStaticGround(c, box, center, _scratchGround);
                }
            }
            return Math.Min(_scratchGround.Count, MaxGroundPoints);
        }

        private void FindContacts(PieceGeometry geometry, WearNTear self, List<Contact> contacts, List<Vector3> ground)
        {
            EnsureMasks();
            foreach (List<WeightedPoint> list in _contactPoints.Values)
                list.Clear();
            ground.Clear();
            s_groundColliders.Clear();
            Vector3 center = geometry.Main.Center;

            foreach (Obb box in geometry.Boxes)
            {
                AddBottomSamples(box, null, ground);
                OrientedBox grown = box.Grown(ContactMargin);

                int count = Physics.OverlapBoxNonAlloc(box.Center, box.Size * 0.5f + Vector3.one * ContactMargin,
                                                       s_hits, box.Rotation, s_rayMask);
                for (int i = 0; i < count; i++)
                {
                    Collider c = s_hits[i];
                    if (c.isTrigger || c.attachedRigidbody != null || geometry.Colliders.Contains(c) ||
                        c.gameObject.layer == s_terrainLayer)
                        continue;
                    WearNTear other = c.GetComponentInParent<WearNTear>();
                    if (other == null)
                    {
                        AddStaticGround(c, box, center, ground);
                        continue;
                    }
                    if (other == self || !other.m_supports)
                        continue;
                    PieceNode otherNode = GetNode(other);
                    if (otherNode == null || !otherNode.Structural)
                        continue;

                    if (!Overlap.Of(grown, Obb.Of(c).Grown(ContactMargin), out Vec3 centroid, out double volume))
                        continue;
                    if (!_contactPoints.TryGetValue(otherNode, out List<WeightedPoint> points))
                        _contactPoints[otherNode] = points = new List<WeightedPoint>();
                    points.Add(new WeightedPoint { Point = centroid, Weight = volume });
                }
            }

            contacts.Clear();
            foreach (KeyValuePair<PieceNode, List<WeightedPoint>> kv in _contactPoints)
                if (kv.Value.Count > 0)
                    contacts.Add(new Contact { Other = kv.Key, Point = Centroid(kv.Value) });
            contacts.Sort((a, b) => PieceNode.Compare(a.Other, b.Other));
            _contactPoints.Clear();

            SortPoints(ground);
            if (ground.Count > MaxGroundPoints)
            {
                for (int i = 0; i < MaxGroundPoints; i++)
                    ground[i] = ground[i * ground.Count / MaxGroundPoints];
                ground.RemoveRange(MaxGroundPoints, ground.Count - MaxGroundPoints);
            }
        }

        private static void AddBottomSamples(Obb box, Collider only, List<Vector3> output)
        {
            Vector3 x = box.Rotation * Vector3.right, y = box.Rotation * Vector3.up, z = box.Rotation * Vector3.forward;
            Vector3 half = box.Size * 0.5f;
            float dx = Vector3.Dot(x, Vector3.down), dy = Vector3.Dot(y, Vector3.down), dz = Vector3.Dot(z, Vector3.down);
            Vector3 normal, u, v;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy) && Mathf.Abs(dx) >= Mathf.Abs(dz))
            {
                normal = x * (Mathf.Sign(dx) * half.x);
                u = y * (half.y * 0.9f);
                v = z * (half.z * 0.9f);
            }
            else if (Mathf.Abs(dy) >= Mathf.Abs(dz))
            {
                normal = y * (Mathf.Sign(dy) * half.y);
                u = x * (half.x * 0.9f);
                v = z * (half.z * 0.9f);
            }
            else
            {
                normal = z * (Mathf.Sign(dz) * half.z);
                u = x * (half.x * 0.9f);
                v = y * (half.y * 0.9f);
            }
            Vector3 face = box.Center + normal;
            float distance = BuriedDepth + ContactMargin;
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    Vector3 p = face + u * i + v * j;
                    var ray = new Ray(p + Vector3.up * BuriedDepth, Vector3.down);
                    bool hit = only != null
                        ? only.Raycast(ray, out RaycastHit _, distance)
                        : Physics.Raycast(ray, distance, s_terrainMask, QueryTriggerInteraction.Ignore);
                    if (hit)
                        output.Add(p);
                }
            }
        }

        private static void AddStaticGround(Collider c, Obb box, Vector3 center, List<Vector3> ground)
        {
            if (!s_groundColliders.Add(c))
                return;
            bool closestPointSupported = c is BoxCollider || c is SphereCollider || c is CapsuleCollider ||
                                         (c is MeshCollider mesh && mesh.convex);
            if (closestPointSupported)
            {
                Vector3 q = c.ClosestPoint(center);
                ground.Add((q + box.ClosestPoint(q)) * 0.5f);
                return;
            }
            int before = ground.Count;
            AddBottomSamples(box, c, ground);
            if (ground.Count == before)
                ground.Add(box.ClosestPoint(c.bounds.ClosestPoint(center)));
        }

        private static void SortPoints(List<Vector3> points) =>
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z));

        private static Vector3 Centroid(List<WeightedPoint> points)
        {
            points.Sort((a, b) => a.Point.X != b.Point.X ? a.Point.X.CompareTo(b.Point.X)
                                : a.Point.Y != b.Point.Y ? a.Point.Y.CompareTo(b.Point.Y)
                                : a.Point.Z != b.Point.Z ? a.Point.Z.CompareTo(b.Point.Z)
                                : a.Weight.CompareTo(b.Weight));
            Vec3 sum = Vec3.Zero;
            double weight = 0;
            foreach (WeightedPoint p in points)
            {
                sum += p.Point * p.Weight;
                weight += p.Weight;
            }
            Vec3 c = sum / weight;
            return new Vector3((float)c.X, (float)c.Y, (float)c.Z);
        }

        private sealed class GhostPiece
        {
            public PieceGeometry Geometry;
            public MaterialProps Material;
            public readonly List<Contact> Contacts = new List<Contact>();
            public readonly List<Vector3> GroundPoints = new List<Vector3>();
        }

        private sealed class Job
        {
            public readonly PieceNode Seed;
            public readonly HashSet<PieceNode> Visited = new HashSet<PieceNode>();
            private readonly Queue<PieceNode> _frontier = new Queue<PieceNode>();
            private readonly GhostPiece _ghost;

            public List<PieceNode> Members;
            public double[] Carried;
            public double[] Snow;
            public int[] SnowLevels;
            public StructureModel Model;
            public StructureAnalysis Analysis;
            public bool Dirty;
            public bool LoadsChanged;
            public long ComputeTicks;
            public int Frames;
            public readonly long Started = Stopwatch.GetTimestamp();

            private System.Threading.Tasks.Task _solve;
            private volatile bool _cancelled;

            public void MarkDirty()
            {
                Dirty = true;
                _cancelled = true;
            }

            public void Cancel() => _cancelled = true;

            public bool OnWorker => _solve != null && !_solve.IsCompleted;

            public Job(PieceNode seed)
            {
                Seed = seed;
                Visited.Add(seed);
                _frontier.Enqueue(seed);
            }

            public Job(List<PieceNode> seeds, GhostPiece ghost)
            {
                _ghost = ghost;
                foreach (PieceNode s in seeds)
                    if (!s.Anchored && Visited.Add(s))
                        _frontier.Enqueue(s);
            }

            public bool Run(StructureManager manager, long deadline)
            {
                Frames++;
                while (_frontier.Count > 0)
                {
                    if (Stopwatch.GetTimestamp() >= deadline)
                        return false;
                    PieceNode node = _frontier.Dequeue();
                    if (!node.Alive)
                        continue;
                    if (!node.ContactsValid)
                        manager.ScanContacts(node);
                    foreach (Contact c in node.Contacts)
                        if (c.Other.Alive && !c.Other.Anchored && Visited.Count < MaxIslandScan && Visited.Add(c.Other))
                            _frontier.Enqueue(c.Other);
                }

                if (Analysis == null)
                    Build();

                if (FysikConfig.BackgroundSolver.Value || _solve != null)
                {
                    if (_solve == null)
                        _solve = System.Threading.Tasks.Task.Run(() => Analysis.RunToCompletion(() => _cancelled));
                    if (!_solve.IsCompleted)
                        return false;
                    if (_solve.IsFaulted)
                        throw _solve.Exception.GetBaseException();
                    return true;
                }

                while (Stopwatch.GetTimestamp() < deadline)
                    if (Analysis.Step(1))
                        return true;
                return false;
            }

            private void Build()
            {
                Members = new List<PieceNode>();
                foreach (PieceNode n in Visited)
                    if (n.Alive)
                        Members.Add(n);
                Members.Sort(PieceNode.Compare);

                var index = new Dictionary<PieceNode, int>(Members.Count);
                Model = new StructureModel();
                Carried = new double[Members.Count];
                Snow = new double[Members.Count];
                SnowLevels = new int[Members.Count];
                for (int i = 0; i < Members.Count; i++)
                {
                    PieceNode member = Members[i];
                    index[member] = i;
                    Body body = member.Geometry.ToBody(MaterialTable.For(member.Wnt));
                    member.Contents = ExtraLoads.ContentsOf(member.Container);
                    SnowLevels[i] = ExtraLoads.SnowLevel(member.Wnt);
                    Snow[i] = ExtraLoads.SnowMass(member.Geometry, SnowLevels[i]);
                    Carried[i] = CarriedBy(member) + member.Riding + member.Contents + Snow[i];
                    body.Mass += Carried[i];
                    body.StrengthFactor = FysikConfig.DamageWeakens.Value
                        ? Damage.StrengthFactor(member.Wnt.GetHealthPercentage())
                        : 1;
                    member.Strength = body.StrengthFactor;
                    Model.AddBody(body);
                }

                var joints = new SortedDictionary<long, Vector3>();
                for (int i = 0; i < Members.Count; i++)
                {
                    foreach (Contact c in Members[i].Contacts)
                    {
                        if (!index.TryGetValue(c.Other, out int j))
                            continue;
                        long key = (long)Math.Min(i, j) * Members.Count + Math.Max(i, j);
                        if (!joints.ContainsKey(key) || i < j)
                            joints[key] = c.Point;
                    }
                }
                foreach (KeyValuePair<long, Vector3> kv in joints)
                {
                    int a = (int)(kv.Key / Members.Count), b = (int)(kv.Key % Members.Count);
                    Model.Connect(a, b, PieceGeometry.ToVec(kv.Value));
                }
                for (int i = 0; i < Members.Count; i++)
                {
                    foreach (Vector3 p in Members[i].GroundPoints)
                        Model.Ground(i, PieceGeometry.ToVec(p));
                    foreach (Contact c in Members[i].Contacts)
                        if (c.Other.Anchored && c.Other.Alive)
                            Model.Ground(i, PieceGeometry.ToVec(c.Point));
                }

                if (_ghost != null)
                {
                    int g = Model.AddBody(_ghost.Geometry.ToBody(_ghost.Material));
                    foreach (Contact c in _ghost.Contacts)
                    {
                        if (index.TryGetValue(c.Other, out int j))
                            Model.Connect(g, j, PieceGeometry.ToVec(c.Point));
                        else if (c.Other.Anchored && c.Other.Alive)
                            Model.Ground(g, PieceGeometry.ToVec(c.Point));
                    }
                    foreach (Vector3 p in _ghost.GroundPoints)
                        Model.Ground(g, PieceGeometry.ToVec(p));
                }

                Analysis = new StructureAnalysis(Model, FysikConfig.MaxFrameBodies.Value);
            }
        }
    }
}
