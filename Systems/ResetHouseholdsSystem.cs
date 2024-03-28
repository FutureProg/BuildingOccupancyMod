using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Trejak.BuildingOccupancyMod.Components;
using Trejak.BuildingOccupancyMod.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class ResetHouseholdsSystem : GameSystemBase
    {


        EntityQuery m_TriggerQuery;
        SimulationSystem m_SimulationSystem;
        private EndFrameBarrier m_EndFrameBarrier;
        EntityQuery m_HouseholdsQuery;
        EntityQuery m_BuildingsQuery;

        public static bool reset { get; private set; }

        public static void TriggerReset(ResetType resetType)
        {            
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var trigger = new ResetHouseholdsTrigger()
            {
                resetType = resetType
            };            
            em.CreateSingleton(trigger);
        }        

        protected override void OnCreate()
        {
            base.OnCreate();
            reset = false;
            m_HouseholdsQuery = GetEntityQuery(            
                ComponentType.ReadWrite<Household>(),                
                ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<TouristHousehold>(),
                ComponentType.Exclude<CommuterHousehold>(),
                ComponentType.Exclude<CurrentBuilding>(),
                ComponentType.Exclude<PropertySeeker>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );
            m_BuildingsQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<ResidentialProperty>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Renter>(),                
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );
            this.m_TriggerQuery = GetEntityQuery(ComponentType.ReadWrite<ResetHouseholdsTrigger>());

            this.m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_EndFrameBarrier = World.GetExistingSystemManaged<EndFrameBarrier>(); 

            this.RequireForUpdate(m_TriggerQuery);
            this.RequireForUpdate(m_HouseholdsQuery);
            this.RequireForUpdate(m_BuildingsQuery);
        }

        protected override void OnUpdate()
        {
            var trigger = SystemAPI.GetSingleton<ResetHouseholdsTrigger>();

            Mod.log.Info("Scheduling household reset of type " + trigger.ToString());

            //AddPropertiesToMarketJob job = new AddPropertiesToMarketJob()
            //{
            //    ecb = m_EndFrameBarrier.CreateCommandBuffer(),
            //    commercialPropertyLookup = SystemAPI.GetComponentLookup<CommercialProperty>(true),
            //    entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
            //    prefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
            //    buildingTypeHandle = SystemAPI.GetComponentTypeHandle<Building>(true),
            //    propertyDataLookup = SystemAPI.GetComponentLookup<BuildingPropertyData>(true),
            //    renterTypeHandle = SystemAPI.GetBufferTypeHandle<Renter>(false),
            //    propertyToBeOnMarketLookup = SystemAPI.GetComponentLookup<PropertyToBeOnMarket>(false),
            //    propertyOnMarketLookup = SystemAPI.GetComponentLookup<PropertyOnMarket>(true),
            //    consumptionDataLookup = SystemAPI.GetComponentLookup<ConsumptionData>(true),
            //    landValueLookup = SystemAPI.GetComponentLookup<LandValue>(true),
            //    buildingDataLookup = SystemAPI.GetComponentLookup<BuildingData>(true)
            //};
            //JobHandle addToMarketHandle = job.ScheduleParallel(m_BuildingsQuery, this.Dependency);
            var temp = m_HouseholdsQuery.ToEntityArray(Allocator.Temp);
            int householdsCount = temp.Length;
            temp.Dispose();
            NativeList<Entity> evictedHouseholds = new NativeList<Entity>(householdsCount/2, Allocator.TempJob);

            var resetResidencesJob = new ResetResidencesJob()
            {
                ecb = m_EndFrameBarrier.CreateCommandBuffer(),
                commercialPropertyLookup = SystemAPI.GetComponentLookup<CommercialProperty>(true),
                entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                prefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                buildingTypeHandle = SystemAPI.GetComponentTypeHandle<Building>(true),
                propertyDataLookup = SystemAPI.GetComponentLookup<BuildingPropertyData>(true),
                randomSeed = RandomSeed.Next(),
                renterTypeHandle = SystemAPI.GetBufferTypeHandle<Renter>(false),
                workProviderLookup = SystemAPI.GetComponentLookup<WorkProvider>(true),
                resetType = trigger.resetType,
                propertyToBeOnMarketLookup = SystemAPI.GetComponentLookup<PropertyToBeOnMarket>(false),
                propertyOnMarketLookup = SystemAPI.GetComponentLookup<PropertyOnMarket>(true),
                consumptionDataLookup = SystemAPI.GetComponentLookup<ConsumptionData>(true),
                landValueLookup = SystemAPI.GetComponentLookup<LandValue>(true),
                buildingDataLookup = SystemAPI.GetComponentLookup<BuildingData>(true),
                evictedList = evictedHouseholds
            };            
            EntityManager.DestroyEntity(m_TriggerQuery.GetSingletonEntity());
            this.Dependency = resetResidencesJob.Schedule(m_BuildingsQuery, this.Dependency);
            this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);                 
        }

        [BurstCompile]
        public partial struct ResetResidencesJob : IJobChunk
        {

            public EntityCommandBuffer ecb;

            public EntityTypeHandle entityTypeHandle;
            public BufferTypeHandle<Renter> renterTypeHandle;
            public ComponentTypeHandle<PrefabRef> prefabRefTypeHandle;
            public ComponentTypeHandle<Building> buildingTypeHandle;

            public ComponentLookup<BuildingData> buildingDataLookup;
            public ComponentLookup<BuildingPropertyData> propertyDataLookup;
            public ComponentLookup<CommercialProperty> commercialPropertyLookup;
            public ComponentLookup<WorkProvider> workProviderLookup;
            public ComponentLookup<PropertyToBeOnMarket> propertyToBeOnMarketLookup;
            public ComponentLookup<PropertyOnMarket> propertyOnMarketLookup;
            public ComponentLookup<ConsumptionData> consumptionDataLookup;
            public ComponentLookup<LandValue> landValueLookup;

            public RandomSeed randomSeed;
            public ResetType resetType;
            private EntityArchetype m_RentEventArchetype;
            public NativeList<Entity> evictedList;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityTypeHandle);
                var renterAccessor = chunk.GetBufferAccessor(ref renterTypeHandle);
                var prefabRefs = chunk.GetNativeArray(ref prefabRefTypeHandle);
                var buildings = chunk.GetNativeArray(ref buildingTypeHandle);

                var random = randomSeed.GetRandom(1);

                for(int i = 0; i < entities.Length; i++)
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
                    } else
                    {
                        householdsCount = renters.Length;
                    }

                    // Too many households
                    if (householdsCount > propertyData.m_ResidentialProperties)
                    {
                        RemoveHouseholds(householdsCount - propertyData.m_ResidentialProperties, entity, renters, ref random);
                        if (resetType == ResetType.FindNewHome)
                        {
                            Entity e = ecb.CreateEntity(this.m_RentEventArchetype);
                            ecb.SetComponent(e, new RentersUpdated(entity));
                        }                        
                    } 
                    else if (householdsCount < propertyData.m_ResidentialProperties && propertyOnMarketLookup.TryGetComponent(entity, out var onMarketInfo))
                    {
                        if (evictedList.Length > 0)
                        {
                            var delta = propertyData.m_ResidentialProperties - householdsCount;
                            while (delta > 0 && evictedList.Length > 0)
                            {
                                var tenant = evictedList[0];
                                //renters.Add(new Renter() { m_Renter = tenant });
                                //ecb.RemoveComponent<PropertySeeker>(tenant);
                                // TODO: Add to list for the RentJob
                                evictedList.RemoveAt(0);
                                delta--;
                            }
                            Entity e = ecb.CreateEntity(this.m_RentEventArchetype);
                            ecb.SetComponent(e, new RentersUpdated(entity));
                        }
                    }
                }                
            }

            private void RemoveHouseholds(int extraHouseholds, Entity property, DynamicBuffer<Renter> renters, ref Unity.Mathematics.Random random)
            {
                //NativeHashSet<Entity> marked = new NativeHashSet<Entity>(extraHouseholds, Allocator.Temp);
                for(int i = 0; i < extraHouseholds && extraHouseholds > 0; i++)
                {
                    // was while(extraHouseholds > 0) but that might take too long if the set already contains it
                    //var entity = renters[random.NextInt(0,renters.Length)].m_Renter; // remove a random household so the newer ones aren't always removed
                    var entity = renters[i].m_Renter;
                    if (workProviderLookup.HasComponent(entity)) continue;
                    switch (resetType)
                    {
                        case ResetType.Delete:
                            ecb.AddComponent<Deleted>(entity);                            
                            break;
                        case ResetType.FindNewHome:
                            ecb.AddComponent(entity, new PropertySeeker());
                            ecb.RemoveComponent<PropertyRenter>(entity);
                            evictedList.Add(entity);
                            ecb.AddComponent(entity, new Evicted() { from = property });
                            break;
                        default:
                            throw new System.Exception($"Invalid ResetType provided: \"{resetType}\"!");
                    }                                    
                }
            }
        }

    }
}
