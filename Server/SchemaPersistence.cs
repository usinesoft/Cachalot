using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Client.Core;

using Constants = Server.Persistence.Constants;

namespace Server;

internal class SchemaPersistence : ISchemaPersistence
{
    private readonly INodeConfig _config;

    

    private readonly JsonSerializerOptions _schemaSerializerSettings = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }

    };

    public SchemaPersistence(INodeConfig config)
    {
        _config = config;
        
    }

    public Schema LoadSchema(string fullPath = null)
    {
        var path = fullPath;


        if (fullPath == null) path = Path.Combine(_config.DataPath, Constants.DataPath);


        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<Schema>(json, _schemaSerializerSettings);
    }

    public void SaveSchema(Schema schema, string schemaDirectory = null)
    {

        var json = JsonSerializer.Serialize(schema, _schemaSerializerSettings);


        var path = schemaDirectory;
        if (schemaDirectory == null) path = Path.Combine(_config.DataPath, Constants.DataPath);


        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        File.WriteAllText(Path.Combine(path, Constants.SchemaFileName), json);
    }
}