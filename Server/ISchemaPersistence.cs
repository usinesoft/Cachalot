using Client.Core;

namespace Server;

public interface ISchemaPersistence
{
    /// <summary>
    ///     Load schema for all collection
    /// </summary>
    /// <param name="fullPath">directory, if null use the one from <see cref="INodeConfig" /></param>
    /// <returns></returns>
    Schema LoadSchema(string fullPath = null);

    /// <summary>
    ///     Save global schema (for all collections)
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="schemaDirectory">directory, if null use the one from <see cref="INodeConfig" /></param>
    void SaveSchema(Schema schema, string schemaDirectory = null);
}