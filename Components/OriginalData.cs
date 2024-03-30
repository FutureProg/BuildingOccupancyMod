using Colossal.Serialization.Entities;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Components
{
    public partial struct BuildingOccupancyOriginalData : IBufferElementData
    {
        public int version;
        public int residentialPropertyCount;
        public PrefabID prefabId;

        public BuildingOccupancyOriginalData(int residentialPropertyCount, PrefabID prefabId)
        {
            version = 0;
            this.residentialPropertyCount = residentialPropertyCount;
            this.prefabId = prefabId;
        }

        //public void Deserialize<TReader>(TReader reader) where TReader : IReader
        //{
        //    reader.Read(out this.version);
        //    reader.Read(out this.residentialPropertyCount);
        //}

        //public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        //{
        //    writer.Write(this.version);
        //    writer.Write(this.residentialPropertyCount);
        //}
    }
}
