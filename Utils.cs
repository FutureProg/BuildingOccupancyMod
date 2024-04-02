using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using static Game.Prefabs.TriggerPrefabData;

namespace Trejak.BuildingOccupancyMod
{
    public static class Utils
    {

        public static int GetAskingRent(Entity roadEdge, Entity prefab, BuildingData buildingData, BuildingPropertyData propertyData, ComponentLookup<LandValue> landValueLookup, ComponentLookup<ConsumptionData> consumptionDataLookup, Game.Zones.AreaType areaType)
        {
            float lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
            float landValue = 0;
            if (landValueLookup.HasComponent(roadEdge))
            {
                landValue = lotSize * landValueLookup[roadEdge].m_LandValue;
            }
            var consumptionData = consumptionDataLookup[prefab];
            var askingRent = RentAdjustSystem.GetRent(consumptionData, propertyData, landValue, Game.Zones.AreaType.Residential).x;
            return askingRent;
        }

    }
}
