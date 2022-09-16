using Newtonsoft.Json;
using System;

namespace Tests.TestData
{
    public class Review
    {
        [JsonProperty("text")] public string Text { get; set; }

        [JsonProperty("date")] public DateTimeOffset Date { get; set; }
    }
}