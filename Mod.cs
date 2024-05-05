using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using Trejak.BuildingOccupancyMod.Systems;
using UnityEngine;

namespace Trejak.BuildingOccupancyMod
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(BuildingOccupancyMod)}.{nameof(Mod)}").SetShowsErrorsInUI(false);      
        public static Setting m_Setting { get; private set; }

        private Harmony m_Harmony;

        //public static Colossal.Version version = Colossal.Version.GetCurrent(Assembly.GetExecutingAssembly());

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            InstantiateSettings();
            ApplyPatches();
            SetupSystems(updateSystem);
        }

        private void SetupSystems(UpdateSystem updateSystem)
        {
            updateSystem.UpdateAt<OccupancyPrefabInitSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<CheckBuildingsSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<ResetHouseholdsSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<MultiCommercialFindPropertySystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAfter<ModifiedCompanyFindPropertySystem, MultiCommercialFindPropertySystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateBefore<ModifiedCompanyFindPropertySystem, PropertyRenterRemoveSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<DebugUISystem>(SystemUpdatePhase.UIUpdate);
        }

        private void InstantiateSettings()
        {
            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(BuildingOccupancyMod), m_Setting, new Setting(this));
        }

        private void ApplyPatches()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();            

            m_Harmony = new Harmony(typeof(Mod).Namespace);
            m_Harmony.PatchAll(assembly);
            var patchedMethods = m_Harmony.GetPatchedMethods().ToArray<MethodBase>();

            log.Info($"Made patches! Patched methods: " + patchedMethods.Length);

            foreach (var patchedMethod in patchedMethods)
            {
                log.Info($"Patched method: {patchedMethod.Module.Name}:{patchedMethod.Name}");
            }
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
            if (m_Harmony != null)
            {
                m_Harmony.UnpatchAll();
            }
        }
    }
}
