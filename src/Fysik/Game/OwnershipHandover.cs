using System.Collections.Generic;
using UnityEngine;

namespace Fysik.Game
{
    internal static class OwnershipHandover
    {
        private const float Interval = 2f;

        private static float s_next;
        private static readonly List<ZDO> s_zdos = new List<ZDO>();
        private static readonly Dictionary<int, bool> s_isPiece = new Dictionary<int, bool>();

        public static void Tick()
        {
            if (Time.time < s_next)
                return;
            s_next = Time.time + Interval;

            ZNet net = ZNet.instance;
            ZDOMan zdoMan = ZDOMan.instance;
            ZNetScene scene = ZNetScene.instance;
            if (net == null || zdoMan == null || scene == null || !net.IsDedicated())
                return;
            List<ZNetPeer> peers = net.GetPeers();
            if (peers.Count == 0)
                return;

            long server = ZDOMan.GetSessionID();
            Vector2s zone = ZoneSystem.GetZone(net.GetReferencePosition());
            SimulationDistance synced = net.GetSyncedSimulationDistance();
            s_zdos.Clear();
            zdoMan.FindSectorObjects(zone, new SimulationDistance(synced.NearSimulationDistance, 0, synced.IsClassic), s_zdos);

            int handed = 0;
            foreach (ZDO zdo in s_zdos)
            {
                if (zdo.GetOwner() != server || !zdo.Persistent || !IsBuildingPiece(scene, zdo.GetPrefab()))
                    continue;
                Vector3 p = zdo.GetPosition();
                long nearest = 0;
                float nearestDistance = float.MaxValue;
                foreach (ZNetPeer peer in peers)
                {
                    if (!peer.IsReady() || !zdoMan.IsInPeerActiveArea(p, peer.m_uid))
                        continue;
                    float d = (peer.m_refPos - p).sqrMagnitude;
                    if (d < nearestDistance)
                    {
                        nearestDistance = d;
                        nearest = peer.m_uid;
                    }
                }
                if (nearest == 0)
                    continue;
                zdo.SetOwner(nearest);
                handed++;
            }
            if (handed > 0)
                Plugin.Log.LogInfo($"Handed {handed} building pieces near the world centre to the nearest players.");
        }

        private static bool IsBuildingPiece(ZNetScene scene, int prefab)
        {
            if (!s_isPiece.TryGetValue(prefab, out bool isPiece))
            {
                GameObject go = scene.GetPrefab(prefab);
                s_isPiece[prefab] = isPiece = go != null && go.GetComponent<WearNTear>() != null;
            }
            return isPiece;
        }
    }
}
