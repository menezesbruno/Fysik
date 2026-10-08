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
            localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "fysik_compression", "Compression" },
                { "fysik_tension", "Tension" },
                { "fysik_bending", "Bending" },
                { "fysik_shear", "Shear" },
                { "fysik_torsion", "Torsion" },
                { "fysik_stress", "Stress" },
                { "fysik_unsupported", "No support" },
                { "fysik_overloaded", "would break" },
                { "fysik_cracking", "cracking!" },
                { "fysik_pending", "Calculating…" },
                { "fysik_world", "World building · not simulated" },
                { "fysik_loadpath", "simplified model" },
                { "fysik_preview", "Preview" },
                { "fysik_overloads", "overloads" },
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
            });
        }
    }
}
