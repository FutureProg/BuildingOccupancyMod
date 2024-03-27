using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst.Intrinsics;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class CheckBuildingsSystem : GameSystemBase
    {

        bool run;
        public bool initialized;
        OccupancyPrefabInitSystem m_PrefabInitSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            this.run = false;
            this.initialized = false;
            m_PrefabInitSystem = World.GetOrCreateSystemManaged<OccupancyPrefabInitSystem>();            
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            this.initialized = false;
            if (mode == GameMode.Game && purpose == Purpose.LoadGame)
            {
                this.run = true;
            }
        }

        protected override void OnUpdate()
        {
            if (this.run && m_PrefabInitSystem.initialized)
            {



                this.run = false;
                this.initialized = true;
            }            
        }

        private partial struct CheckBuildings : IJobChunk
        {

            ComponentTypeHandle<PrefabRef> prefabRefHandle;
            BufferTypeHandle<Renter> renterHandle;

            ComponentLookup<CommercialProperty> commercialPropertyLookup;
            ComponentLookup<BuildingPropertyData> buildingPropertiesLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                
            }
        }

    }
}
