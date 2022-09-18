using Client.Core;
using Client.Interface;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Client.Tools
{
    public static class DumpHelper
    {

        internal static IEnumerable<PackedObject> LoadObjects(IDataClient @this, string jsonPath, [NotNull] string collectionName)
        {
            if (collectionName == null) throw new ArgumentNullException(nameof(collectionName));

            var json = File.ReadAllText(jsonPath);

            var array = JArray.Parse(json);

            var info = @this.GetClusterInformation();

            var schemaByName = info.Schema.ToDictionary(td => td.CollectionName.ToLower());

            if (!schemaByName.ContainsKey(collectionName.ToLower()))
                throw new CacheException($"Collection {collectionName} not found");

            CollectionSchema collectionSchema = schemaByName[collectionName];

            foreach (var item in array.Children<JObject>())
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
                ? $"{collectionSchema.CollectionName}_shard{shardIndex:D4}*.txt"
                : $"{collectionSchema.CollectionName}_shard*.txt";

            var files = Directory.GetFiles(path, fileMask);

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);

                var parts = content.Split(new[] { "\\-" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(txt => txt.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

                foreach (var part in parts)
                {
                    var cachedObject = PackedObject.PackJson(part, collectionSchema);
                    yield return cachedObject;
                }
            }
        }

        public static void DumpObjects(string path, CollectionSchema collectionSchema, int shardIndex,
            IEnumerable<PackedObject> objects)
        {
            if (!Directory.Exists(path)) throw new NotSupportedException("Dump path not found:" + path);

            if (shardIndex < 0) throw new ArgumentOutOfRangeException(nameof(shardIndex));

            var date = DateTime.Today.ToString("yyyy-MM-dd");

            var fullPath = Path.Combine(path, date);

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

            const int maxObjectsInFile = 1000;


            var sb = new StringBuilder();

            foreach (var cachedObject in objects)
            {
                var json = cachedObject.AsJson(collectionSchema);


                sb.Append(json);
                sb.AppendLine();
                sb.AppendLine("\\-"); // separator which is illegal in json


                if (index == maxObjectsInFile)
                {
                    var fileName = $"{collectionSchema.CollectionName}_shard{shardIndex:D4}_{fileIndex:D5}.txt";
                    var filePath = Path.Combine(fullPath, fileName);
                    File.WriteAllText(filePath, sb.ToString());
                    sb = new StringBuilder();
                    index = 0;
                    fileIndex++;
                }

                index++;
            }


            // write what's left after the last complete block
            var content = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                var fileName = $"{collectionSchema.CollectionName}_shard{shardIndex:D4}_{fileIndex:D5}.txt";
                var filePath = Path.Combine(fullPath, fileName);
                File.WriteAllText(filePath, content);
            }
        }

        /// <summary>
        ///     Dumps are stored as subdirectories like "2018-02-15"
        ///     If a sub directory is is specified take it as is. Otherwise take the most recent sub directory
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string NormalizeDumpPath(string path)
        {
            if (!Directory.Exists(path)) throw new NotSupportedException($"The directory {path} does not exist ");

            if (path.Length > 10)
            {
                var subdir = path.Substring(path.Length - 10);
                var parts = subdir.Split('-');
                if (parts.Length == 3)
                    if (int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _) && int.TryParse(parts[2], out _))
                        return path;
            }

            return Directory.GetDirectories(path).OrderBy(d => d).Last();
        }
    }
}