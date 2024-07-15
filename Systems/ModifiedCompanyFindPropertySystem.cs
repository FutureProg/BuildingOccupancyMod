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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trejak.BuildingOccupancyMod;
using Trejak.BuildingOccupancyMod.Components;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class ModifiedCompanyFindPropertySystem : GameSystemBase
    {

        EntityArchetype m_RentEventArchetype;
        EntityArchetype m_MovedEventArchetype;
        EntityQuery m_PropertyRenterQuery;
        EntityQuery m_FreePropertyQuery;

        PropertyRenterRemoveSystem m_RenterRemoveSystem;
        MultiCommercialFindPropertySystem m_CommercialFindPropertySystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_RentEventArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<Event>(), ComponentType.ReadWrite<RentersUpdated>());
            m_MovedEventArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<Event>(), ComponentType.ReadWrite<PathTargetMoved>());

            m_RenterRemoveSystem = World.GetOrCreateSystemManaged<PropertyRenterRemoveSystem>();
            m_CommercialFindPropertySystem = World.GetOrCreateSystemManaged<MultiCommercialFindPropertySystem>();

            m_PropertyRenterQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<ServiceAvailable>(),
                ComponentType.ReadWrite<PropertyRenter>(),
                ComponentType.ReadWrite<ResourceSeller>(),
                ComponentType.ReadWrite<CompanyData>(),                
                ComponentType.ReadOnly<PrefabRef>(),
                //ComponentType.Exclude<PropertySeeker>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Created>()
            );

            m_FreePropertyQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<ExtraCommercialProperty>(),                
                ComponentType.ReadOnly<CommercialProperty>(),                
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<PropertyOnMarket>(),
                ComponentType.ReadOnly<PrefabRef>(),                
                ComponentType.Exclude<Abandoned>(),
                ComponentType.Exclude<Condemned>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );

            this.RequireForUpdate(m_PropertyRenterQuery);
        }

        protected override void OnUpdate()
        {
            FixPropertyRentersListJob job = new FixPropertyRentersListJob()
            {
                renterLookup = SystemAPI.GetBufferLookup<Renter>(false),
                companyLookup = SystemAPI.GetComponentLookup<CompanyData>(true),
                entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                prefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                propertyDataLookup = SystemAPI.GetComponentLookup<BuildingPropertyData>(true),
                propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                propertySeekerLookup = SystemAPI.GetComponentLookup<PropertySeeker>(false),
                ecb = World.GetOrCreateSystemManaged<EndFrameBarrier>().CreateCommandBuffer()
            };
            var dependencyHandle = JobHandle.CombineDependencies(
                //m_RenterRemoveSystem.CheckedStateRef.Dependency, 
                m_CommercialFindPropertySystem.CheckedStateRef.Dependency, 
                this.Dependency
            );                        
            this.Dependency = job.Schedule(m_PropertyRenterQuery, this.Dependency);
        }


        /// <summary>
        /// Check to make sure the property is actually full if seeking.
        /// If a company is not marked as a renter (rent job removed it), put it back
        /// </summary>
        public partial struct FixPropertyRentersListJob : IJobChunk
        {
            public EntityTypeHandle entityTypeHandle;

            public ComponentLookup<PropertySeeker> propertySeekerLookup; // if it has a property seeker and is a property renter, check to make sure that the property is actually full
            public ComponentLookup<PropertyRenter> propertyRenterLookup;
            public BufferLookup<Renter> renterLookup;
            public ComponentLookup<CompanyData> companyLookup;
            public ComponentLookup<PrefabRef> prefabRefLookup;
            public ComponentLookup<BuildingPropertyData> propertyDataLookup;

            public EntityCommandBuffer ecb;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityTypeHandle);
                foreach (Entity companyEntity in entities)
                {
                    if (propertyRenterLookup.TryGetComponent(companyEntity, out var propertyRenter))
                    {
                        if (propertyRenter.m_Property != Entity.Null && renterLookup.TryGetBuffer(propertyRenter.m_Property, out var renterBuffer))
                        {
                            var buildingEntity = propertyRenter.m_Property;
                            bool presentInRentersList = false;
                            int companyCount = 0;
                            for(int i = 0; i < renterBuffer.Length; i++)
                            {
                                if (renterBuffer[i].m_Renter == companyEntity)
                                {
                                    presentInRentersList = true;
                                    break;
                                }
                                if (companyLookup.HasComponent(renterBuffer[i].m_Renter))
                                {
                                    companyCount++;
                                }
                            }
                            if (presentInRentersList) continue;

                            var prefab = prefabRefLookup[buildingEntity];
                            var buildingProperty = propertyDataLookup[prefab.m_Prefab];
                            if (companyCount < buildingProperty.CountProperties(Game.Zones.AreaType.Commercial))
                            {
                                renterBuffer.Add(new Renter() { m_Renter = companyEntity });
                                //ecb.RemoveComponent<PropertySeeker>(companyEntity);
                            }
                        }
                    }
                }
            }
        }

        public partial struct UpdatePropertyOnMarketJob : IJobEntity
        {

            public EntityCommandBuffer.ParallelWriter ecb;
            public ComponentLookup<LandValue> landValueLookup;
            public ComponentLookup<ConsumptionData> consumptionDataLookup;
            public ComponentLookup<BuildingPropertyData> buildingPropertyLookup;
            public ComponentLookup<BuildingData> buildingDataLookup;
            public ComponentLookup<CompanyData> companyDataLookup;
            public ComponentLookup<PropertyOnMarket> propertyOnMarketLookup;            
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
