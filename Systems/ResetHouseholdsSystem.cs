using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Trejak.BuildingOccupancyMod.Components;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

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

            var resetResidencesJob = new ResetResidencesJob()
            {
                ecb = m_EndFrameBarrier.CreateCommandBuffer(),
                commercialPropertyLookup = SystemAPI.GetComponentLookup<CommercialProperty>(true),
                entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                prefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                propertyDataLookup = SystemAPI.GetComponentLookup<BuildingPropertyData>(true),
                randomSeed = RandomSeed.Next(),
                renterTypeHandle = SystemAPI.GetBufferTypeHandle<Renter>(false),
                workProviderLookup = SystemAPI.GetComponentLookup<WorkProvider>(true),
                resetType = trigger.resetType
            };            
            EntityManager.DestroyEntity(m_TriggerQuery.GetSingletonEntity());
            this.Dependency = resetResidencesJob.Schedule(m_BuildingsQuery, this.Dependency);
            this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);               
        }

        public partial struct ResetResidencesJob : IJobChunk
        {

            public EntityCommandBuffer ecb;

            public EntityTypeHandle entityTypeHandle;
            public BufferTypeHandle<Renter> renterTypeHandle;
            public ComponentTypeHandle<PrefabRef> prefabRefTypeHandle;

            public ComponentLookup<BuildingPropertyData> propertyDataLookup;
            public ComponentLookup<CommercialProperty> commercialPropertyLookup;
            public ComponentLookup<WorkProvider> workProviderLookup;

            public RandomSeed randomSeed;
            public ResetType resetType;
            private EntityArchetype m_RentEventArchetype;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityTypeHandle);
                var renterAccessor = chunk.GetBufferAccessor(ref renterTypeHandle);
                var prefabRefs = chunk.GetNativeArray(ref prefabRefTypeHandle);                

                var random = randomSeed.GetRandom(1);

                for(int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var prefabRef = prefabRefs[i];
                    var renters = renterAccessor[i];
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
                            ecb.AddComponent<PropertySeeker>(entity);
                            ecb.RemoveComponent<PropertyRenter>(entity);
                            break;
                        default:
                            throw new System.Exception($"Invalid ResetType provided: \"{resetType}\"!");
                    }
                    //extraHouseholds -= 1;                                        
                }
            }
        }

    }
}
