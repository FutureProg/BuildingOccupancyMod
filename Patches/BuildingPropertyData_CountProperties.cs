using Game.Economy;
using Game.Prefabs;
using Game.Zones;

namespace Trejak.BuildingOccupancyMod.Patches
{
    //[HarmonyLib.HarmonyPatch(typeof(BuildingPropertyData), "CountProperties", new System.Type[] {typeof(AreaType)})]
    class BuildingPropertyData_CountProperties
    {

        static bool Prefix(BuildingPropertyData __instance, ref int __result, AreaType areaType)
        {
            switch (areaType)
            {
                case AreaType.Residential:
                    return true;
                case AreaType.Commercial:
                    if (__instance.m_AllowedSold == Resource.NoResource)
                    {
                        __result = 0;
                        return false;
                    }
                    __result = 1;
                    return false;
                case AreaType.Industrial:
                    if (__instance.m_AllowedStored != Resource.NoResource)
                    {
                        __result = 1;
                        return false;
                    }
                    if (__instance.m_AllowedManufactured == Resource.NoResource)
                    {
                        __result = 0;
                        return false;
                    }
                    __result = 1;
                    return false;
                default:
                    __result = 0;
                    return false;
            }
        }
    }
}
