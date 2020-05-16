using System.Collections.Generic;
using Client.Interface;
using Newtonsoft.Json;

namespace UnitTests.TestData
{
    public class Business
    {
        [PrimaryKey(KeyDataType.StringKey)]
        [JsonProperty("id")]
        public string Id { get; set; }

        [FullTextIndexation]
        [JsonProperty("streetAddress")]
        public string StreetAddress { get; set; }

        [FullTextIndexation]
        [Index(KeyDataType.StringKey)]
        [JsonProperty("city")]
        public string City { get; set; }

        [FullTextIndexation]
        [Index(KeyDataType.StringKey)]
        [JsonProperty("state")]
        public string State { get; set; }


        [Index(KeyDataType.IntKey, true)]
        [JsonProperty("stars")]
        public double Stars { get; set; }

        [Index(KeyDataType.StringKey)]
        [FullTextIndexation]
        [JsonProperty("categories")]
        public List<string> Categories { get; set; }

        [FullTextIndexation]
        [JsonProperty("reviews")]
        public List<Review> Reviews { get; set; }
    }
}