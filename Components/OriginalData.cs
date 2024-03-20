﻿using Colossal.Serialization.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace Trejak.BuildingOccupancyMod.Components
{
    public partial struct BuildingOccupancyOriginalData : IComponentData
    {
        public int version;
        public int residentialPropertyCount;

        public BuildingOccupancyOriginalData(int residentialPropertyCount)
        {
            version = 0;
            this.residentialPropertyCount = residentialPropertyCount;
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