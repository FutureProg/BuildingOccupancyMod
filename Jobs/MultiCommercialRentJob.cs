using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Jobs;
using static Game.Buildings.PropertyUtils;
using Unity.Mathematics;
using Unity.Collections;
using Game.City;
using Game.Triggers;
using Game.Buildings;
using Game.Areas;
using Game.Citizens;
using Game.Companies;
using Game.Agents;
using Game.Pathfind;
using UnityEngine;
using Game.Economy;

namespace BuildingOccupancyMod.Jobs
{

    /// <summary>
    /// @Deprecated
    /// </summary>
    [Obsolete]
    public partial struct MultiCommercialRentJob : IJob
    {

        [ReadOnly]
        public EntityArchetype m_RentEventArchetype;

        [ReadOnly]
        public EntityArchetype m_MovedEventArchetype;

        public ComponentLookup<WorkProvider> m_WorkProviders;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDatas;

        [ReadOnly]
        public ComponentLookup<ParkData> m_ParkDatas;

        [ReadOnly]
        public ComponentLookup<PropertyOnMarket> m_PropertiesOnMarket;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingProperties;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;

        [ReadOnly]
        public ComponentLookup<Household> m_Households;

        [ReadOnly]
        public ComponentLookup<CompanyData> m_Companies;

        [ReadOnly]
        public ComponentLookup<CommercialCompany> m_Commercials;

        [ReadOnly]
        public ComponentLookup<IndustrialCompany> m_Industrials;

        [ReadOnly]
        public ComponentLookup<IndustrialProcessData> m_ProcessDatas;

        [ReadOnly]
        public ComponentLookup<ServiceCompanyData> m_ServiceCompanyDatas;

        public ComponentLookup<Citizen> m_Citizens;

        [ReadOnly]
        public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

        [ReadOnly]
        public ComponentLookup<Abandoned> m_Abandoneds;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.Park> m_Parks;

        [ReadOnly]
        public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

        [ReadOnly]
        public BufferLookup<Employee> m_Employees;

        [ReadOnly]
        public BufferLookup<Game.Areas.SubArea> m_SubAreas;

        [ReadOnly]
        public ComponentLookup<Game.Areas.Lot> m_Lots;

        [ReadOnly]
        public ComponentLookup<Geometry> m_Geometries;

        [ReadOnly]
        public ComponentLookup<Attached> m_Attacheds;

        [ReadOnly]
        public ComponentLookup<Game.Companies.ExtractorCompany> m_ExtractorCompanies;

        [ReadOnly]
        public ResourcePrefabs m_ResourcePrefabs;

        [ReadOnly]
        public ComponentLookup<ResourceData> m_Resources;

        public ComponentLookup<PropertyRenter> m_PropertyRenters;

        public BufferLookup<Renter> m_Renters;

        public Game.Zones.AreaType m_AreaType;

        public EntityCommandBuffer m_CommandBuffer;

        public NativeQueue<RentAction> m_RentQueue;

        public NativeList<Entity> m_ReservedProperties;

        public NativeQueue<TriggerAction> m_TriggerQueue;

        public NativeQueue<StatisticsEvent> m_StatisticsQueue;

        public void Execute()
        {
            RentAction item;
            while (m_RentQueue.TryDequeue(out item))
            {
                Entity value = item.m_Property;
                if (!m_Renters.HasBuffer(value) || !m_Prefabs.HasComponent(item.m_Renter))
                {
                    continue;
                }
                if (!m_ReservedProperties.Contains(value))
                {
                    DynamicBuffer<Renter> dynamicBuffer = m_Renters[value];
                    Entity prefab = m_Prefabs[value].m_Prefab;
                    int num = 0;
                    bool flag = false;
                    if (m_BuildingProperties.HasComponent(prefab))
                    {
                        num = m_BuildingProperties[prefab].CountProperties(m_AreaType);
                        for (int i = 0; i < dynamicBuffer.Length; i++)
                        {
                            Entity renter = dynamicBuffer[i].m_Renter;
                            if (m_AreaType == Game.Zones.AreaType.Residential)
                            {
                                if (m_Households.HasComponent(renter))
                                {
                                    num--;
                                }
                            }
                            else if (m_Companies.HasComponent(renter))
                            {
                                num--;
                            }
                        }
                    }
                    else if (m_BuildingDatas.HasComponent(prefab) && (m_Abandoneds.HasComponent(value) || (m_Parks.HasComponent(value) && m_ParkDatas[prefab].m_AllowHomeless)))
                    {
                        num = HomelessShelterAISystem.GetShelterCapacity(m_BuildingDatas[prefab], m_BuildingProperties.HasComponent(prefab) ? m_BuildingProperties[prefab] : default(BuildingPropertyData)) - m_Renters[value].Length;
                        flag = true;
                    }
                    if (num > 0)
                    {
                        if (m_PropertyRenters.HasComponent(item.m_Renter))
                        {
                            if (m_WorkProviders.HasComponent(item.m_Renter) && m_Employees.HasBuffer(item.m_Renter) && CountWorkplaces(item, prefab) < m_Employees[item.m_Renter].Length)
                            {
                                continue;
                            }
                            PropertyRenter value2 = m_PropertyRenters[item.m_Renter];
                            if (m_Renters.HasBuffer(value2.m_Property))
                            {
                                DynamicBuffer<Renter> dynamicBuffer2 = m_Renters[value2.m_Property];
                                for (int j = 0; j < dynamicBuffer2.Length; j++)
                                {
                                    if (dynamicBuffer2[j].m_Renter.Equals(item.m_Renter))
                                    {
                                        dynamicBuffer2.RemoveAt(j);
                                        break;
                                    }
                                }
                                Entity e = m_CommandBuffer.CreateEntity(m_RentEventArchetype);
                                m_CommandBuffer.SetComponent(e, new RentersUpdated(value2.m_Property));
                            }
                            if (m_Prefabs.HasComponent(value2.m_Property) && !m_PropertiesOnMarket.HasComponent(value2.m_Property))
                            {
                                m_CommandBuffer.AddComponent(value2.m_Property, default(PropertyToBeOnMarket));
                            }
                            if (value == Entity.Null)
                            {
                                UnityEngine.Debug.LogWarning("trying to rent null property");
                            }
                            value2.m_Property = value;
                            if (m_PropertiesOnMarket.HasComponent(value))
                            {
                                if (m_BuildingProperties.HasComponent(prefab))
                                {
                                    BuildingPropertyData buildingPropertyData = m_BuildingProperties[prefab];
                                    if (buildingPropertyData.m_ResidentialProperties > 0 && (buildingPropertyData.m_AllowedSold != Resource.NoResource || buildingPropertyData.m_AllowedManufactured != Resource.NoResource) && !m_Households.HasComponent(item.m_Renter))
                                    {
                                        value2.m_Rent = Mathf.RoundToInt(/*RentAdjustSystem.kMixedCompanyRent*/ 1 * (float)m_PropertiesOnMarket[value].m_AskingRent * (float)buildingPropertyData.m_ResidentialProperties / (1f - /*PropertyUtils.kMixedCompanyRent*/0));
                                    }
                                    else
                                    {
                                        value2.m_Rent = m_PropertiesOnMarket[value].m_AskingRent;
                                    }
                                }
                                else
                                {
                                    value2.m_Rent = m_PropertiesOnMarket[value].m_AskingRent;
                                }
                            }
                            else
                            {
                                value2.m_Rent = 0;
                                //value2.m_MaxRent = 0;
                            }
                            m_PropertyRenters[item.m_Renter] = value2;
                            dynamicBuffer.Add(new Renter
                            {
                                m_Renter = item.m_Renter
                            });
                        }
                        else
                        {
                            dynamicBuffer.Add(new Renter
                            {
                                m_Renter = item.m_Renter
                            });
                            int rent = 0;
                            if (m_PropertiesOnMarket.HasComponent(value))
                            {
                                if (m_BuildingProperties.HasComponent(prefab))
                                {
                                    BuildingPropertyData buildingPropertyData2 = m_BuildingProperties[prefab];
                                    rent = ((buildingPropertyData2.m_ResidentialProperties <= 0 || (buildingPropertyData2.m_AllowedSold == Resource.NoResource && buildingPropertyData2.m_AllowedManufactured == Resource.NoResource) || m_Households.HasComponent(item.m_Renter)) ? m_PropertiesOnMarket[value].m_AskingRent : Mathf.RoundToInt(/*RentAdjustSystem.kMixedCompanyRent*/ 1 * (float)m_PropertiesOnMarket[value].m_AskingRent * (float)buildingPropertyData2.m_ResidentialProperties / (1f - /*RentAdjustSystem.kMixedCompanyRent*/ 0)));
                                }
                                else
                                {
                                    rent = m_PropertiesOnMarket[value].m_AskingRent;
                                }
                            }
                            m_CommandBuffer.AddComponent(item.m_Renter, new PropertyRenter
                            {
                                m_Property = value,
                                m_Rent = rent
                            });
                        }
                        if (m_Companies.HasComponent(item.m_Renter) && m_Prefabs.TryGetComponent(item.m_Renter, out var componentData) && m_Companies[item.m_Renter].m_Brand != Entity.Null)
                        {
                            m_TriggerQueue.Enqueue(new TriggerAction
                            {
                                m_PrimaryTarget = item.m_Renter,
                                m_SecondaryTarget = item.m_Property,
                                m_TriggerPrefab = componentData.m_Prefab,
                                m_TriggerType = TriggerType.BrandRented
                            });
                        }
                        if (m_WorkProviders.HasComponent(item.m_Renter))
                        {
                            Entity renter2 = item.m_Renter;
                            WorkProvider value3 = m_WorkProviders[renter2];
                            int num2 = CountWorkplaces(item, prefab);
                            value3.m_MaxWorkers = math.max(math.min(value3.m_MaxWorkers, num2), 2 * num2 / 3);
                            m_WorkProviders[renter2] = value3;
                        }
                        if (m_HouseholdCitizens.HasBuffer(item.m_Renter))
                        {
                            DynamicBuffer<HouseholdCitizen> dynamicBuffer3 = m_HouseholdCitizens[item.m_Renter];
                            for (int k = 0; k < dynamicBuffer3.Length; k++)
                            {
                                Entity citizen = dynamicBuffer3[k].m_Citizen;
                                if (m_Citizens.HasComponent(citizen))
                                {
                                    Citizen value4 = m_Citizens[citizen];
                                    value4.m_State |= CitizenFlags.NeedsNewJob;
                                    m_Citizens[citizen] = value4;
                                }
                            }
                            if (m_BuildingProperties.HasComponent(prefab) && m_HomelessHouseholds.HasComponent(item.m_Renter))
                            {
                                m_CommandBuffer.RemoveComponent<HomelessHousehold>(item.m_Renter);
                            }
                            else if (!m_BuildingProperties.HasComponent(prefab) && !m_HomelessHouseholds.HasComponent(item.m_Renter))
                            {
                                m_CommandBuffer.AddComponent(item.m_Renter, new HomelessHousehold
                                {
                                    m_TempHome = value
                                });
                            }
                            if (m_BuildingProperties.HasComponent(prefab) && m_PropertyRenters.HasComponent(item.m_Renter))
                            {
                                foreach (HouseholdCitizen item2 in dynamicBuffer3)
                                {
                                    m_TriggerQueue.Enqueue(new TriggerAction(TriggerType.CitizenMovedHouse, Entity.Null, item2.m_Citizen, m_PropertyRenters[item.m_Renter].m_Property));
                                }
                            }
                        }
                        if (m_BuildingProperties.HasComponent(prefab) && dynamicBuffer.Length >= m_BuildingProperties[prefab].CountProperties())
                        {
                            m_ReservedProperties.Add(in value);
                            m_CommandBuffer.RemoveComponent<PropertyOnMarket>(value);
                        }
                        else if (flag && num <= 1)
                        {
                            m_ReservedProperties.Add(in value);
                        }
                        Entity e2 = m_CommandBuffer.CreateEntity(m_RentEventArchetype);
                        m_CommandBuffer.SetComponent(e2, new RentersUpdated(value));
                        if (m_MovedEventArchetype.Valid)
                        {
                            e2 = m_CommandBuffer.CreateEntity(m_MovedEventArchetype);
                            m_CommandBuffer.SetComponent(e2, new PathTargetMoved(item.m_Renter, default(float3), default(float3)));
                        }
                    }
                    else if (m_BuildingProperties.HasComponent(prefab) && dynamicBuffer.Length >= m_BuildingProperties[prefab].CountProperties())
                    {
                        m_CommandBuffer.RemoveComponent<PropertyOnMarket>(value);
                    }
                }
                else
                {
                    m_CommandBuffer.AddComponent<PropertySeeker>(item.m_Renter);
                }
            }
            m_ReservedProperties.Clear();
        }

        private int CountWorkplaces(RentAction rentAction, Entity prefab)
        {
            if (m_Prefabs.HasComponent(rentAction.m_Renter))
            {
                int level = 1;
                Entity prefab2 = m_Prefabs[rentAction.m_Renter].m_Prefab;
                if (m_BuildingDatas.HasComponent(prefab) && m_BuildingProperties.HasComponent(prefab))
                {
                    BuildingPropertyData properties = m_BuildingProperties[prefab];
                    if (m_SpawnableBuildings.HasComponent(prefab))
                    {
                        level = m_SpawnableBuildings[prefab].m_Level;
                    }
                    if (m_ServiceCompanyDatas.HasComponent(prefab2))
                    {
                        return CommercialAISystem.GetFittingWorkers(m_BuildingDatas[prefab], properties, level, m_ServiceCompanyDatas[prefab2]);
                    }
                    if (m_ProcessDatas.HasComponent(prefab2))
                    {
                        if (m_ExtractorCompanies.HasComponent(rentAction.m_Renter))
                        {
                            Attached attached = m_Attacheds[rentAction.m_Property];
                            float area = ExtractorAISystem.GetArea(m_SubAreas[attached.m_Parent], m_Lots, m_Geometries);
                            return math.max(1, ExtractorAISystem.GetFittingWorkers(area, 1f, m_ProcessDatas[prefab2]) / 2);
                        }
                        return IndustrialAISystem.GetFittingWorkers(m_BuildingDatas[prefab], properties, level, m_ProcessDatas[prefab2]);
                    }
                }
            }
            return 0;
        }

    }
}
