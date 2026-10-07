using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Fysik
{
    internal enum StructureMode
    {
        Physics,

        DisplayOnly,

        Sandbox,
    }

    internal enum Difficulty
    {
        Relaxed,

        Normal,

        Strict,
    }

    internal static class FysikConfig
    {
        internal static ConfigEntry<StructureMode> Mode { get; private set; }
        internal static ConfigEntry<float> CrackWarningSeconds { get; private set; }
        internal static ConfigEntry<bool> DamageWeakens { get; private set; }
        internal static ConfigEntry<int> MaxFrameBodies { get; private set; }
        internal static ConfigEntry<Difficulty> StructureDifficulty { get; private set; }

        internal static double StrengthMultiplier =>
            StructureDifficulty.Value == Difficulty.Relaxed ? 1.5 : StructureDifficulty.Value == Difficulty.Strict ? 0.7 : 1.0;

        internal static ConfigEntry<float> FrameBudgetMs { get; private set; }
        internal static ConfigEntry<bool> BackgroundSolver { get; private set; }
        internal static ConfigEntry<bool> HammerInfo { get; private set; }
        internal static ConfigEntry<bool> PlacementPreview { get; private set; }
        internal static ConfigEntry<bool> CollapseAnimation { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> XRayKey { get; private set; }
        internal static ConfigEntry<float> XRayRadius { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> XRayRadiusKey { get; private set; }

        internal const float XRayRadiusMin = 40f, XRayRadiusMax = 80f, XRayRadiusStep = 10f;
        internal static ConfigEntry<bool> LogSolves { get; private set; }
        internal static ConfigEntry<bool> LogGeometry { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> DumpKey { get; private set; }

        internal static readonly Dictionary<WearNTear.MaterialType, MaterialEntries> Materials =
            new Dictionary<WearNTear.MaterialType, MaterialEntries>();

        internal static MaterialEntries Unknown { get; private set; }

        internal sealed class MaterialEntries
        {
            public string Name;
            public ConfigEntry<float> Density, Compression, Tension, Shear, Slenderness;
        }

        internal static void Bind(ConfigFile config)
        {
            Mode = config.Bind(
                "Structure", "Mode", StructureMode.Physics,
                Server("Physics: overloaded pieces crack, then fall (vanilla support is replaced). " +
                       "DisplayOnly: vanilla decides what falls, Fysik only shows the forces. " +
                       "Sandbox: nothing falls for lack of support, Fysik shows the forces (calibration, test worlds).",
                       null));

            CrackWarningSeconds = config.Bind(
                "Failure", "CrackWarningSeconds", 5f,
                Server("Seconds an overloaded piece stays cracked (red, shaking) before it falls, " +
                       "giving time to shore it up. Once the first piece falls, the rest of the collapse is instant.",
                       new AcceptableValueRange<float>(0f, 60f)));

            DamageWeakens = config.Bind(
                "Failure", "DamageWeakens", true,
                Server("Badly damaged pieces are weaker: down to half health they keep their full strength (rain wear " +
                       "alone never weakens them); below that, strength falls to 25% at the brink of breaking, so a " +
                       "pillar under attack can crack under the same load. Off: damage never changes strength.", null));

            MaxFrameBodies = config.Bind(
                "Structure", "MaxFrameBodies", 2000,
                Server("Largest connected structure (pieces) solved with the full frame analysis. Bigger ones use a " +
                       "simplified load-path model, which does not recognise arches. Higher values recognise arches in big " +
                       "bases but take longer to update (about 0.5 s for 2000 pieces, 2 s for 5000, on a worker thread).",
                       new AcceptableValueRange<int>(500, 5000)));

            StructureDifficulty = config.Bind(
                "Structure", "Difficulty", Difficulty.Normal,
                Server("Relaxed: materials 50% stronger (longer spans and cantilevers). Normal. " +
                       "Strict: materials 30% weaker (structures need more thought).", null));

            FrameBudgetMs = config.Bind(
                "Performance", "FrameBudgetMs", 1.5f,
                new ConfigDescription("Milliseconds per frame spent on structural calculations; the rest waits for the next frame.",
                                      new AcceptableValueRange<float>(0.25f, 10f)));

            BackgroundSolver = config.Bind(
                "Performance", "BackgroundSolver", true,
                "Solve structures on a worker thread, so big bases update in about a second without taking frame time. " +
                "Off: the solver shares the per-frame budget (slower for big structures).");

            HammerInfo = config.Bind(
                "Display", "HammerInfo", true,
                "Color the piece under the hammer by Fysik stress (blue = relaxed, red = at its limit) instead of vanilla support, and show its value next to the crosshair.");

            PlacementPreview = config.Bind(
                "Display", "PlacementPreview", true,
                "While placing a piece, color it by the stress it would have once placed, and warn if it would overload another piece.");

            CollapseAnimation = config.Bind(
                "Display", "CollapseAnimation", true,
                "Pieces brought down by Fysik fall together as debris and burst when they hit something, like a building " +
                "being imploded. Off: they shatter where they stand, as in vanilla.");

            XRayKey = config.Bind(
                "Display", "XRayKey", new KeyboardShortcut(KeyCode.F7),
                "Toggles the X-ray view: every piece nearby is colored by stress.");

            XRayRadius = config.Bind(
                "Display", "XRayRadius", 40f,
                new ConfigDescription("X-ray view radius, metres. Also changed in game with XRayRadiusKey.",
                                      new AcceptableValueRange<float>(XRayRadiusMin, XRayRadiusMax)));

            XRayRadiusKey = config.Bind(
                "Display", "XRayRadiusKey", new KeyboardShortcut(KeyCode.F7, KeyCode.LeftControl),
                "Widens the X-ray radius by 10 m, from 40 m up to 80 m, then back to 40 m.");

            LogSolves = config.Bind(
                "Debug", "LogSolves", true,
                "Write to the log each structure solved (size, solver, time, worst piece) and each piece that cracks or falls.");

            DumpKey = config.Bind(
                "Debug", "DumpKey", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "Writes the structure under the hammer to BepInEx/fysik-dump.txt (same as the console command 'fysik dump').");

            LogGeometry = config.Bind(
                "Debug", "LogGeometry", true,
                "Write one line to the log per kind of piece, the first time it is seen: colliders, box, volume and weight. For calibration.");

            BindMaterial(config, WearNTear.MaterialType.Wood, "Wood", 500, 4, 4, 1, 60);
            BindMaterial(config, WearNTear.MaterialType.HardWood, "HardWood", 600, 6, 6, 1.5f, 65);
            BindMaterial(config, WearNTear.MaterialType.Timberwood, "Timberwood", 650, 8, 8, 2, 70);
            BindMaterial(config, WearNTear.MaterialType.Ancient, "Ancient", 700, 15, 15, 3, 80);
            BindMaterial(config, WearNTear.MaterialType.Stone, "Stone", 2400, 20, 0.8f, 2, 30);
            BindMaterial(config, WearNTear.MaterialType.Marble, "Marble", 2700, 30, 1.5f, 3, 35);
            BindMaterial(config, WearNTear.MaterialType.Ashstone, "Ashstone", 2300, 25, 1.2f, 2.5f, 30);
            BindMaterial(config, WearNTear.MaterialType.Iron, "Iron", 1200, 20, 20, 10, 90);
            BindMaterial(config, WearNTear.MaterialType.Ice, "Ice", 920, 5, 0.5f, 0.8f, 30);
            Unknown = BindMaterialSection(config, "Material.Unknown", 600, 3, 3, 0.8f, 50,
                                          "Pieces whose material Fysik does not know (other mods)");
            Unknown.Name = "unknown";
        }

        private static void BindMaterial(ConfigFile config, WearNTear.MaterialType type, string name,
                                         float density, float compression, float tension, float shear, float slenderness)
        {
            MaterialEntries entries = BindMaterialSection(config, "Material." + name, density, compression, tension, shear,
                                                          slenderness, name);
            entries.Name = name;
            Materials[type] = entries;
        }

        private static MaterialEntries BindMaterialSection(ConfigFile config, string section, float density, float compression,
                                                           float tension, float shear, float slenderness, string what)
        {
            return new MaterialEntries
            {
                Density = config.Bind(section, "Density", density,
                    Server($"{what}: density, kg/m³ (the weight of a piece comes from its volume).",
                           new AcceptableValueRange<float>(10f, 20000f))),
                Compression = config.Bind(section, "CompressiveStrength", compression,
                    Server($"{what}: compressive strength, MPa.", new AcceptableValueRange<float>(0.01f, 1000f))),
                Tension = config.Bind(section, "TensileStrength", tension,
                    Server($"{what}: tensile strength, MPa (also limits bending).", new AcceptableValueRange<float>(0.01f, 1000f))),
                Shear = config.Bind(section, "ShearStrength", shear,
                    Server($"{what}: shear strength, MPa.", new AcceptableValueRange<float>(0.01f, 1000f))),
                Slenderness = config.Bind(section, "BucklingSlenderness", slenderness,
                    Server($"{what}: slenderness (length / radius of gyration) at which the compressive limit halves. Lower = buckles sooner.",
                           new AcceptableValueRange<float>(5f, 500f))),
            };
        }

        private static ConfigDescription Server(string description, AcceptableValueBase range) =>
            new ConfigDescription(description + " Enforced by the server.", range,
                                  new ConfigurationManagerAttributes { IsAdminOnly = true });
    }
}
