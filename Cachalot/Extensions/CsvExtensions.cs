using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cachalot.Linq;
using Client.Core;
using Client.Tools;

namespace Cachalot.Extensions;

public static class CsvExtensions
{
    private static IEnumerable<string> LinesGenerator(TextReader reader)
    {
        try
        {
            var line = reader.ReadLine();
            while (line != null)
            {
                yield return line;

                line = reader.ReadLine();
            }
        }
        finally
        {
            reader.Close();
        }
    }


    public static void FeedCsvWithAutomaticSchema(this Connector connector, string csvFileName, string collectionName)
    {
        var csvSchema = new CsvSchemaBuilder(csvFileName).InferSchema();

        using var fileStream = File.OpenRead(csvFileName);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 50000);

        var _ = reader.ReadLine(); // first one is the header => ignore it

        var schema = csvSchema.ToCollectionSchema();

        connector.DeclareCollection(collectionName, schema);

        connector.FeedWithCsvLines(collectionName, LinesGenerator(reader), csvSchema);
    }

    public static void FeedCsv(this Connector connector, string csvFileName, string collectionName, CsvSchema csvSchema)
    {
        using var fileStream = File.OpenRead(csvFileName);

        using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 50000);

        var header = reader.ReadLine();


        connector.FeedWithCsvLines(collectionName, LinesGenerator(reader), csvSchema);
    }


    /// <summary>
    ///     Pack a data line of a csv file. It also generates unique primary keys
    /// </summary>
    /// <param name="connector">used for primary key generation</param>
    /// <param name="lines"></param>
    /// <param name="collectionName"></param>
    /// <param name="csvSchema"></param>
    /// <returns></returns>
    public static IEnumerable<PackedObject> PackCsv(this Connector connector, IEnumerable<string> lines,
                                                    string collectionName, CsvSchema csvSchema)
    {
        if (connector is null) throw new ArgumentNullException(nameof(connector));


        var processingQueue = new BlockingCollection<PackedObject>(200_000);


        Task.Run(() =>
        {
            var primaryKeyIndex = 0;

            var processedLines = 0;

            // as we do not know how many lines there are, generate unique ids by pack of 1000
            var ids = connector.GenerateUniqueIds($"{collectionName}_id", 1000);

            foreach (var line in lines)
            {
                processedLines++;

                var packed = PackedObject.PackCsv(ids[primaryKeyIndex], line, collectionName, csvSchema);

                KeyValuePool.ProcessPackedObject(packed);

                processingQueue.Add(packed);

                primaryKeyIndex++;

                if (primaryKeyIndex == ids.Length - 1) // a new pack of unique ids is required
                {
                    ids = connector.GenerateUniqueIds($"{collectionName}_id", 1000);

                    primaryKeyIndex = 0;
                }

                if (processedLines % 10_000 == 0) connector.NotifyProgress(processedLines);
            }

            processingQueue.CompleteAdding();
        });

        return processingQueue.GetConsumingEnumerable();
    }
}