using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Trejak.BuildingOccupancyMod.Systems;

namespace Trejak.BuildingOccupancyMod
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(BuildingOccupancyMod)}.{nameof(Mod)}").SetShowsErrorsInUI(false);      
        public static Setting m_Setting { get; private set; }

        public static Colossal.Version version = Colossal.Version.GetCurrent();

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");            
            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(BuildingOccupancyMod), m_Setting, new Setting(this));

            updateSystem.UpdateAt<OccupancyPrefabInitSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<CheckBuildingsSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<ResetHouseholdsSystem>(SystemUpdatePhase.GameSimulation);            
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
