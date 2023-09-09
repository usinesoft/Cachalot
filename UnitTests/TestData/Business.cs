using System.Collections.Generic;
using Client.Core;
using Client.Interface;
using Newtonsoft.Json;

namespace Tests.TestData
{
    public class Business
    {
        [ServerSideValue(IndexType.Primary)]
        [JsonProperty("id")]
        public string Id { get; set; }

        [FullTextIndexation]
        [JsonProperty("streetAddress")]
        public string StreetAddress { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        [JsonProperty("city")]
        public string City { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        [JsonProperty("state")]
        public string State { get; set; }


        [ServerSideValue(IndexType.Dictionary)]
        [JsonProperty("stars")]
        public double Stars { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        [FullTextIndexation]
        [JsonProperty("categories")]
        public List<string> Categories { get; set; }

        [FullTextIndexation]
        [JsonProperty("reviews")]
        public List<Review> Reviews { get; set; }
    }
}