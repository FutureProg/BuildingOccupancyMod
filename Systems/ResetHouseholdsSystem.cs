using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trejak.BuildingOccupancyMod.Components;
using Unity.Burst.Intrinsics;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class ResetHouseholdsSystem : GameSystemBase
    {

        public EntityQuery m_TriggerQuery;
        public EntityQuery m_HouseholdsQuery;
        public EntityQuery m_ResidencesQuery;

        public static bool reset { get; private set; }

        public static void TriggerReset()
        {            
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            em.CreateSingleton<ResetHouseholdsTrigger>();
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
            m_ResidencesQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<ResidentialProperty>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Renter>(),                
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()             
            );
            this.m_TriggerQuery = GetEntityQuery(ComponentType.ReadWrite<ResetHouseholdsTrigger>());

            this.RequireForUpdate(m_TriggerQuery);
            this.RequireForUpdate(m_HouseholdsQuery);
            this.RequireForUpdate(m_ResidencesQuery);
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = World.GetExistingSystemManaged<EndFrameBarrier>().CreateCommandBuffer();



            ecb.DestroyEntity(m_TriggerQuery.GetSingletonEntity());
        }

        public partial struct ResetResidencesJob : IJobChunk
        {

            public EntityCommandBuffer ecb;

            public EntityTypeHandle entityTypeHandle;
            public BufferTypeHandle<Renter> renterTypeHandle;
            public ComponentTypeHandle<PrefabRef> prefabRefTypeHandle;

            public ComponentLookup<BuildingPropertyData> propertyDataLookup;
            public ComponentLookup<CommercialProperty> commercialPropertyLookup;

            public RandomSeed randomSeed;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityTypeHandle);
                var renters = chunk.GetBufferAccessor(ref renterTypeHandle);
                var prefabRefs = chunk.GetNativeArray(ref prefabRefTypeHandle);

                var random = randomSeed.GetRandom(1);


            }
        }

    }
}
