using System.Collections.Generic;
using System.Text.Json.Serialization;
using Client.Core;
using Client.Interface;


namespace Tests.TestData
{
    public class Business
    {
        [ServerSideValue(IndexType.Primary)]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [FullTextIndexation]
        [JsonPropertyName("streetAddress")]
        public string StreetAddress { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        [JsonPropertyName("city")]
        public string City { get; set; }

        [FullTextIndexation]
        [ServerSideValue(IndexType.Dictionary)]
        [JsonPropertyName("state")]
        public string State { get; set; }


        [ServerSideValue(IndexType.Dictionary)]
        [JsonPropertyName("stars")]
        public double Stars { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        [FullTextIndexation]
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }

        [FullTextIndexation]
        [JsonPropertyName("reviews")]
        public List<Review> Reviews { get; set; }
    }
}