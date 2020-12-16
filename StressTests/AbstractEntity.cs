using System;
using Client.Core;
using Client.Interface;

namespace StressTests
{
    public class AbstractEntity
    {
        [ServerSideValue(IndexType.Primary)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [ServerSideValue(IndexType.Dictionary)]
        public Guid CollectionId { get; set; }


        [ServerSideValue(IndexType.Dictionary)]
        public int X { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int Y { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int Z { get; set; }
    }
}