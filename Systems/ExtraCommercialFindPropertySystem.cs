using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trejak.BuildingOccupancyMod.Components;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class ExtraCommercialFindPropertySystem : GameSystemBase
    {

        EntityQuery m_ExtraCommercialSpacesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ExtraCommercialSpacesQuery = GetEntityQuery(
                ComponentType.ReadWrite<PropertyOnMarket>(),
                ComponentType.ReadWrite<CommercialProperty>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<ExtraCommercialProperty>(),
                ComponentType.Exclude<Abandoned>(),
                ComponentType.Exclude<Condemned>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );            

        }

        protected override void OnUpdate()
        {
            
        }
    }
}
