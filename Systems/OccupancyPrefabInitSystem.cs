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
using UnityEngine;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class OccupancyPrefabInitSystem : GameSystemBase
    {
        EntityQuery m_Query;
        PrefabSystem m_PrefabSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp);
            m_Query = builder.WithAll<PrefabData, BuildingData, BuildingPropertyData, SpawnableBuildingData, ObjectGeometryData, SubMesh>()            
                .Build(this.EntityManager);
            builder.Reset();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();            

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

        private void GetBuildingDimensions()
        {
            List<Mesh> meshes = new List<Mesh>();
            var entities = m_Query.ToEntityArray(Allocator.Temp);
            int totalSize = 0;
            foreach (var entity in entities)
            {
                //var prefabData = SystemAPI.GetComponentRO<PrefabData>(entity);
                //var prefab = m_PrefabSystem.GetPrefab<BuildingPrefab>(prefabData.ValueRO);
                DynamicBuffer<SubMesh> subMeshes = SystemAPI.GetBuffer<SubMesh>(entity);
                foreach(var submesh in subMeshes)
                {
                    var meshData = SystemAPI.GetComponentRO<MeshData>(submesh.m_SubMesh);
                    if ((meshData.ValueRO.m_State & MeshFlags.Base) != MeshFlags.Base || (meshData.ValueRO.m_DecalLayer & Game.Rendering.DecalLayers.Buildings) != Game.Rendering.DecalLayers.Buildings )
                    {
                        // not the main building of the asset, skip
                        continue;
                    }
                }
            }           
        }

        protected override void OnUpdate()
        {
            
        }
    }
}
