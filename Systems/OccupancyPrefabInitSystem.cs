using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.UI.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Trejak.BuildingOccupancyMod.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class OccupancyPrefabInitSystem : GameSystemBase
    {
        EntityQuery m_Query;
        PrefabSystem m_PrefabSystem;
        public bool initialized;
        NotificationUISystem m_NotificationUISystem;

        NotificationUISystem.NotificationInfo activeNotification;

        protected override void OnCreate()
        {
            base.OnCreate();

            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp);
            m_Query = builder.WithAll<PrefabData, BuildingData, BuildingPropertyData, SpawnableBuildingData, ObjectGeometryData, SubMesh>()            
                .Build(this.EntityManager);
            builder.Reset();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_NotificationUISystem = World.GetOrCreateSystemManaged<NotificationUISystem>();
            initialized = false;
            RequireForUpdate(m_Query);
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            this.initialized = false;
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
                randomSeed = RandomSeed.Next(),
                meshDataLookup = SystemAPI.GetComponentLookup<MeshData>(true),
                subMeshHandle = SystemAPI.GetBufferTypeHandle<SubMesh>(true)                
            };
            residentialJob.ScheduleParallel(m_Query, this.Dependency).Complete();
            initialized = true;
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {            
            base.OnGameLoadingComplete(purpose, mode);
            if (Setting.showLoadNotification && (purpose == Purpose.LoadGame || purpose == Purpose.NewGame) && mode.IsGame())
            {
                activeNotification = m_NotificationUISystem.AddOrUpdateNotification(
                    $"{nameof(BuildingOccupancyMod)}.{nameof(Mod)}.Loaded",
                    title: "Realistic Building Occupany Loaded",
                    text: "Click to close",
                    onClicked: this.CloseNotification,
                    progressState: Colossal.PSI.Common.ProgressState.Complete
                );                
            }
        }
        
        private void CloseNotification()
        {            
            m_NotificationUISystem.RemoveNotification(activeNotification.id);
            activeNotification = null;
        }

        protected override void OnUpdate()
        {
            
        }
    }
}
