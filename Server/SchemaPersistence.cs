using System.IO;
using System.Text;
using Client.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Constants = Server.Persistence.Constants;

namespace Server;

internal class SchemaPersistence : ISchemaPersistence
{
    private readonly INodeConfig _config;

    private readonly JsonSerializer _jsonSerializer;

    private readonly JsonSerializerSettings _schemaSerializerSettings = new()
    {
        Formatting = Formatting.Indented
    };

    public SchemaPersistence(INodeConfig config)
    {
        _config = config;
        _jsonSerializer = JsonSerializer.Create(_schemaSerializerSettings);
        _jsonSerializer.Converters.Add(new StringEnumConverter());
    }

    public Schema LoadSchema(string fullPath = null)
    {
        var path = fullPath;
        if (fullPath == null) path = Path.Combine(_config.DataPath, Constants.DataPath);


        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);

        return _jsonSerializer.Deserialize<Schema>(
            new JsonTextReader(new StringReader(json)));
    }

    public void SaveSchema(Schema schema, string schemaDirectory = null)
    {
        var sb = new StringBuilder();

        _jsonSerializer.Serialize(new JsonTextWriter(new StringWriter(sb)), schema);

        var json = sb.ToString();


        var path = schemaDirectory;
        if (schemaDirectory == null) path = Path.Combine(_config.DataPath, Constants.DataPath);


        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        File.WriteAllText(Path.Combine(path, Constants.SchemaFileName), json);
    }
}