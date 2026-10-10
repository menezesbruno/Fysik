using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class MovingLoads
    {
        private const int MaxSupports = 4;
        private const float Interval = 0.25f;
        private const float AirborneSeconds = 1.5f;
        private const double RoundTo = 5;
        private const float WheelReach = 0.5f;

        private static readonly int s_countKey = "fysik_loads".GetStableHashCode();
        private static readonly KeyValuePair<int, int>[] s_pieceKeys = new KeyValuePair<int, int>[MaxSupports];
        private static readonly int[] s_massKeys = new int[MaxSupports];
        private static readonly RaycastHit[] s_hits = new RaycastHit[16];
        private static int s_mask;

        static MovingLoads()
        {
            for (int i = 0; i < MaxSupports; i++)
            {
                s_pieceKeys[i] = ZDO.GetHashZDOID("fysik_on" + i);
                s_massKeys[i] = ("fysik_kg" + i).GetStableHashCode();
            }
        }

        public static MovingLoads Instance { get; } = new MovingLoads();

        private struct Support
        {
            public ZDOID Piece;
            public double Mass;
        }

        private struct Entry
        {
            public ZDOID Mover;
            public int Slot;
            public ZDOID Piece;
            public double Mass;
        }

        private float _next;
        private float _offGroundSince = -1;
        private WearNTear _lastStand;
        private readonly List<Support> _supports = new List<Support>();
        private readonly List<Entry> _entries = new List<Entry>();
        private Dictionary<PieceNode, double> _riding = new Dictionary<PieceNode, double>();
        private Dictionary<PieceNode, double> _collected = new Dictionary<PieceNode, double>();
        private readonly List<PieceNode> _changed = new List<PieceNode>();
        private readonly Dictionary<string, Recipe> _cartRecipes = new Dictionary<string, Recipe>();

        public void Tick()
        {
            if (ZNetScene.instance == null || Time.time < _next)
                return;
            _next = Time.time + Interval;
            bool on = FysikConfig.LiveLoads.Value;
            if (on)
            {
                PublishPlayer(Player.m_localPlayer);
                foreach (Vagon cart in Vagon.m_instances)
                    PublishCart(cart);
            }
            Collect(on);
        }

        public void Clear()
        {
            _riding.Clear();
            _collected.Clear();
            _entries.Clear();
            _lastStand = null;
            _offGroundSince = -1;
        }

        private void PublishPlayer(Player player)
        {
            if (player == null || player.m_nview == null || !player.m_nview.IsValid() || !player.m_nview.IsOwner())
                return;
            _supports.Clear();
            WearNTear stand = StandingOn(player);
            if (stand != null)
            {
                double mass = FysikConfig.PlayerWeight.Value + player.GetInventory().GetTotalWeight() * FysikConfig.ContentsWeight.Value;
                _supports.Add(new Support { Piece = stand.m_nview.GetZDO().m_uid, Mass = Round(mass) });
            }
            Write(player.m_nview.GetZDO(), _supports);
        }

        private WearNTear StandingOn(Player player)
        {
            if (player.IsDead())
            {
                _lastStand = null;
                return null;
            }
            if (player.m_attached)
            {
                _offGroundSince = -1;
                _lastStand = Carrier(player.m_attachPoint != null ? player.m_attachPoint.GetComponentInParent<WearNTear>() : null);
                return _lastStand;
            }
            if (player.IsOnGround())
            {
                _offGroundSince = -1;
                _lastStand = Carrier(player.m_lastGroundCollider != null
                                         ? player.m_lastGroundCollider.GetComponentInParent<WearNTear>()
                                         : null);
                return _lastStand;
            }
            if (_offGroundSince < 0)
                _offGroundSince = Time.time;
            if (Time.time - _offGroundSince > AirborneSeconds)
                _lastStand = null;
            return _lastStand;
        }

        private void PublishCart(Vagon cart)
        {
            if (cart == null || cart.m_nview == null || !cart.m_nview.IsValid() || !cart.m_nview.IsOwner())
                return;
            _supports.Clear();
            double mass = Round(CartMass(cart));
            if (cart.m_wheels != null && cart.m_wheels.Length > 0)
            {
                foreach (Rigidbody wheel in cart.m_wheels)
                    if (wheel != null)
                        AddSupport(Below(wheel.position, Radius(wheel) + WheelReach), mass / cart.m_wheels.Length);
            }
            else
            {
                AddSupport(Below(cart.transform.position + Vector3.up * WheelReach, 2 * WheelReach), mass);
            }
            Write(cart.m_nview.GetZDO(), _supports);
        }

        private void AddSupport(WearNTear under, double mass)
        {
            if (under == null)
                return;
            ZDOID id = under.m_nview.GetZDO().m_uid;
            for (int i = 0; i < _supports.Count; i++)
            {
                if (_supports[i].Piece != id)
                    continue;
                _supports[i] = new Support { Piece = id, Mass = _supports[i].Mass + mass };
                return;
            }
            if (_supports.Count < MaxSupports)
                _supports.Add(new Support { Piece = id, Mass = mass });
        }

        private double CartMass(Vagon cart)
        {
            string prefab = cart.gameObject.name;
            if (!_cartRecipes.TryGetValue(prefab, out Recipe recipe))
                _cartRecipes[prefab] = recipe = Recipe.Of(cart.GetComponent<Piece>());
            double own = recipe.Mass > 0 ? recipe.Mass : cart.m_baseMass;
            return own + ExtraLoads.ContentsOf(cart.m_container);
        }

        private static float Radius(Rigidbody wheel)
        {
            Collider collider = wheel.GetComponent<Collider>();
            return collider != null ? collider.bounds.extents.y : 0.5f;
        }

        private static WearNTear Below(Vector3 origin, float reach)
        {
            if (s_mask == 0)
                s_mask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, s_hits, reach, s_mask, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.collider.attachedRigidbody != null || hit.distance >= best)
                    continue;
                best = hit.distance;
                nearest = hit.collider;
            }
            return nearest != null ? Carrier(nearest.GetComponentInParent<WearNTear>()) : null;
        }

        private static WearNTear Carrier(WearNTear wnt)
        {
            if (wnt == null || wnt.m_nview == null || !wnt.m_nview.IsValid())
                return null;
            PieceNode node = StructureManager.Instance.GetNode(wnt);
            return node != null && (node.Structural || node.Attachment) && !node.Anchored ? wnt : null;
        }

        private static double Round(double mass) => Math.Round(mass / RoundTo) * RoundTo;

        private static void Write(ZDO zdo, List<Support> supports)
        {
            bool same = zdo.GetInt(s_countKey) == supports.Count;
            for (int i = 0; same && i < supports.Count; i++)
                same = zdo.GetZDOID(s_pieceKeys[i]) == supports[i].Piece && zdo.GetFloat(s_massKeys[i]) == (float)supports[i].Mass;
            if (same)
                return;
            zdo.Set(s_countKey, supports.Count);
            for (int i = 0; i < supports.Count; i++)
            {
                zdo.Set(s_pieceKeys[i], supports[i].Piece);
                zdo.Set(s_massKeys[i], (float)supports[i].Mass);
            }
        }

        private void Collect(bool on)
        {
            _entries.Clear();
            if (on)
            {
                foreach (Player player in Player.GetAllPlayers())
                    Read(player != null ? player.m_nview : null);
                foreach (Vagon cart in Vagon.m_instances)
                    Read(cart != null ? cart.m_nview : null);
            }
            _entries.Sort(CompareEntries);

            StructureManager manager = StructureManager.Instance;
            _collected.Clear();
            foreach (Entry e in _entries)
            {
                GameObject instance = ZNetScene.instance.FindInstance(e.Piece);
                WearNTear wnt = instance != null ? instance.GetComponent<WearNTear>() : null;
                PieceNode node = wnt != null ? manager.GetNode(wnt) : null;
                if (node == null || !(node.Structural || node.Attachment) || node.Anchored)
                    continue;
                _collected.TryGetValue(node, out double mass);
                _collected[node] = mass + e.Mass;
            }

            _changed.Clear();
            foreach (KeyValuePair<PieceNode, double> kv in _riding)
                if (!_collected.ContainsKey(kv.Key))
                    _changed.Add(kv.Key);
            foreach (KeyValuePair<PieceNode, double> kv in _collected)
                if (!_riding.TryGetValue(kv.Key, out double old) || old != kv.Value)
                    _changed.Add(kv.Key);

            Dictionary<PieceNode, double> previous = _riding;
            _riding = _collected;
            _collected = previous;
            foreach (PieceNode node in _changed)
            {
                node.Riding = _riding.TryGetValue(node, out double mass) ? mass : 0;
                manager.ReloadLoads(node);
            }
        }

        private void Read(ZNetView view)
        {
            if (view == null || !view.IsValid())
                return;
            ZDO zdo = view.GetZDO();
            int count = Math.Min(zdo.GetInt(s_countKey), MaxSupports);
            for (int i = 0; i < count; i++)
            {
                ZDOID piece = zdo.GetZDOID(s_pieceKeys[i]);
                double mass = zdo.GetFloat(s_massKeys[i]);
                if (!piece.IsNone() && mass > 0)
                    _entries.Add(new Entry { Mover = zdo.m_uid, Slot = i, Piece = piece, Mass = mass });
            }
        }

        private static int CompareEntries(Entry a, Entry b)
        {
            int c = a.Mover.UserID.CompareTo(b.Mover.UserID);
            if (c == 0)
                c = a.Mover.ID.CompareTo(b.Mover.ID);
            return c != 0 ? c : a.Slot.CompareTo(b.Slot);
        }
    }
}
