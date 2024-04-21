using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Tests.TestData
{
    public class Review
    {
        [JsonPropertyName("text")] public string Text { get; set; }

        [JsonPropertyName("date")] public DateTimeOffset Date { get; set; }
    }
}