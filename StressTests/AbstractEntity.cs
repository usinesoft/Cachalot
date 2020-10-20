using System;
using Client.Interface;

namespace StressTests
{
    public class AbstractEntity
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Index]
        public Guid CollectionId { get; set; }


        [Index]
        public int X { get; set; }

        [Index]
        public int Y { get; set; }

        [Index]
        public int Z { get; set; }
    }
}