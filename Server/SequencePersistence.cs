using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Persistence;

namespace Server;

internal class SequencePersistence : ISequencePersistence
{
    private readonly INodeConfig _config;


    private readonly JsonSerializerOptions _schemaSerializerSettings = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }

    };
    private readonly object _syncRoot = new();

    public SequencePersistence(INodeConfig config)
    {
        _config = config;
      
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

            return JsonSerializer.Deserialize<Dictionary<string, int>>(json);
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


            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            
            var json = JsonSerializer.Serialize(lastValueByName, _schemaSerializerSettings);


            File.WriteAllText(filePath, json);
        }
    }
}