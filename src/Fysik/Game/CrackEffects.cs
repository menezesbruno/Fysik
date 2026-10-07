using System.Collections.Generic;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class CrackEffects
    {
        private const float ScanInterval = 0.25f;
        private const float EffectRadius = 80f;
        private const float EffectInterval = 1.3f;
        private const float ShakeAmplitude = 0.02f;

        private static readonly Color Bright = new Color(1f, 0.12f, 0.05f);
        private static readonly Color Dark = new Color(0.4f, 0.02f, 0.02f);

        public static CrackEffects Instance { get; } = new CrackEffects();

        private sealed class Visual
        {
            public readonly List<Transform> Shakers = new List<Transform>();
            public readonly List<Vector3> Rest = new List<Vector3>();
            public float Phase;
            public float NextEffect;
        }

        private readonly Dictionary<WearNTear, Visual> _active = new Dictionary<WearNTear, Visual>();
        private readonly List<WearNTear> _ended = new List<WearNTear>();
        private readonly Dictionary<string, GameObject> _sounds = new Dictionary<string, GameObject>();
        private float _nextScan;

        public bool IsCracking(WearNTear wnt) => _active.ContainsKey(wnt);

        public void Tick()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                _active.Clear();
                return;
            }
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                Scan(player.transform.position);
            }
            foreach (KeyValuePair<WearNTear, Visual> kv in _active)
                if (kv.Key != null)
                    Animate(kv.Key, kv.Value);
        }

        private void Scan(Vector3 center)
        {
            float r2 = EffectRadius * EffectRadius;
            StructureManager manager = StructureManager.Instance;
            foreach (WearNTear wnt in WearNTear.GetAllInstances())
            {
                if (wnt == null || wnt.m_nview == null || !wnt.m_nview.IsValid())
                    continue;
                long crack = wnt.m_nview.GetZDO().GetLong(CollapseController.CrackKey);
                if (manager.TryGetNode(wnt, out PieceNode node))
                    node.CrackTicks = crack;

                bool show = crack != 0 && (wnt.transform.position - center).sqrMagnitude <= r2;
                if (show && !_active.ContainsKey(wnt))
                    Begin(wnt);
                else if (!show && _active.ContainsKey(wnt))
                    _ended.Add(wnt);
            }

            foreach (WearNTear wnt in _active.Keys)
                if (wnt == null)
                    _ended.Add(wnt);
            foreach (WearNTear wnt in _ended)
                End(wnt);
            _ended.Clear();
        }

        private void Begin(WearNTear wnt)
        {
            var v = new Visual { Phase = Random.value * 10f, NextEffect = Time.time };
            foreach (Renderer r in wnt.GetComponentsInChildren<Renderer>())
            {
                Transform t = r.transform;
                if (t == wnt.transform || v.Shakers.Contains(t) || t.GetComponentInChildren<Collider>(true) != null)
                    continue;
                if (HasAncestorIn(t, v.Shakers, wnt.transform))
                    continue;
                v.Shakers.Add(t);
                v.Rest.Add(t.localPosition);
            }
            _active[wnt] = v;
        }

        private void End(WearNTear wnt)
        {
            if (!_active.TryGetValue(wnt, out Visual v))
                return;
            _active.Remove(wnt);
            if (wnt == null)
                return;
            for (int i = 0; i < v.Shakers.Count; i++)
                if (v.Shakers[i] != null)
                    v.Shakers[i].localPosition = v.Rest[i];
            if (XRayView.Instance.Holds(wnt))
                XRayView.Instance.Reapply(wnt);
            else
                StressDisplay.Reset(wnt);
        }

        private void Animate(WearNTear wnt, Visual v)
        {
            float t = Time.time + v.Phase;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f);
            StressDisplay.Apply(wnt, Color.Lerp(Dark, Bright, pulse));

            var offset = new Vector3(Mathf.Sin(t * 53f), 0.5f * Mathf.Sin(t * 61f + 1.3f), Mathf.Sin(t * 47f + 2.1f)) * ShakeAmplitude;
            for (int i = 0; i < v.Shakers.Count; i++)
                if (v.Shakers[i] != null)
                    v.Shakers[i].localPosition = v.Rest[i] + offset;

            if (Time.time < v.NextEffect)
                return;
            v.NextEffect = Time.time + EffectInterval * Random.Range(0.8f, 1.2f);
            Vector3 position = wnt.transform.position;
            wnt.m_hitEffect.Create(position, wnt.transform.rotation, wnt.transform);
            GameObject creak = Sound(IsStone(wnt.m_materialType) ? "sfx_rock_wall_crumble" : "sfx_wood_break");
            if (creak != null)
                Object.Instantiate(creak, position, Quaternion.identity);
        }

        private GameObject Sound(string name)
        {
            if (!_sounds.TryGetValue(name, out GameObject prefab))
            {
                prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                _sounds[name] = prefab;
                if (prefab == null)
                    Plugin.Log.LogWarning($"Sound '{name}' not found; cracking pieces will only use their hit effect.");
            }
            return prefab;
        }

        private static bool IsStone(WearNTear.MaterialType type) =>
            type == WearNTear.MaterialType.Stone || type == WearNTear.MaterialType.Marble ||
            type == WearNTear.MaterialType.Ashstone || type == WearNTear.MaterialType.Ice;

        private static bool HasAncestorIn(Transform t, List<Transform> set, Transform root)
        {
            for (Transform p = t.parent; p != null && p != root; p = p.parent)
                if (set.Contains(p))
                    return true;
            return false;
        }
    }
}
