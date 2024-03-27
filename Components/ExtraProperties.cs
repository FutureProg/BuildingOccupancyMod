using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Components
{
    public partial struct ExtraCommercialProperty : IComponentData
    {
        public int extraCount;
    }
}
