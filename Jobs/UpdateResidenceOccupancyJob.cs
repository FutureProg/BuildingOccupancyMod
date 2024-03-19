using Game.Common;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Colossal.Mathematics;
using Game.Objects;

namespace Trejak.BuildingOccupancyMod.Jobs
{
    //[BurstCompile]
    public partial struct UpdateResidenceOccupancyJob : IJobChunk
    {
        public EntityTypeHandle entityHandle;
        public ComponentTypeHandle<BuildingPropertyData> buildingPropertyDataHandle;
        public ComponentTypeHandle<SpawnableBuildingData> spawnableBuildingDataHandle;
        public ComponentTypeHandle<ObjectGeometryData> objectGeometryHandle;
        public ComponentTypeHandle<BuildingData> buildingDataHandle;
        public BufferTypeHandle<SubMesh> subMeshHandle;

        public ComponentLookup<ZoneData> zoneDataLookup;
        public ComponentLookup<MeshData> meshDataLookup;

        public EntityCommandBuffer.ParallelWriter commandBuffer;

        public RandomSeed randomSeed;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // Only doing this for High Density Office and High/Medium density residential
            // because we can't find the size of the actual building without looking at the mesh.
            // Will look for mesh access later on.
            var spawnBuildingDataArr = chunk.GetNativeArray(ref spawnableBuildingDataHandle);
            var buildingDataArr = chunk.GetNativeArray(ref buildingDataHandle);
            var subMeshBufferAccessor = chunk.GetBufferAccessor(ref subMeshHandle);
            var buildingPropertyDataArr = chunk.GetNativeArray(ref buildingPropertyDataHandle);
            var objectGeometryArr = chunk.GetNativeArray(ref objectGeometryHandle);
            var entityArr = chunk.GetNativeArray(entityHandle);
            var random = randomSeed.GetRandom(1);

            int changed = 0;
            // Plugin.Log.LogInfo($"Updating {buildingDataArr.Count()} Items");                
            for (int i = 0; i < buildingDataArr.Length; i++)
            {
                SpawnableBuildingData spawnBuildingData = spawnBuildingDataArr[i];
                ObjectGeometryData geom = objectGeometryArr[i];
                BuildingPropertyData property = buildingPropertyDataArr[i];
                DynamicBuffer<SubMesh> subMeshes = subMeshBufferAccessor[i];
                Entity entity = entityArr[i];
                if (spawnBuildingData.m_ZonePrefab == Entity.Null)
                {
                    Mod.log.Info("Zone Prefab is null!");
                    continue;
                }
                if (!zoneDataLookup.TryGetComponent(spawnBuildingData.m_ZonePrefab, out var zonedata))
                {
                    Mod.log.Info("Zone Data not found!");
                    continue;
                }
                bool isResidential = zonedata.m_AreaType == Game.Zones.AreaType.Residential;
                if (!isResidential)
                {
                    continue;
                }
                
                var dimensions = GetBuildingDimensions(subMeshes);
                var size = ObjectUtils.GetSize(dimensions);
                float width = size.x;// geom.m_Size.x;
                float length = size.z;// geom.m_Size.z;
                float height = size.y;// geom.m_Size.y;                
                buildingPropertyDataArr[i] = UpdateResidential(unfilteredChunkIndex, width, length, height, random, zonedata, property, entity);
                changed += 1;                
            }
            // Plugin.Log.LogInfo($"Successfully Updated {changed} Items!");              
        }

        private Bounds3 GetBuildingDimensions(DynamicBuffer<SubMesh> subMeshes)
        {
            var totalBounds = new Bounds3(0, 0);
            foreach (var submesh in subMeshes)
            {
                var meshData = meshDataLookup[submesh.m_SubMesh];
                if ((meshData.m_State & MeshFlags.Base) != MeshFlags.Base || (meshData.m_DecalLayer & Game.Rendering.DecalLayers.Buildings) != Game.Rendering.DecalLayers.Buildings)
                {
                    // not the main building of the asset, skip
                    continue;
                }
                totalBounds |= meshData.m_Bounds;
            }            
            //float3 size = ObjectUtils.GetSize(totalBounds);
            return totalBounds;
        }

        BuildingPropertyData UpdateResidential(int unfilteredChunkIndex, float width, float length, float height, Unity.Mathematics.Random random,
                ZoneData zonedata, BuildingPropertyData property, Entity prefab)
        {
            bool is_singleFamilyResidence = property.m_ResidentialProperties == 1; // Probably safe assumption to make until we find something else                                      
            if (is_singleFamilyResidence)
            {
                // Plugin.Log.LogInfo("Skipping Single Family Residential\n=======");
                return property;
            }
            // Plugin.Log.LogInfo($"Default Residences {property.m_ResidentialProperties}");
            float RESIDENTIAL_HEIGHT = 3.5f;// 3.5 Metre Floor Height for residences (looks like the vanilla height)                                   
            float FOUNDATION_HEIGHT = 1.0f; // Looks like that'd be it? Only using this for row homes    
            float MIN_RESIDENCE_SIZE = 80;
            float MAX_RESIDENCE_SIZE = 111; // between 800sqft and 1200sqft  (80sqm and 111sqm)  
            float HALLWAY_BUFFER = 1.5f; // 1.5 metres of space in front of the unit's door
            float ELEVATOR_RATIO = 1.0f / 60f; // 1 elevator for every 60 residences
            float ELEVATOR_SPACE = 4.0f; // 4sqm for an elevator

            bool is_RowHome = zonedata.m_ZoneFlags.HasFlag(ZoneFlags.SupportNarrow);
            if (is_RowHome)
            {
                float floorCount = (height - FOUNDATION_HEIGHT) / RESIDENTIAL_HEIGHT;
                property.m_ResidentialProperties = (int)math.floor(math.floor(floorCount) * 1.5f);// For Row Homes max 1.5 residences per floor. No basement                                                                           
            }
            else
            {
                var floorSize = width * length;
                int floorUnits = 0;
                var floorCount = (int)math.floor(height / RESIDENTIAL_HEIGHT);
                floorCount -= 1; // Remove for the lobby floor
                //if (height < 52)
                //{ // Ignore mid-rise buildings, they're usually fine
                //    return property;
                //}
                // each floor has one large residence if it can fit
                if (floorSize - MAX_RESIDENCE_SIZE > MIN_RESIDENCE_SIZE + 2)
                {
                    floorSize -= MAX_RESIDENCE_SIZE;
                    floorUnits++;
                }
                float minThreshold = MIN_RESIDENCE_SIZE + math.ceil(ELEVATOR_RATIO * floorUnits * floorCount * ELEVATOR_SPACE) + HALLWAY_BUFFER;
                do
                {
                    //float maximum = floorSize < MAX_RESIDENCE_SIZE ? floorSize : MAX_RESIDENCE_SIZE;
                    //float minimum = MIN_RESIDENCE_SIZE;
                    floorSize -= MIN_RESIDENCE_SIZE;
                    floorUnits++;
                    minThreshold = MIN_RESIDENCE_SIZE + math.ceil(ELEVATOR_RATIO * floorUnits * floorCount * ELEVATOR_SPACE) + HALLWAY_BUFFER;
                } while (floorSize > minThreshold);
                property.m_ResidentialProperties = floorUnits * floorCount;
            }
            return property;
        }
    }
}
