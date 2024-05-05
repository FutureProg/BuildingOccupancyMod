using Game;
using Game.Prefabs;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Systems
{
    public partial class DebugUISystem : GameSystemBase
    {

        protected override void OnCreate()
        {
            // Show Property Count
            var updateInfoMethod = (Entity entity, Entity prefab, GenericInfo info) => {
                var buildingProperty = this.EntityManager.GetComponentData<BuildingPropertyData>(prefab);
                var properties = buildingProperty.CountProperties();
                info.label = "Property Count";
                info.value = $"Property Count: {properties}";
            };
            this.World.GetOrCreateSystemManaged<SelectedInfoUISystem>().AddDeveloperInfo(new GenericInfo(
                (entity, prefab) => this.EntityManager.HasComponent<BuildingPropertyData>(prefab),
                updateInfoMethod
            ));
        }

        protected override void OnUpdate()
        {
            
        }
    }
}
