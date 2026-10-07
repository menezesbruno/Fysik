using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace Fysik.Game
{
    internal static class Readiness
    {
        public const float LoadGraceSeconds = 10f;

        private const float AreaMargin = 16f;

        private static readonly Dictionary<Vector2s, bool> s_zoneReady = new Dictionary<Vector2s, bool>();
        private static int s_zoneFrame = -1;
        private static Func<Bounds, string, bool> s_voxheimCanSimulate;
        private static bool s_voxheimChecked;

        public static bool Settled(WearNTear w)
        {
            if (w == null)
                return false;
            if (w.m_createTime >= 0 && Time.time - w.m_createTime < LoadGraceSeconds)
                return false;
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
                return false;
            Vector3 p = w.transform.position;
            if (NearUnloadedArea(scene, p) || !ZoneReady(scene, p))
                return false;
            return TerrainReady(new Bounds(p, Vector3.one * 4f));
        }

        private static bool ZoneReady(ZNetScene scene, Vector3 p)
        {
            if (s_zoneFrame != Time.frameCount)
            {
                s_zoneFrame = Time.frameCount;
                s_zoneReady.Clear();
            }
            Vector2s zone = ZoneSystem.GetZone(p);
            if (!s_zoneReady.TryGetValue(zone, out bool ready))
                s_zoneReady[zone] = ready = scene.IsAreaReady(p);
            return ready;
        }

        private static bool NearUnloadedArea(ZNetScene scene, Vector3 p) =>
            scene.OutsideActiveArea(p) ||
            scene.OutsideActiveArea(p + new Vector3(AreaMargin, 0, AreaMargin)) ||
            scene.OutsideActiveArea(p + new Vector3(-AreaMargin, 0, AreaMargin)) ||
            scene.OutsideActiveArea(p + new Vector3(AreaMargin, 0, -AreaMargin)) ||
            scene.OutsideActiveArea(p + new Vector3(-AreaMargin, 0, -AreaMargin));

        public static bool TerrainReady(Bounds bounds)
        {
            if (!s_voxheimChecked)
            {
                s_voxheimChecked = true;
                if (Chainloader.PluginInfos.TryGetValue("com.bogey.voxheim", out BepInEx.PluginInfo voxheim) && voxheim.Instance != null)
                {
                    Type readiness = voxheim.Instance.GetType().Assembly.GetType("Voxheim.Unity.TerrainSimulationReadiness");
                    MethodInfo method = readiness?.GetMethod("CanSimulate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                                             null, new[] { typeof(Bounds), typeof(string) }, null);
                    if (method != null)
                    {
                        s_voxheimCanSimulate = (Func<Bounds, string, bool>)Delegate.CreateDelegate(typeof(Func<Bounds, string, bool>), method);
                        Plugin.Log.LogInfo("Voxheim detected: structures wait for its terrain before anything falls.");
                    }
                    else
                    {
                        Plugin.Log.LogWarning("Voxheim detected, but its terrain readiness check was not found; structures will not wait for it.");
                    }
                }
            }
            return s_voxheimCanSimulate == null || s_voxheimCanSimulate(bounds, "Fysik structure");
        }
    }
}
