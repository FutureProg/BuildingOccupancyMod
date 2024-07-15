using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;

namespace Trejak.BuildingOccupancyMod.Systems
{

    public partial class MultiCompanyPropertyOnMarketSystem : GameSystemBase
    {

        private EntityQuery m_Query;
        private EndFrameBarrier m_EndFrameBarrier;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 262144 / (PropertyRenterSystem.kUpdatesPerDay * 32);
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Query = EntityManager.CreateEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[]
                {
                ComponentType.ReadOnly<Renter>(),
                ComponentType.ReadOnly<Building>()
                },
                Any = new ComponentType[]
                {
                ComponentType.ReadOnly<CommercialProperty>(),
                ComponentType.ReadOnly<OfficeProperty>()
                },
                None = new ComponentType[]
                {
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Deleted>(),
                ComponentType.ReadOnly<PropertyOnMarket>(),
                ComponentType.ReadOnly<PropertyToBeOnMarket>()
                }
            });

            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();

            this.RequireForUpdate(m_Query);
        }

        protected override void OnUpdate()
        {

            AddBuildingsToMarketJob job = new()
            {
                buildingPropertyDataLookup = SystemAPI.GetComponentLookup<BuildingPropertyData>(),
                entityHandle = SystemAPI.GetEntityTypeHandle(),
                prefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(),
                rentersHandle = SystemAPI.GetBufferTypeHandle<Renter>(),
                ecb = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
            };
            this.Dependency = job.ScheduleParallel(m_Query, this.Dependency);
            m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
        }

        public partial struct AddBuildingsToMarketJob : IJobChunk
        {
            public BufferTypeHandle<Renter> rentersHandle;
            public EntityTypeHandle entityHandle;

            public ComponentLookup<PrefabRef> prefabRefLookup;
            public ComponentLookup<BuildingPropertyData> buildingPropertyDataLookup;

            public EntityCommandBuffer.ParallelWriter ecb;


            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityHandle);
                var rentersAccessor = chunk.GetBufferAccessor(ref rentersHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    var renters = rentersAccessor[i];
                    var prefab = prefabRefLookup[entity];
                    var propertyData = buildingPropertyDataLookup[prefab.m_Prefab];
                    int propertyCount = propertyData.CountProperties();
                    if (renters.Length < propertyCount)
                    {
                        ecb.AddComponent<PropertyToBeOnMarket>(unfilteredChunkIndex, entity);
                    }
                }
            }
        }

    }
}