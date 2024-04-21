using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Client.Core;
using Client.Interface;
using JetBrains.Annotations;


namespace Client.Tools;

public static class DumpHelper
{
    internal static IEnumerable<PackedObject> LoadObjects(IDataClient @this, string jsonPath,
                                                          [NotNull] string collectionName)
    {
        if (collectionName == null) throw new ArgumentNullException(nameof(collectionName));

        var json = File.ReadAllText(jsonPath);

        var array = JsonDocument.Parse(json);

        var info = @this.GetClusterInformation();

        var schemaByName = info.Schema.ToDictionary(td => td.CollectionName.ToLower());

        if (!schemaByName.ContainsKey(collectionName.ToLower()))
            throw new CacheException($"Collection {collectionName} not found");

        var collectionSchema = schemaByName[collectionName];

        foreach (var item in array.RootElement.EnumerateArray())
        {
            var cachedObject = PackedObject.PackJson(item.ToString(), collectionSchema);
            yield return cachedObject;
        }
    }


    /// <summary>
    ///     Helper function. Enumerate all the objects in the dump
    /// </summary>
    /// <param name="path">path of the dump</param>
    /// <param name="collectionSchema"></param>
    /// <param name="shardIndex"></param>
    /// <returns></returns>
    public static IEnumerable<PackedObject> ObjectsInDump(string path, CollectionSchema collectionSchema,
                                                          int shardIndex = -1)
    {
        var fileMask = shardIndex != -1
            ? $"{collectionSchema.CollectionName}_shard{shardIndex:D4}*.gzip"
            : $"{collectionSchema.CollectionName}_shard*.gzip";

        var files = Directory.GetFiles(path, fileMask);

        foreach (var file in files)
        {
            var oneObject = new StringBuilder();

            using (var reader = File.OpenRead(file))
            using (var zip = new GZipStream(reader, CompressionMode.Decompress, true))
            using (var unzip = new StreamReader(zip))
            {
                while (!unzip.EndOfStream)
                {
                    var line = unzip.ReadLine();

                    if (line.StartsWith("\\-")) // object separator
                    {
                        var cachedObject = PackedObject.PackJson(oneObject.ToString(), collectionSchema);
                        yield return cachedObject;

                        oneObject = new();
                    }
                    else
                    {
                        oneObject.AppendLine(line);
                    }
                }
            }

            var lastContent = oneObject.ToString();
            if (!string.IsNullOrWhiteSpace(lastContent))
            {
                var cachedObject = PackedObject.PackJson(oneObject.ToString(), collectionSchema);
                yield return cachedObject;
            }
        }
    }

    public static void DumpObjects(string fullPath, CollectionSchema collectionSchema, int shardIndex,
                                   IEnumerable<PackedObject> objects)
    {
        if (!Directory.Exists(fullPath)) throw new NotSupportedException("Dump path not found:" + fullPath);

        if (shardIndex < 0) throw new ArgumentOutOfRangeException(nameof(shardIndex));


        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
        else
        {
            // clean the files corresponding to my type and shard
            var fileMask = $"{collectionSchema.CollectionName}_shard{shardIndex:D4}_*.txt";

            var files = Directory.GetFiles(fullPath, fileMask);
            foreach (var file in files) File.Delete(file);
        }

        var index = 1;
        var fileIndex = 1;

        const int maxObjectsInFile = 50_000;


        var fileName = $"{collectionSchema.CollectionName}_shard{shardIndex:D4}_{fileIndex:D5}.gzip";
        var filePath = Path.Combine(fullPath, fileName);
        var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        var zstream = new GZipStream(stream, CompressionMode.Compress, false);
        var writer = new StreamWriter(zstream);

        foreach (var cachedObject in objects)
        {
            var json = cachedObject.AsJson(collectionSchema);

            writer.Write(json);
            writer.WriteLine();
            writer.WriteLine("\\-");

            if (index == maxObjectsInFile)
            {
                writer.Dispose();


                fileIndex++;

                fileName = $"{collectionSchema.CollectionName}_shard{shardIndex:D4}_{fileIndex:D5}.gzip";
                filePath = Path.Combine(fullPath, fileName);

                stream = new(filePath, FileMode.Create, FileAccess.Write);
                zstream = new(stream, CompressionMode.Compress, false);
                writer = new(zstream);


                index = 0;
            }

            index++;
        }

        // write what's left after the last complete block            
        writer.Dispose();
    }

    /// <summary>
    ///     Dumps are stored as subdirectories like "2018-02-15_..."
    ///     If a sub directory is is specified take it as is. Otherwise take the most recent sub directory
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string NormalizeDumpPath(string path)
    {
        if (!Directory.Exists(path)) throw new NotSupportedException($"The directory {path} does not exist ");

        // check if the directory is a specific backup or a backup root
        var dirName = new DirectoryInfo(path).Name;
        var parts = dirName.Split('_');
        if (parts.Length > 1)
        {
            parts = parts[0].Split('-');
            if (parts.Length == 3)
                if (int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _) && int.TryParse(parts[2], out _))
                    return path;
        }


        return Directory.GetDirectories(path).OrderBy(d => d).Last();
    }
}