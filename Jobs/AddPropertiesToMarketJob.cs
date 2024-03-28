using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trejak.BuildingOccupancyMod.Components;
using Unity.Burst.Intrinsics;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Jobs
{
    public partial struct AddPropertiesToMarketJob : IJobChunk
    {
        public EntityCommandBuffer ecb;

        public EntityTypeHandle entityTypeHandle;        
        public BufferTypeHandle<Renter> renterTypeHandle;
        public ComponentTypeHandle<PrefabRef> prefabRefTypeHandle;
        public ComponentTypeHandle<Building> buildingTypeHandle;

        public ComponentLookup<BuildingData> buildingDataLookup;
        public ComponentLookup<BuildingPropertyData> propertyDataLookup;
        public ComponentLookup<CommercialProperty> commercialPropertyLookup;
        public ComponentLookup<PropertyToBeOnMarket> propertyToBeOnMarketLookup;
        public ComponentLookup<PropertyOnMarket> propertyOnMarketLookup;
        public ComponentLookup<ConsumptionData> consumptionDataLookup;
        public ComponentLookup<LandValue> landValueLookup;


        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(entityTypeHandle);
            var renterAccessor = chunk.GetBufferAccessor(ref renterTypeHandle);
            var prefabRefs = chunk.GetNativeArray(ref prefabRefTypeHandle);
            var buildings = chunk.GetNativeArray(ref buildingTypeHandle);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var prefabRef = prefabRefs[i];
                var renters = renterAccessor[i];
                var building = buildings[i];
                if (!propertyDataLookup.TryGetComponent(prefabRef.m_Prefab, out var propertyData))
                {
                    return;
                }
                int householdsCount;
                bool isCommercialOffice = commercialPropertyLookup.HasComponent(entity);   // TODO: Check for office as well                 
                if (isCommercialOffice)
                {
                    // TODO: change to counting the number of commercial properties instead of -1 after implementing multi-tenant commercial/office
                    householdsCount = renters.Length - 1;
                }
                else
                {
                    householdsCount = renters.Length;
                }

                if (householdsCount < propertyData.m_ResidentialProperties && !propertyOnMarketLookup.HasComponent(entity))
                {
                    Entity roadEdge = building.m_RoadEdge;
                    BuildingData buildingData = buildingDataLookup[prefabRef.m_Prefab];
                    float lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
                    float landValue = 0;
                    if (landValueLookup.HasComponent(roadEdge))
                    {
                        landValue = lotSize * landValueLookup[roadEdge].m_LandValue;
                    }
                    var consumptionData = consumptionDataLookup[prefabRef.m_Prefab];
                    var askingRent = RentAdjustSystem.GetRent(consumptionData, propertyData, landValue, Game.Zones.AreaType.Residential).x;
                    ecb.AddComponent(entity, new PropertyOnMarket { m_AskingRent = askingRent });
                }
                else if (householdsCount == propertyData.m_ResidentialProperties && propertyToBeOnMarketLookup.HasComponent(entity))
                {
                    ecb.RemoveComponent<PropertyToBeOnMarket>(entity);
                }
            }
        }
    }
}
