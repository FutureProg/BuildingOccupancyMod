using Game;
using Game.Agents;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trejak.BuildingOccupancyMod;
using Trejak.BuildingOccupancyMod.Components;
using Unity.Entities;
using UnityEngine;

namespace BuildingOccupancyMod.Systems
{
    public partial class ModifiedCompanyFindPropertySystem : GameSystemBase
    {

        EntityArchetype m_RentEventArchetype;
        EntityArchetype m_MovedEventArchetype;
        EntityQuery m_CommerceQuery;
        EntityQuery m_FreePropertyQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_RentEventArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<Event>(), ComponentType.ReadWrite<RentersUpdated>());
            m_MovedEventArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<Event>(), ComponentType.ReadWrite<PathTargetMoved>());

            m_CommerceQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<ServiceAvailable>(),
                ComponentType.ReadWrite<ResourceSeller>(),
                ComponentType.ReadWrite<CompanyData>(),
                ComponentType.ReadWrite<PropertySeeker>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Created>()
            );

            m_FreePropertyQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<ExtraCommercialProperty>(),                
                ComponentType.ReadOnly<CommercialProperty>(),                
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<PrefabRef>(),                
                ComponentType.Exclude<Abandoned>(),
                ComponentType.Exclude<Condemned>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<PropertyOnMarket>()
            );
        }

        protected override void OnUpdate()
        {
            
        }

        public partial struct AddPropertyToMarketJob : IJobEntity
        {

            public EntityCommandBuffer.ParallelWriter ecb;
            public ComponentLookup<LandValue> landValueLookup;
            public ComponentLookup<ConsumptionData> consumptionDataLookup;
            public ComponentLookup<BuildingPropertyData> buildingPropertyLookup;
            public ComponentLookup<BuildingData> buildingDataLookup;
            public ComponentLookup<CompanyData> companyDataLookup;
            public BufferLookup<Renter> renterLookup;

            public void Execute(Entity entity, Building building, PrefabRef prefabRef, ref ExtraCommercialProperty extraCommercial, [ChunkIndexInQuery] int chunkIndex)
            {
                bool hasRenters = renterLookup.TryGetBuffer(entity, out var renters);
                int tenantCount = 0;
                if (hasRenters)
                {
                    for(int i = 0; i < renters.Length; i++)
                    {
                        if (companyDataLookup.HasComponent(renters[i]))
                        {
                            tenantCount++;                            
                        }
                    }
                }
                if (tenantCount < extraCommercial.extraCount + 1)
                {
                    Entity roadEdge = building.m_RoadEdge;
                    BuildingData buildingData = buildingDataLookup[prefabRef.m_Prefab];
                    BuildingPropertyData propertyData = buildingPropertyLookup[prefabRef.m_Prefab];                    
                    var askingRent = Utils.GetAskingRent(roadEdge, prefabRef.m_Prefab, buildingData, propertyData, landValueLookup, consumptionDataLookup, Game.Zones.AreaType.Commercial);
                    askingRent = Mathf.RoundToInt(askingRent / (extraCommercial.extraCount + 1));
                    ecb.AddComponent(chunkIndex, entity, new PropertyOnMarket { m_AskingRent = askingRent });
                }
            }
        }

    }
}
