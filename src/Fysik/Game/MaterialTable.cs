using System.Collections.Generic;
using Fysik.Structure;

namespace Fysik.Game
{
    internal static class MaterialTable
    {
        private static readonly Dictionary<int, MaterialProps> s_cache = new Dictionary<int, MaterialProps>();
        private static readonly HashSet<int> s_warned = new HashSet<int>();

        internal static void Invalidate() => s_cache.Clear();

        internal static MaterialProps For(WearNTear wnt)
        {
            int key = (int)wnt.m_materialType;
            if (s_cache.TryGetValue(key, out MaterialProps props))
                return props;

            if (!FysikConfig.Materials.TryGetValue(wnt.m_materialType, out FysikConfig.MaterialEntries entries))
            {
                entries = FysikConfig.Unknown;
                if (s_warned.Add(key))
                    Plugin.Log.LogWarning($"Unknown material type {key} on '{wnt.name}' (probably from another mod); " +
                                          "using the Material.Unknown settings.");
            }

            Stiffness(wnt.m_materialType, out double elasticity, out double shearRatio);
            props = new MaterialProps
            {
                Name = entries.Name,
                Density = entries.Density.Value,
                Elasticity = elasticity,
                ShearModulus = elasticity * shearRatio,
                CompressiveStrength = entries.Compression.Value * 1e6 * FysikConfig.StrengthMultiplier,
                TensileStrength = entries.Tension.Value * 1e6 * FysikConfig.StrengthMultiplier,
                ShearStrength = entries.Shear.Value * 1e6 * FysikConfig.StrengthMultiplier,
                BucklingSlenderness = entries.Slenderness.Value,
            };
            s_cache[key] = props;
            return props;
        }

        private static void Stiffness(WearNTear.MaterialType type, out double elasticity, out double shearRatio)
        {
            switch (type)
            {
                case WearNTear.MaterialType.Wood:
                    elasticity = 10e9; shearRatio = 1.0 / 16; break;
                case WearNTear.MaterialType.HardWood:
                case WearNTear.MaterialType.Timberwood:
                    elasticity = 12e9; shearRatio = 1.0 / 16; break;
                case WearNTear.MaterialType.Ancient:
                    elasticity = 15e9; shearRatio = 1.0 / 16; break;
                case WearNTear.MaterialType.Stone:
                case WearNTear.MaterialType.Ashstone:
                    elasticity = 30e9; shearRatio = 0.4; break;
                case WearNTear.MaterialType.Marble:
                    elasticity = 50e9; shearRatio = 0.4; break;
                case WearNTear.MaterialType.Iron:
                    elasticity = 100e9; shearRatio = 0.38; break;
                case WearNTear.MaterialType.Ice:
                    elasticity = 9e9; shearRatio = 0.38; break;
                default:
                    elasticity = 10e9; shearRatio = 1.0 / 16; break;
            }
        }
    }
}
