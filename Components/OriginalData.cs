using Colossal.Serialization.Entities;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Components
{
    public struct BuildingOccupancyOriginalData : IBufferElementData
    {
        public int residentialPropertyCount;        
        public FixedString512Bytes prefabName;

        public BuildingOccupancyOriginalData(int residentialPropertyCount, string prefabName)
        {
            this.residentialPropertyCount = residentialPropertyCount;
            this.prefabName = prefabName;
        }
    }
}
