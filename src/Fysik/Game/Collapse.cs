using System;
using System.Collections.Generic;
using Fysik.Structure;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class CollapseController
    {
        internal static readonly int CrackKey = "fysik_crack".GetStableHashCode();

        private const float RequestInterval = 1f;
        private const float EvaluateInterval = 0.25f;

        private const int MaxNewPiecesPerPass = 300;

        public static CollapseController Instance { get; } = new CollapseController();

        private float _nextRequest, _nextEvaluate;
        private readonly Dictionary<Island, bool> _ready = new Dictionary<Island, bool>();
        private readonly List<PieceNode> _falling = new List<PieceNode>();

        public void Tick()
        {
            if (ZNet.instance == null || ZNet.instance.IsDedicated() || ZoneSystem.instance == null)
                return;
            bool physics = FysikConfig.Mode.Value == StructureMode.Physics;
            float now = Time.time;
            if (physics && now >= _nextRequest)
            {
                _nextRequest = now + RequestInterval;
                RequestOwnedStructures();
            }
            if (now >= _nextEvaluate)
            {
                _nextEvaluate = now + EvaluateInterval;
                Evaluate(physics);
            }
        }

        private static void RequestOwnedStructures()
        {
            StructureManager manager = StructureManager.Instance;
            int created = 0;
            foreach (WearNTear wnt in WearNTear.GetAllInstances())
            {
                if (!Owned(wnt))
                    continue;
                if (!manager.Knows(wnt) && created++ >= MaxNewPiecesPerPass)
                    continue;
                manager.Request(wnt);
            }
        }

        private void Evaluate(bool physics)
        {
            _ready.Clear();
            _falling.Clear();
            long nowTicks = ZNet.instance.GetTime().Ticks;
            double warning = FysikConfig.CrackWarningSeconds.Value;
            bool worldAllowsFalls = !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoBuildingFall);
            StructureManager manager = StructureManager.Instance;

            foreach (WearNTear wnt in WearNTear.GetAllInstances())
            {
                if (!Owned(wnt) || !manager.TryGetNode(wnt, out PieceNode node))
                    continue;
                ZDO zdo = wnt.m_nview.GetZDO();
                long crack = zdo.GetLong(CrackKey);
                node.CrackTicks = crack;

                if (!physics)
                {
                    if (crack != 0)
                        SetCrack(zdo, node, 0);
                    continue;
                }
                if (node.Attachment)
                {
                    if (node.ContactsValid && !node.HasSupport && AttachmentReady(node) && CanFall(wnt, worldAllowsFalls))
                        _falling.Add(node);
                    continue;
                }
                if (!node.Structural || !node.HasResult || node.Island == null || node.Island.Stale)
                    continue;

                bool ready = IsReady(node.Island);
                if (!ready)
                {
                    node.Watched = false;
                    continue;
                }
                if (!node.Watched)
                {
                    node.Watched = true;
                    if (crack != 0)
                        SetCrack(zdo, node, crack = nowTicks);
                }

                bool canFall = CanFall(wnt, worldAllowsFalls);
                double seconds = crack != 0 ? (nowTicks - crack) / (double)TimeSpan.TicksPerSecond : 0;
                CrackAction action = CrackPolicy.Decide(node.Result.Utilization, node.Result.Mode == StressMode.Unsupported,
                                                        crack != 0, seconds, warning, node.Island.Cascading, ready, canFall);
                switch (action)
                {
                    case CrackAction.StartCrack:
                        SetCrack(zdo, node, nowTicks);
                        if (FysikConfig.LogSolves.Value)
                            Plugin.Log.LogInfo($"{node.Name} is cracking ({StructureManager.Describe(node.Result)})");
                        break;
                    case CrackAction.StopCrack:
                        SetCrack(zdo, node, 0);
                        if (FysikConfig.LogSolves.Value)
                            Plugin.Log.LogInfo($"{node.Name} stopped cracking ({StructureManager.Describe(node.Result)})");
                        break;
                    case CrackAction.Fall:
                        _falling.Add(node);
                        break;
                }
            }

            if (_falling.Count == 0)
                return;
            int failed = _falling.Count;
            AddUnsupported(worldAllowsFalls);

            FallingDebris.Announce(_falling);
            for (int i = 0; i < _falling.Count; i++)
            {
                PieceNode node = _falling[i];
                if (!node.Alive)
                    continue;
                node.FellByFysik = true;
                if (FysikConfig.LogSolves.Value)
                    Plugin.Log.LogInfo($"{node.Name} gave way (" +
                                       (i < failed && !node.Attachment
                                           ? StructureManager.Describe(node.Result) + (node.Island.Cascading ? ", cascade" : "")
                                           : "lost its support") + ")");
                node.Wnt.ApplyDamage(node.Wnt.m_health * 10f + 1000f);
            }
        }

        private static bool CanFall(WearNTear wnt, bool worldAllowsFalls) =>
            worldAllowsFalls && wnt.m_noSupportWear && (wnt.m_piece == null || wnt.m_piece.CanBeRemoved());

        private readonly HashSet<PieceNode> _fallingSet = new HashSet<PieceNode>();
        private readonly HashSet<Island> _fallingIslands = new HashSet<Island>();
        private readonly HashSet<PieceNode> _grounded = new HashSet<PieceNode>();
        private readonly Queue<PieceNode> _queue = new Queue<PieceNode>();

        private void AddUnsupported(bool worldAllowsFalls)
        {
            _fallingSet.Clear();
            _fallingIslands.Clear();
            foreach (PieceNode n in _falling)
            {
                _fallingSet.Add(n);
                if (n.Island != null)
                    _fallingIslands.Add(n.Island);
            }

            foreach (Island island in _fallingIslands)
            {
                _grounded.Clear();
                _queue.Clear();
                foreach (PieceNode m in island.Members)
                    if (m.Alive && m.Grounded && !_fallingSet.Contains(m) && _grounded.Add(m))
                        _queue.Enqueue(m);
                while (_queue.Count > 0)
                {
                    foreach (Contact c in _queue.Dequeue().Contacts)
                        if (c.Other.Alive && c.Other.Island == island && !_fallingSet.Contains(c.Other) && _grounded.Add(c.Other))
                            _queue.Enqueue(c.Other);
                }
                foreach (PieceNode m in island.Members)
                {
                    if (!m.Alive || _grounded.Contains(m) || _fallingSet.Contains(m) || !Owned(m.Wnt) || !CanFall(m.Wnt, worldAllowsFalls))
                        continue;
                    _fallingSet.Add(m);
                    _falling.Add(m);
                }
            }

            for (int i = 0; i < _falling.Count; i++)
            {
                PieceNode n = _falling[i];
                if (n.Attachment)
                    continue;
                foreach (PieceNode a in n.Attached)
                {
                    if (!a.Alive || _fallingSet.Contains(a) || !a.ContactsValid || a.OnGround || HeldByOther(a) ||
                        !Owned(a.Wnt) || !CanFall(a.Wnt, worldAllowsFalls))
                        continue;
                    _fallingSet.Add(a);
                    _falling.Add(a);
                }
            }
        }

        private bool HeldByOther(PieceNode attachment)
        {
            foreach (Contact c in attachment.Contacts)
                if (c.Other.Standing && !_fallingSet.Contains(c.Other))
                    return true;
            return false;
        }

        private static bool AttachmentReady(PieceNode node)
        {
            if (!Readiness.Settled(node.Wnt))
                return false;
            if (node.ScanTrusted)
                return true;
            StructureManager.Instance.InvalidateContacts(node);
            return false;
        }

        private static bool Owned(WearNTear wnt) =>
            wnt != null && wnt.m_nview != null && wnt.m_nview.IsValid() && wnt.m_nview.IsOwner();

        private static void SetCrack(ZDO zdo, PieceNode node, long ticks)
        {
            zdo.Set(CrackKey, ticks);
            node.CrackTicks = ticks;
        }

        private bool IsReady(Island island)
        {
            if (!_ready.TryGetValue(island, out bool ready))
                _ready[island] = ready = ComputeReady(island);
            return ready;
        }

        private bool ComputeReady(Island island)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (PieceNode m in island.Members)
            {
                if (!m.Alive || !Readiness.Settled(m.Wnt))
                    return false;
                Vector3 p = m.Wnt.transform.position;
                if (first)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
            if (!Readiness.TerrainReady(bounds))
                return false;

            bool trusted = true;
            foreach (PieceNode m in island.Members)
            {
                if (m.ScanTrusted)
                    continue;
                StructureManager.Instance.InvalidateContacts(m);
                trusted = false;
            }
            return trusted;
        }
    }
}
