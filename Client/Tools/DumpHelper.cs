using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Newtonsoft.Json.Linq;

namespace Client.Tools
{
    public class DumpHelper
    {
        /// <summary>
        ///     Helper function. Deserialize and pack objects from a json file
        /// </summary>
        internal static IEnumerable<CachedObject> LoadObjects(string jsonPath, ICacheClient client)
        {
            var json = File.ReadAllText(jsonPath);

            var array = JArray.Parse(json);

            var info = client.GetClusterInformation();
            var typesByName = info.Schema.ToDictionary(td => td.FullTypeName);

            TypeDescription typeDescription = null;

            foreach (var item in array.Children<JObject>())
            {
                // Get the type description only once. All should have the same
                if (typeDescription == null)
                {
                    var type = item.Property("$type")?.Value?.ToString();
                    if (type == null) throw new FormatException("Type information not found");
                    var parts = type.Split(',');
                    if (parts.Length != 2) throw new FormatException("Can not parse type information:" + type);

                    typeDescription = typesByName[parts[0].Trim()];
                }


                var cachedObject = CachedObject.PackJson(item.ToString(), typeDescription);
                yield return cachedObject;
            }
        }

        internal static IEnumerable<CachedObject> LoadObjects(string jsonPath, IDataClient client)
        {
            var json = File.ReadAllText(jsonPath);

            var array = JArray.Parse(json);

            var info = client.GetClusterInformation();
            var typesByName = info.Schema.ToDictionary(td => td.FullTypeName);

            TypeDescription typeDescription = null;

            foreach (var item in array.Children<JObject>())
            {
                // Get the type description only once. All should have the same
                if (typeDescription == null)
                {
                    var type = item.Property("$type")?.Value?.ToString();
                    if (type == null) throw new FormatException("Type information not found");
                    var parts = type.Split(',');
                    if (parts.Length != 2) throw new FormatException("Can not parse type information:" + type);

                    typeDescription = typesByName[parts[0].Trim()];
                }


                var cachedObject = CachedObject.PackJson(item.ToString(), typeDescription);
                yield return cachedObject;
            }
        }


        /// <summary>
        ///     Helper function. Enumerate all the objects in the dump
        /// </summary>
        /// <param name="path">path of the dump</param>
        /// <param name="typeDescription"></param>
        /// <param name="shardIndex"></param>
        /// <returns></returns>
        public static IEnumerable<CachedObject> ObjectsInDump(string path, TypeDescription typeDescription,
            int shardIndex = -1)
        {
            var fileMask = shardIndex != -1
                ? $"{typeDescription.FullTypeName}_shard{shardIndex:D4}*.txt"
                : $"{typeDescription.FullTypeName}_shard*.txt";

            var files = Directory.GetFiles(path, fileMask);

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);

                var parts = content.Split(new[] {"\\-"}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(txt => txt.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

                foreach (var part in parts)
                {
                    var cachedObject = CachedObject.PackJson(part, typeDescription);
                    yield return cachedObject;
                }
            }
        }

        public static void DumpObjects(string path, TypeDescription typeDescription, int shardIndex,
            IEnumerable<CachedObject> objects)
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
                var fileMask = $"{typeDescription.FullTypeName}_shard{shardIndex:D4}_*.txt";

                var files = Directory.GetFiles(fullPath, fileMask);
                foreach (var file in files) File.Delete(file);
            }

            var index = 1;
            var fileIndex = 1;

            const int maxObjectsInFile = 1000;


            var sb = new StringBuilder();

            foreach (var cachedObject in objects)
            {
                var json = cachedObject.AsJson();


                sb.Append(json);
                sb.AppendLine();
                sb.AppendLine("\\-"); // separator which is illegal in json


                if (index == maxObjectsInFile)
                {
                    var fileName = $"{typeDescription.FullTypeName}_shard{shardIndex:D4}_{fileIndex:D5}.txt";
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
                var fileName = $"{typeDescription.FullTypeName}_shard{shardIndex:D4}_{fileIndex:D5}.txt";
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