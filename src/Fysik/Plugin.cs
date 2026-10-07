using BepInEx;
using BepInEx.Configuration;
using Fysik.Game;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace Fysik
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            FysikConfig.Bind(Config);
            Config.SettingChanged += OnSettingChanged;
            SynchronizationManager.OnConfigurationSynchronized += OnConfigurationSynchronized;
            Texts.Register();
            CommandManager.Instance.AddConsoleCommand(new FysikCommand());

            ApplyPatches();

            Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded " +
                        $"(Valheim {Version.GetVersionString()}, crack warning {FysikConfig.CrackWarningSeconds.Value:0.#} s)");
        }

        private void ApplyPatches()
        {
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            int applied = 0, failed = 0;
            System.Type[] types;
            try
            {
                types = typeof(Plugin).Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException e)
            {
                types = e.Types;
            }
            foreach (System.Type type in types)
            {
                if (type == null || type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                    continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    Log.LogWarning($"Hook {type.Name} not applied, that feature is off (different game version?): " +
                                   e.GetBaseException().Message);
                }
            }
            if (failed > 0)
                Log.LogWarning($"{failed} of {applied + failed} hooks could not be applied.");
        }

        private void Update()
        {
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                try
                {
                    OwnershipHandover.Tick();
                }
                catch (System.Exception e)
                {
                    Guard.Report("Ownership handover", e);
                }
                return;
            }

            if (ZNetScene.instance == null)
            {
                if (StructureManager.Instance.NodeCount > 0)
                    StructureManager.Instance.Clear();
                return;
            }
            try
            {
                XRayView.Instance.Tick();
            }
            catch (System.Exception e)
            {
                Guard.Report("X-ray view", e);
            }
            try
            {
                StructureManager.Instance.Tick(FysikConfig.FrameBudgetMs.Value);
            }
            catch (System.Exception e)
            {
                Guard.Report("Structure solver", e);
                StructureManager.Instance.AbortJob();
            }
            try
            {
                CollapseController.Instance.Tick();
            }
            catch (System.Exception e)
            {
                Guard.Report("Collapse", e);
            }
            try
            {
                FallingDebris.Instance.Tick();
            }
            catch (System.Exception e)
            {
                Guard.Report("Collapse animation", e);
            }
            try
            {
                CrackEffects.Instance.Tick();
            }
            catch (System.Exception e)
            {
                Guard.Report("Crack effects", e);
            }
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            SynchronizationManager.OnConfigurationSynchronized -= OnConfigurationSynchronized;
            _harmony?.UnpatchSelf();
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            string section = args.ChangedSetting.Definition.Section;
            if (section.StartsWith("Material.") || section == "Structure" || args.ChangedSetting == FysikConfig.DamageWeakens)
                SolveAgain();
        }

        private static void OnConfigurationSynchronized(object sender, ConfigurationSynchronizationEventArgs args)
        {
            if (!args.UpdatedPluginGUIDs.Contains(MyPluginInfo.PLUGIN_GUID))
                return;
            Log.LogInfo($"Server config applied: crack warning {FysikConfig.CrackWarningSeconds.Value:0.#} s");
            SolveAgain();
        }

        private static void SolveAgain()
        {
            MaterialTable.Invalidate();
            StructureManager.Instance.InvalidateAllResults();
        }
    }
}
