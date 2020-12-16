using System;
using Newtonsoft.Json;

namespace Tests.TestData
{
    public class Review
    {
        [JsonProperty("text")] public string Text { get; set; }

        [JsonProperty("date")] public DateTimeOffset Date { get; set; }
    }
}