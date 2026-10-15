using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace Fysik.Game
{
    internal static class Texts
    {
        public static void Register()
        {
            CustomLocalization localization = LocalizationManager.Instance.GetLocalization();
            var texts = new Dictionary<string, string>
            {
                { "fysik_compression", "Compression" },
                { "fysik_tension", "Tension" },
                { "fysik_bending", "Bending" },
                { "fysik_shear", "Shear" },
                { "fysik_torsion", "Torsion" },
                { "fysik_tipping", "Tipping" },
                { "fysik_sliding", "Sliding" },
                { "fysik_unstable", "Unstable" },
                { "fysik_would_fall", "would fall" },
                { "fysik_stress", "Stress" },
                { "fysik_unsupported", "No support" },
                { "fysik_overloaded", "would break" },
                { "fysik_cracking", "cracking!" },
                { "fysik_pending", "Calculating…" },
                { "fysik_world", "World building · not simulated" },
                { "fysik_loadpath", "simplified model" },
                { "fysik_preview", "Preview" },
                { "fysik_overloads", "overloads" },
                { "fysik_holds", "Holds" },
                { "fysik_squeezed", "Squeezed" },
                { "fysik_stretched", "Stretched" },
                { "fysik_snow", "Snow" },
                { "fysik_rests_on", "Rests on" },
                { "fysik_ground_terrain", "the ground" },
                { "fysik_ground_rock", "rock" },
                { "fysik_ground_tree", "a tree" },
                { "fysik_ground_world", "a world building" },
                { "fysik_damaged", "Damaged" },
                { "fysik_strength", "strength" },
                { "fysik_xray_on", "Fysik X-ray on" },
                { "fysik_xray_off", "Fysik X-ray off" },
                { "fysik_xray_radius", "Fysik X-ray radius:" },
                { "fysik_mat_wood", "wood" },
                { "fysik_mat_hardwood", "core wood" },
                { "fysik_mat_timberwood", "timberwood" },
                { "fysik_mat_ancient", "ancient" },
                { "fysik_mat_stone", "stone" },
                { "fysik_mat_marble", "black marble" },
                { "fysik_mat_ashstone", "grausten" },
                { "fysik_mat_iron", "iron" },
                { "fysik_mat_ice", "ice" },
                { "fysik_mat_unknown", "unknown material" },
            };
            foreach (ArchStone stone in ArchStones.All)
            {
                texts[stone.Token] = $"Arch stone, {stone.Span} m arch";
                texts[stone.Token + "_description"] =
                    $"A stone cut as a wedge for arches: {stone.Shape.Stones} make a half circle {stone.Span} m wide and " +
                    $"{stone.Span / 2} m high inside, standing on supports 1 m wide. Aim at the top of a wall or pillar to " +
                    "set the first one, then aim at a stone to lay the next one along the curve.";
            }
            localization.AddTranslation("English", texts);
        }
    }
}
