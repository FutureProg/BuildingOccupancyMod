using Colossal;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using Game.Simulation;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;
using Trejak.BuildingOccupancyMod.Systems;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod
{
    [FileLocation(nameof(BuildingOccupancyMod))]
    [SettingsUIGroupOrder(kDefaultGroup, kOverflowMaintenanceGroup, kDebugGroup)]
    [SettingsUIShowGroupName(kOverflowMaintenanceGroup, kDebugGroup)]
    //[SettingsUIGroupOrder(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup)]
    //[SettingsUIShowGroupName(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup)]
    public class Setting : ModSetting
    {        
        public const string kSection = "Main";

        public const string kDefaultGroup = "Default";
        public const string kDebugGroup = "Debug";
        public const string kOverflowMaintenanceGroup = "Maintenance";

        public static bool showLoadNotification;

        //public const string kButtonGroup = "Button";
        //public const string kToggleGroup = "Toggle";
        //public const string kSliderGroup = "Slider";
        //public const string kDropdownGroup = "Dropdown";

        public Setting(IMod mod) : base(mod)
        {

        }

        public static bool IsInGame()
        {
            if (GameManager.instance)
            {
                return GameManager.instance.gameMode.IsGame();
            }            
            return false;
        }

        [SettingsUISection(kSection, kDefaultGroup)]
        public bool EnableLoadNotification
        {
            get => showLoadNotification;
            set {
                showLoadNotification = value;
            }
        }

        [SettingsUIDeveloper]
        [SettingsUISection(kSection, kDebugGroup)]
        public bool EnableInstantBuilding
        {
            get => World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ZoneSpawnSystem>().debugFastSpawn;            
            set
            {
                World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ZoneSpawnSystem>().debugFastSpawn = value;
            }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUIHideByCondition(typeof(Setting), "IsInGame")]
        [SettingsUISection(kSection, kOverflowMaintenanceGroup)]
        public bool SeekNewHouseholds
        {
            set
            {
                ResetHouseholdsSystem.TriggerReset(Components.ResetType.FindNewHome);
            }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUIHideByCondition(typeof(Setting), "IsInGame")]
        [SettingsUISection(kSection, kOverflowMaintenanceGroup)]
        public bool DeleteOverflowHouseholds
        {
            set
            {
                ResetHouseholdsSystem.TriggerReset(Components.ResetType.Delete);
            }
        }
        
        public override void SetDefaults()
        {
            EnableInstantBuilding = false;
            EnableLoadNotification = false;
        }

        //[SettingsUISection(kSection, kButtonGroup)]
        //public bool Button { set { Mod.log.Info("Button clicked"); } }

        //[SettingsUIButton]
        //[SettingsUIConfirmation]
        //[SettingsUISection(kSection, kButtonGroup)]
        //public bool ButtonWithConfirmation { set { Mod.log.Info("ButtonWithConfirmation clicked"); } }

        //[SettingsUISection(kSection, kToggleGroup)]
        //public bool Toggle { get; set; }

        //[SettingsUISlider(min = 0, max = 100, step = 1, scalarMultiplier = 1, unit = Unit.kDataMegabytes)]
        //[SettingsUISection(kSection, kSliderGroup)]
        //public int IntSlider { get; set; }

        //[SettingsUIDropdown(typeof(Setting), nameof(GetIntDropdownItems))]
        //[SettingsUISection(kSection, kDropdownGroup)]
        //public int IntDropdown { get; set; }

        //[SettingsUISection(kSection, kDropdownGroup)]
        //public SomeEnum EnumDropdown { get; set; } = SomeEnum.Value1;

        //public DropdownItem<int>[] GetIntDropdownItems()
        //{
        //    var items = new List<DropdownItem<int>>();

        //    for (var i = 0; i < 3; i += 1)
        //    {
        //        items.Add(new DropdownItem<int>()
        //        {
        //            value = i,
        //            displayName = i.ToString(),
        //        });
        //    }

        //    return items.ToArray();
        //}

        //public override void SetDefaults()
        //{
        //    throw new System.NotImplementedException();
        //}

        //public enum SomeEnum
        //{
        //    Value1,
        //    Value2,
        //    Value3,
        //}
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Realistic Building Occupancy" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                {m_Setting.GetOptionGroupLocaleID(Setting.kDefaultGroup), "Default" },
                {m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableLoadNotification)), "Mod Loaded Notification" },
                {m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableLoadNotification)), "Enable for a notification to be sent in-game when the mod has been successfully loaded."},

                { m_Setting.GetOptionGroupLocaleID(Setting.kDebugGroup), "Debug" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableInstantBuilding)), "Fast Building Spawning" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableInstantBuilding)), $"Buildings are constructed immediately" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kOverflowMaintenanceGroup), "Overflow Maintenance" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SeekNewHouseholds)), "Seek New Households" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SeekNewHouseholds)), $"RECOMMENDED: If any building has more households than properties (usually when this mod is started with a pre-existing existing save), click this button to have some households look for a new home while the simulation plays. Effect is near immediate, so be aware." },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.SeekNewHouseholds)), "Read the setting description first and prepare for residents to move out. The other option won't work, and this can't be undone. Are you sure you want to reset the households?"},

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DeleteOverflowHouseholds)), "Delete Overflow Households" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DeleteOverflowHouseholds)), $"USE WITH CAUTION: If any building has more households than properties (usually when this mod is started with a pre-existing existing save), click this button to remove those households. This change is abrupt and immediate after pressing play." },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.DeleteOverflowHouseholds)), "Read the setting description first and prepare for a large drop in population. The other option won't work, and this can't be undone. Are you sure you want to delete the overflow households?"}

                //{ m_Setting.GetOptionGroupLocaleID(Setting.kButtonGroup), "Buttons" },
                //{ m_Setting.GetOptionGroupLocaleID(Setting.kToggleGroup), "Toggle" },
                //{ m_Setting.GetOptionGroupLocaleID(Setting.kSliderGroup), "Sliders" },
                //{ m_Setting.GetOptionGroupLocaleID(Setting.kDropdownGroup), "Dropdowns" },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.Button)), "Button" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(Setting.Button)), $"Simple single button. It should be bool property with only setter or use [{nameof(SettingsUIButtonAttribute)}] to make button from bool property with setter and getter" },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ButtonWithConfirmation)), "Button with confirmation" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ButtonWithConfirmation)), $"Button can show confirmation message. Use [{nameof(SettingsUIConfirmationAttribute)}]" },
                //{ m_Setting.GetOptionWarningLocaleID(nameof(Setting.ButtonWithConfirmation)), "is it confirmation text which you want to show here?" },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.Toggle)), "Toggle" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(Setting.Toggle)), $"Use bool property with setter and getter to get toggable option" },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntSlider)), "Int slider" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(Setting.IntSlider)), $"Use int property with getter and setter and [{nameof(SettingsUISliderAttribute)}] to get int slider" },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntDropdown)), "Int dropdown" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(Setting.IntDropdown)), $"Use int property with getter and setter and [{nameof(SettingsUIDropdownAttribute)}(typeof(SomeType), nameof(SomeMethod))] to get int dropdown: Method must be static or instance of your setting class with 0 parameters and returns {typeof(DropdownItem<int>).Name}" },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnumDropdown)), "Simple enum dropdown" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnumDropdown)), $"Use any enum property with getter and setter to get enum dropdown" },

                //{ m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value1), "Value 1" },
                //{ m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value2), "Value 2" },
                //{ m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value3), "Value 3" },
            };
        }

        public void Unload()
        {

        }
    }
}
