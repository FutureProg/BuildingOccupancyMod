using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trejak.BuildingOccupancyMod.Jobs;
using Unity.Collections;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class OccupancyPrefabInitSystem : GameSystemBase
    {
        EntityQuery m_Query;

        protected override void OnCreate()
        {
            base.OnCreate();

            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp);
            m_Query = builder.WithAll<PrefabData, BuildingData, BuildingPropertyData, SpawnableBuildingData, ObjectGeometryData>()            
                .Build(this.EntityManager);
            builder.Reset();

            World.GetOrCreateSystemManaged<ZoneSpawnSystem>().debugFastSpawn = true; // REMOVE FOR RELEASE
            RequireForUpdate(m_Query);
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            if (mode != GameMode.Game)
            {
                return;
            }
            var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var residentialJob = new UpdateResidenceOccupancyJob
            {
                entityHandle = SystemAPI.GetEntityTypeHandle(),
                commandBuffer = commandBuffer.AsParallelWriter(),
                spawnableBuildingDataHandle = SystemAPI.GetComponentTypeHandle<SpawnableBuildingData>(true),
                buildingPropertyDataHandle = SystemAPI.GetComponentTypeHandle<BuildingPropertyData>(false),
                zoneDataLookup = SystemAPI.GetComponentLookup<ZoneData>(true),
                objectGeometryHandle = SystemAPI.GetComponentTypeHandle<ObjectGeometryData>(true),
                buildingDataHandle = SystemAPI.GetComponentTypeHandle<BuildingData>(true),
                randomSeed = RandomSeed.Next()
            };
            residentialJob.ScheduleParallel(m_Query, this.Dependency).Complete();
        }

        protected override void OnUpdate()
        {
            
        }
    }
}
