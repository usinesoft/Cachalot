using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace StressTests.Model
{

    public class Product
    {
        [ServerSideValue(IndexType.Primary)]
        public int ProductId { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string Name { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        public string Brand { get; set; }

        [FullTextIndexation] 
        public string Summary { get; set; }

        [FullTextIndexation]
        public string Description { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public string ScanCode { get; set; }

        [ServerSideValue]
        public string ImageId { get; set; }

        [FullTextIndexation] 
        [ServerSideValue(IndexType.Dictionary)]
        public IList<string> Categories { get; set; } = new List<string>();

        [FullTextIndexation] 
        [ServerSideValue(IndexType.Dictionary)]
        public IList<string> Tags { get; set; } = new List<string>();

        [FullTextIndexation] 
        [ServerSideValue(IndexType.Dictionary)]
        public IList<string> Ingredients { get; set; } = new List<string>();

        [FullTextIndexation]
        public IList<string> About { get; set; } = new List<string>();

    }
}
