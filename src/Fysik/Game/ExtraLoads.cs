using System;
using UnityEngine;

namespace Fysik.Game
{
    internal static class ExtraLoads
    {
        public const int SnowLevels = 4;

        public static double ContentsOf(Container container)
        {
            Inventory inventory = container != null && FysikConfig.LiveLoads.Value ? container.GetInventory() : null;
            return inventory == null ? 0 : Math.Round(inventory.GetTotalWeight() * FysikConfig.ContentsWeight.Value);
        }

        public static int SnowLevel(WearNTear wnt)
        {
            if (!FysikConfig.LiveLoads.Value || FysikConfig.SnowWeight.Value <= 0 || wnt == null || wnt.m_nview == null ||
                !wnt.m_nview.IsValid())
                return 0;
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null || zones.GetGlobalKey(GlobalKeys.NoHeavySnow) ||
                (wnt.m_snowDamageImmune && !zones.GetGlobalKey(GlobalKeys.AllHeavySnow)))
                return 0;
            float buildup = wnt.m_nview.GetZDO().GetFloat(ZDOVars.s_snow);
            return Mathf.Clamp(Mathf.FloorToInt(buildup * SnowLevels + 0.001f), 0, SnowLevels);
        }

        public static double SnowMass(PieceGeometry geometry, int level) =>
            level <= 0 ? 0 : Math.Round(FysikConfig.SnowWeight.Value * geometry.SnowArea * level / SnowLevels);
    }
}
