using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Server.Persistence;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Server
{
    class SequencePersistence : ISequencePersistence
    {

        private readonly INodeConfig _config;

        private readonly JsonSerializerSettings _schemaSerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented

        };

        private readonly object _syncRoot = new object();


        private readonly JsonSerializer _jsonSerializer;

        public SequencePersistence(INodeConfig config)
        {
            _config = config;
            _jsonSerializer = JsonSerializer.Create(_schemaSerializerSettings);
            _jsonSerializer.Converters.Add(new StringEnumConverter());
        }

        public Dictionary<string, int> LoadValues(string fullPath)
        {

            lock (_syncRoot)
            {
                var path = fullPath;
                if (fullPath == null)
                {
                    path = Path.Combine(_config.DataPath, Constants.DataPath);

                    path = Path.Combine(path, Constants.SequenceFileName);
                }


                if (!File.Exists(path)) return null;

                var json = File.ReadAllText(path);

                return _jsonSerializer.Deserialize<Dictionary<string, int>>(
                    new JsonTextReader(new StringReader(json)));
            }
        }

        public void SaveValues(Dictionary<string, int> lastValueByName, string fullPath = null)
        {
            lock (_syncRoot)
            {

                var path = fullPath;
                var filePath = fullPath;

                if (path == null)
                {
                    path = Path.Combine(_config.DataPath, Constants.DataPath);

                    filePath = Path.Combine(path, Constants.SequenceFileName);
                }
                else
                {
                    path = Path.GetDirectoryName(fullPath);
                }


                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var sb = new StringBuilder();

                _jsonSerializer.Serialize(new JsonTextWriter(new StringWriter(sb)), lastValueByName);

                var json = sb.ToString();

                File.WriteAllText(filePath, json);
            }

        }

    }
}