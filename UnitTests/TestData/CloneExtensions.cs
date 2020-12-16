using Newtonsoft.Json;

namespace Tests.TestData
{
    public static class CloneExtensions
    {
        public static T Clone<T>(this T original)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            var json = JsonConvert.SerializeObject(original, settings);

            return JsonConvert.DeserializeObject<T>(json, settings);
        }
    }
}