using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Components
{
    /// <summary>
    /// Added to households that were removed due to a lower number of properties
    /// </summary>
    public partial struct Evicted : IComponentData
    {
        /// <summary>
        /// The property that they were evicted from
        /// </summary>
        public Entity from;
    }
}
