using System.Collections.Generic;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class FallingDebris
    {
        private const string RpcName = "Fysik_Fall";
        private const float ExpectSeconds = 10f;
        private const int MaxDebris = 400;
        private const float ColliderShrink = 0.8f;

        public static FallingDebris Instance { get; } = new FallingDebris();

        private static int s_effectLayer = -1;
        private static int s_restingMask;
        private static readonly Collider[] s_hits = new Collider[64];
        private static readonly HashSet<Renderer> s_lowLods = new HashSet<Renderer>();
        private static readonly List<Collider> s_colliders = new List<Collider>();

        private readonly Dictionary<ZDOID, float> _expected = new Dictionary<ZDOID, float>();
        private readonly List<ZDOID> _expired = new List<ZDOID>();
        private int _live;
        private float _effectWindow;
        private int _effectsInWindow;

        public void RegisterRpc() => ZRoutedRpc.instance.Register<ZPackage>(RpcName, OnFallRpc);

        public static void Announce(List<PieceNode> falling)
        {
            if (ZRoutedRpc.instance == null || falling.Count == 0)
                return;
            var package = new ZPackage();
            package.Write(falling.Count);
            foreach (PieceNode node in falling)
                package.Write(node.Id);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, package);
        }

        private void OnFallRpc(long sender, ZPackage package)
        {
            int count = package.ReadInt();
            float until = Time.time + ExpectSeconds;
            for (int i = 0; i < count; i++)
                _expected[package.ReadZDOID()] = until;
        }

        public void Tick()
        {
            if (_expected.Count == 0)
                return;
            _expired.Clear();
            foreach (KeyValuePair<ZDOID, float> kv in _expected)
                if (Time.time > kv.Value)
                    _expired.Add(kv.Key);
            foreach (ZDOID id in _expired)
                _expected.Remove(id);
        }

        public bool TryTakeOver(WearNTear wnt)
        {
            if (!FysikConfig.CollapseAnimation.Value || ZNet.instance == null || ZNet.instance.IsDedicated())
                return false;
            ZDO zdo = wnt.m_nview != null ? wnt.m_nview.GetZDO() : null;
            if (zdo == null || !_expected.Remove(zdo.m_uid) || _live >= MaxDebris)
                return false;
            return Spawn(wnt);
        }

        private bool Spawn(WearNTear wnt)
        {
            if (s_effectLayer < 0)
            {
                s_effectLayer = LayerMask.NameToLayer("effect");
                s_restingMask = LayerMask.GetMask("piece", "Default", "Default_small");
            }

            var root = new GameObject("fysik_debris") { layer = s_effectLayer };
            root.transform.SetPositionAndRotation(wnt.transform.position, wnt.transform.rotation);

            CollectLowLods(wnt);
            int meshes = 0;
            foreach (MeshRenderer r in wnt.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || s_lowLods.Contains(r))
                    continue;
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                var part = new GameObject("mesh") { layer = s_effectLayer };
                part.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                part.transform.localScale = r.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                part.transform.SetParent(root.transform, true);
                meshes++;
            }
            if (meshes == 0)
            {
                Object.Destroy(root);
                return false;
            }
            foreach (Renderer r in wnt.GetComponentsInChildren<Renderer>())
                r.enabled = false;

            PieceGeometry geometry = StructureManager.Instance.TryGetNode(wnt, out PieceNode node) ? node.Geometry : PieceGeometry.Of(wnt);
            s_colliders.Clear();
            foreach (Obb box in geometry.Boxes)
            {
                var part = new GameObject("box") { layer = s_effectLayer };
                part.transform.SetPositionAndRotation(box.Center, box.Rotation);
                BoxCollider collider = part.AddComponent<BoxCollider>();
                collider.size = box.Size * ColliderShrink;
                part.transform.SetParent(root.transform, true);
                s_colliders.Add(collider);

                int count = Physics.OverlapBoxNonAlloc(box.Center, box.Size * 0.5f + Vector3.one * 0.2f, s_hits, box.Rotation, s_restingMask);
                for (int i = 0; i < count; i++)
                    Physics.IgnoreCollision(collider, s_hits[i]);
            }
            if (s_colliders.Count == 0)
            {
                Object.Destroy(root);
                return false;
            }

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(node != null && node.Mass > 0 ? (float)node.Mass : 50f, 5f, 5000f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.isKinematic = true;
            root.AddComponent<Debris>().Init(wnt.m_destroyedEffect, Random.Range(0f, 0.08f));
            _live++;
            return true;
        }

        private static void CollectLowLods(WearNTear wnt)
        {
            s_lowLods.Clear();
            foreach (LODGroup group in wnt.GetComponentsInChildren<LODGroup>())
            {
                LOD[] lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    foreach (Renderer r in lods[i].renderers)
                        if (r != null)
                            s_lowLods.Add(r);
            }
        }

        public void OnDebrisGone() => _live = Mathf.Max(0, _live - 1);

        public void PlayDestroyed(EffectList effect, Vector3 position, Quaternion rotation)
        {
            if (effect == null)
                return;
            if (Time.time > _effectWindow)
            {
                _effectWindow = Time.time + 0.5f;
                _effectsInWindow = 0;
            }
            if (_effectsInWindow++ < 6)
                effect.Create(position, rotation);
        }
    }

    internal sealed class Debris : MonoBehaviour
    {
        private const float ImpactSpeed = 2.5f;
        private const float MaxFallSeconds = 8f;

        private EffectList _destroyed;
        private float _release;
        private bool _released, _done;
        private Rigidbody _body;

        public void Init(EffectList destroyed, float delay)
        {
            _destroyed = destroyed;
            _release = Time.time + delay;
            _body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_done)
                return;
            if (!_released)
            {
                if (Time.time < _release)
                    return;
                _released = true;
                _body.isKinematic = false;
                _body.linearVelocity = Vector3.down * 0.5f;
                _body.angularVelocity = Random.insideUnitSphere * 0.3f;
                return;
            }
            float age = Time.time - _release;
            if (age > MaxFallSeconds || (age > 0.6f && _body.linearVelocity.sqrMagnitude < 0.04f))
                Shatter();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_released && !_done && Time.time - _release > 0.1f && collision.relativeVelocity.magnitude > ImpactSpeed)
                Shatter();
        }

        private void Shatter()
        {
            _done = true;
            FallingDebris.Instance.PlayDestroyed(_destroyed, transform.position, transform.rotation);
            Destructible.CreateFragments(gameObject, visibleOnly: false);
            Destroy(gameObject);
        }

        private void OnDestroy() => FallingDebris.Instance.OnDebrisGone();
    }
}
