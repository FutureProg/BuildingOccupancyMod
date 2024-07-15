using Game.Buildings;
using Game.Companies;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Trejak.BuildingOccupancyMod.Jobs
{

    public struct TempRenterStorage
    {
        public Entity property;
        public Entity company;
        public int index;
        public int rent;
        public int maxRent;
    }

    public partial struct RestoreRentersJob : IJob
    {
        public BufferLookup<Renter> renterLookup;
        public NativeQueue<TempRenterStorage> renterStorageList;
        public ComponentLookup<PropertyRenter> propertyRenterLookup;

        public void Execute()
        {
            while(renterStorageList.TryDequeue(out var item))
            {
                if (renterLookup.TryGetBuffer(item.company, out var renters))
                {
                    renters.Add(new Renter() { m_Renter = item.company });
                }
                if (!propertyRenterLookup.HasComponent(item.company))
                {
                    propertyRenterLookup[item.company] = new PropertyRenter()
                    {
                        m_Rent = item.rent,
                        m_Property = item.property
                    };
                }
            }            
        }
    }

    public partial struct TempPropertyRenter : IComponentData
    {
        public Entity property;
        public int maxRent;
        public int rent;
    }

    /// <summary>
    /// Grabs building OnTheMarket + CommercialProperty and if has space for companies, removes em all temporarily
    /// </summary>
    public partial struct TemporaryClearRentersJob : IJobChunk
    {
        public BufferTypeHandle<Renter> renterHandle;
        public EntityTypeHandle entityTypeHandle;
        public ComponentTypeHandle<PrefabRef> prefabHandle;
        
        public ComponentLookup<BuildingPropertyData> buildingPropertyDataLookup;
        public ComponentLookup<CommercialCompany> commercialCompanyLookup;
        public ComponentLookup<PropertyRenter> propertyRenterLookup;
        
        public NativeQueue<TempRenterStorage> renterStorageList;       

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var buildingEntities = chunk.GetNativeArray(entityTypeHandle);
            var renterBuffers = chunk.GetBufferAccessor(ref renterHandle);
            var prefabs = chunk.GetNativeArray(ref prefabHandle);
            NativeList<TempRenterStorage> tempStorageList = new NativeList<TempRenterStorage>(Allocator.Temp);

            for (int i = 0; i < buildingEntities.Length; i++)
            {                
                var building = buildingEntities[i];
                var prefab = prefabs[i].m_Prefab;
                var renters = renterBuffers[i];
                if (buildingPropertyDataLookup.HasComponent(prefab))
                {                    
                    for (int j = 0; j < renters.Length; j++)
                    {
                        var renter = renters[j];
                        if (commercialCompanyLookup.HasComponent(renter) && propertyRenterLookup.TryGetComponent(renter, out var propertyRenterComponent))
                        {
                            tempStorageList.Add(new TempRenterStorage()
                            {
                                property = building,
                                company = renter.m_Renter,
                                rent = propertyRenterComponent.m_Rent
                            });
                        }
                    }
                    if (tempStorageList.Length < buildingPropertyDataLookup[prefab].CountProperties(Game.Zones.AreaType.Commercial))
                    {
                        for (int j = renters.Length - 1; j >= 0; j--)
                        {                            
                            if (commercialCompanyLookup.HasComponent(renters[j]))
                            {
                                renters.RemoveAt(j);
                            }
                        }
                        foreach(var item in tempStorageList)
                        {
                            renterStorageList.Enqueue(item);
                        }                        
                    }
                    tempStorageList.Clear();
                }
            }
            tempStorageList.Dispose();
        }
    }
}
