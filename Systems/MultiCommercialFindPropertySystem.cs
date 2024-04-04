using Game;
using Game.Simulation;
using System.Runtime.CompilerServices;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Tools;
using Game.Triggers;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Trejak.BuildingOccupancyMod.Jobs;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class MultiCommercialFindPropertySystem : GameSystemBase
    {

        public NativeQueue<TempRenterStorage> renterStorageQueue;
        private EntityQuery m_OnMarketCommercialWithRentersQuery;

        private EntityQuery m_CommerceQuery;
        private EntityQuery m_FreePropertyQuery;
        private EntityQuery m_EconomyParameterQuery;
        private EntityQuery m_ZonePreferenceQuery;

        private SimulationSystem m_SimulationSystem;
        private EndFrameBarrier m_EndFrameBarrier;
        private ResourceSystem m_ResourceSystem;
        private TriggerSystem m_TriggerSystem;
        private CityStatisticsSystem m_CityStatisticsSystem;


        private NativeQueue<PropertyUtils.RentAction> m_RentQueue;

        private NativeList<Entity> m_ReservedProperties;

        private EntityArchetype m_RentEventArchetype;
        private EntityArchetype m_MovedEventArchetype;

        protected override void OnCreate()
        {
            base.OnCreate();
            World.GetOrCreateSystemManaged<CommercialFindPropertySystem>().Enabled = false;

            this.m_RentQueue = new NativeQueue<PropertyUtils.RentAction>(Allocator.Persistent);
            this.m_ReservedProperties = new NativeList<Entity>(Allocator.Persistent);
            this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
            this.m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
            this.m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
            this.m_CityStatisticsSystem = base.World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            this.m_RentEventArchetype = base.EntityManager.CreateArchetype(new ComponentType[]
            {
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<RentersUpdated>()
            });
            this.m_MovedEventArchetype = base.EntityManager.CreateArchetype(new ComponentType[]
            {
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<PathTargetMoved>()
            });
            this.m_CommerceQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadWrite<ServiceAvailable>(),
                ComponentType.ReadWrite<ResourceSeller>(),
                ComponentType.ReadWrite<CompanyData>(),
                ComponentType.ReadWrite<PropertySeeker>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Created>()
            });
            this.m_FreePropertyQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadWrite<PropertyOnMarket>(),
                ComponentType.ReadWrite<CommercialProperty>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Abandoned>(),
                ComponentType.Exclude<Condemned>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            });
            this.m_EconomyParameterQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<EconomyParameterData>()
            });
            this.m_ZonePreferenceQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<ZonePreferenceData>()
            });

            this.renterStorageQueue = new NativeQueue<TempRenterStorage>(Allocator.Persistent);
            this.m_OnMarketCommercialWithRentersQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadWrite<PropertyOnMarket>(),
                ComponentType.ReadWrite<CommercialProperty>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadWrite<Renter>(),
                ComponentType.Exclude<Abandoned>(),
                ComponentType.Exclude<Condemned>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            });

            base.RequireForUpdate(this.m_CommerceQuery);
            base.RequireForUpdate(this.m_EconomyParameterQuery);
        }

        protected override void OnUpdate()
        {
            if (this.m_CommerceQuery.CalculateEntityCount() > 0)
            {
                var tempClearRentersJob = new TemporaryClearRentersJob()
                {
                    buildingPropertyDataLookup = SystemAPI.GetComponentLookup<BuildingPropertyData>(),
                    commercialCompanyLookup = SystemAPI.GetComponentLookup<CommercialCompany>(),
                    entityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    prefabHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                    renterHandle = SystemAPI.GetBufferTypeHandle<Renter>(),
                    renterStorageList = renterStorageQueue
                };
                var clearJobHandle = tempClearRentersJob.Schedule(m_OnMarketCommercialWithRentersQuery, this.Dependency);

                JobHandle job;
                JobHandle job2;
                PropertyUtils.CompanyFindPropertyJob jobData = new PropertyUtils.CompanyFindPropertyJob
                {
                    m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle,
                    m_PrefabType = SystemAPI.GetComponentTypeHandle<Game.Prefabs.PrefabRef>(false),
                    m_PropertySeekerType = SystemAPI.GetComponentTypeHandle<Game.Agents.PropertySeeker>(false),
                    m_CompanyDataType = SystemAPI.GetComponentTypeHandle<Game.Companies.CompanyData>(false),
                    m_StorageCompanyType = SystemAPI.GetComponentTypeHandle<Game.Companies.StorageCompany>(true),
                    m_FreePropertyEntities = m_FreePropertyQuery.ToEntityListAsync(base.World.UpdateAllocator.ToAllocator, out job),
                    m_PropertyPrefabs = this.m_FreePropertyQuery.ToComponentDataListAsync<PrefabRef>(base.World.UpdateAllocator.ToAllocator, out job2),
                    m_BuildingPropertyDatas = SystemAPI.GetComponentLookup<Game.Prefabs.BuildingPropertyData>(true),
                    m_IndustrialProcessDatas = SystemAPI.GetComponentLookup<Game.Prefabs.IndustrialProcessData>(true),
                    m_PrefabFromEntity = SystemAPI.GetComponentLookup<Game.Prefabs.PrefabRef>(true),
                    m_PropertiesOnMarket = SystemAPI.GetComponentLookup<Game.Buildings.PropertyOnMarket>(true),
                    m_Availabilities = SystemAPI.GetBufferLookup<Game.Net.ResourceAvailability>(true),
                    m_BuildingDatas = SystemAPI.GetComponentLookup<Game.Prefabs.BuildingData>(true),
                    m_Buildings = SystemAPI.GetComponentLookup<Game.Buildings.Building>(true),
                    m_PropertyRenters = SystemAPI.GetComponentLookup<Game.Buildings.PropertyRenter>(false),
                    m_ResourceDatas = SystemAPI.GetComponentLookup<Game.Prefabs.ResourceData>(true),
                    m_LandValues = SystemAPI.GetComponentLookup<Game.Net.LandValue>(true),
                    m_ServiceCompanies = SystemAPI.GetComponentLookup<Game.Companies.ServiceCompanyData>(true),
                    m_SpawnableDatas = SystemAPI.GetComponentLookup<Game.Prefabs.SpawnableBuildingData>(true),
                    m_WorkplaceDatas = SystemAPI.GetComponentLookup<Game.Prefabs.WorkplaceData>(true),
                    m_CommercialCompanies = SystemAPI.GetComponentLookup<Game.Companies.CommercialCompany>(true),
                    m_Renters = SystemAPI.GetBufferLookup<Game.Buildings.Renter>(true),
                    m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
                    m_ZonePreferences = this.m_ZonePreferenceQuery.GetSingleton<ZonePreferenceData>(),
                    m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs(),
                    m_Commercial = true,
                    m_RentQueue = this.m_RentQueue.AsParallelWriter(),
                    m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
                };
                this.Dependency = jobData.ScheduleParallel(this.m_CommerceQuery, JobHandle.CombineDependencies(job, job2, clearJobHandle));
                this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
                this.m_ResourceSystem.AddPrefabsReader(base.Dependency);

                var restoreRentersJob = new RestoreRentersJob()
                {
                    renterLookup = SystemAPI.GetBufferLookup<Renter>(),
                    renterStorageList = renterStorageQueue
                };
                this.Dependency = restoreRentersJob.Schedule(this.Dependency);

                JobHandle job3;
                PropertyUtils.RentJob jobData2 = new PropertyUtils.RentJob
                {
                    m_RentEventArchetype = this.m_RentEventArchetype,
                    m_MovedEventArchetype = this.m_MovedEventArchetype,
                    m_PropertiesOnMarket = SystemAPI.GetComponentLookup<Game.Buildings.PropertyOnMarket>(true),
                    m_Renters = SystemAPI.GetBufferLookup<Game.Buildings.Renter>(false),
                    m_BuildingProperties = SystemAPI.GetComponentLookup<Game.Prefabs.BuildingPropertyData>(true),
                    m_ParkDatas = SystemAPI.GetComponentLookup<Game.Prefabs.ParkData>(true),
                    m_Prefabs = SystemAPI.GetComponentLookup<Game.Prefabs.PrefabRef>(true),
                    m_Companies = SystemAPI.GetComponentLookup<Game.Companies.CompanyData>(true),
                    m_Households = SystemAPI.GetComponentLookup<Game.Citizens.Household>(true),
                    m_Industrials = SystemAPI.GetComponentLookup<Game.Companies.IndustrialCompany>(true),
                    m_Commercials = SystemAPI.GetComponentLookup<Game.Companies.CommercialCompany>(true),
                    m_TriggerQueue = this.m_TriggerSystem.CreateActionBuffer(),
                    m_BuildingDatas = SystemAPI.GetComponentLookup<Game.Prefabs.BuildingData>(true),
                    m_ServiceCompanyDatas = SystemAPI.GetComponentLookup<Game.Companies.ServiceCompanyData>(true),
                    m_ProcessDatas = SystemAPI.GetComponentLookup<Game.Prefabs.IndustrialProcessData>(true),
                    m_WorkProviders = SystemAPI.GetComponentLookup<Game.Companies.WorkProvider>(false),
                    m_Citizens = SystemAPI.GetComponentLookup<Game.Citizens.Citizen>(false),
                    m_HouseholdCitizens = SystemAPI.GetBufferLookup<Game.Citizens.HouseholdCitizen>(true),
                    m_Abandoneds = SystemAPI.GetComponentLookup<Game.Buildings.Abandoned>(true),
                    m_HomelessHouseholds = SystemAPI.GetComponentLookup<Game.Citizens.HomelessHousehold>(true),
                    m_Parks = SystemAPI.GetComponentLookup<Game.Buildings.Park>(true),
                    m_Employees = SystemAPI.GetBufferLookup<Game.Companies.Employee>(true),
                    m_SpawnableBuildings = SystemAPI.GetComponentLookup<Game.Prefabs.SpawnableBuildingData>(true),
                    m_Attacheds = SystemAPI.GetComponentLookup<Game.Objects.Attached>(true),
                    m_ExtractorCompanies = SystemAPI.GetComponentLookup<Game.Companies.ExtractorCompany>(true),
                    m_SubAreas = SystemAPI.GetBufferLookup<Game.Areas.SubArea>(true),
                    m_Geometries = SystemAPI.GetComponentLookup<Game.Areas.Geometry>(true),
                    m_Lots = SystemAPI.GetComponentLookup<Game.Areas.Lot>(true),
                    m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs(),
                    m_Resources = SystemAPI.GetComponentLookup<Game.Prefabs.ResourceData>(true),
                    m_StatisticsQueue = this.m_CityStatisticsSystem.GetStatisticsEventQueue(out job3),
                    m_AreaType = Game.Zones.AreaType.Commercial,
                    m_PropertyRenters = SystemAPI.GetComponentLookup<Game.Buildings.PropertyRenter>(false),
                    m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer(),
                    m_RentQueue = this.m_RentQueue,
                    m_ReservedProperties = this.m_ReservedProperties
                };
                this.Dependency = jobData2.Schedule(JobHandle.CombineDependencies(this.Dependency, job3));
                this.m_CityStatisticsSystem.AddWriter(this.Dependency);
                this.m_TriggerSystem.AddActionBufferWriter(this.Dependency);
                this.m_EndFrameBarrier.AddJobHandleForProducer(this.Dependency);
            }
        }

        protected override void OnDestroy()
        {
            this.m_RentQueue.Dispose();
            this.m_ReservedProperties.Dispose();
            this.renterStorageQueue.Dispose();
            base.OnDestroy();
        }        
      
    }
}
