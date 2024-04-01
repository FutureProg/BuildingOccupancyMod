using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Components
{
    public struct ResetHouseholdsTrigger: IComponentData
    {
        public ResetType resetType;
    }

    public enum ResetType : byte
    {
        FindNewHome = 1,
        Delete = 2
    }
}
