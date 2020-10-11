using System;
using Client.Interface;

namespace StressTests
{
    public class AbstractEntity
    {
        [PrimaryKey(KeyDataType.StringKey)]
        public Guid Id { get; set; } = new Guid();

        [Index(KeyDataType.IntKey)]
        public int X { get; set; }

        [Index(KeyDataType.IntKey)]
        public int Y { get; set; }

        [Index(KeyDataType.IntKey)]
        public int Z { get; set; }
    }
}