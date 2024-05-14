using System.Diagnostics;
using System.Text;
using Cachalot.Extensions;
using Cachalot.Linq;
using Client.Core;
using Client.Tools;
using ProgressEventArgs = Cachalot.Linq.ProgressEventArgs;

Console.WriteLine(Logo);


// With a single argument (the csv file) it just reads the csv file and pack the lines
// Used mostly internally for performance tests
if (args.Length == 1)
{
    Console.WriteLine("In this mode you will only simulate reading a csv file. No data will be sent to the server");
    Console.WriteLine("SYNTAX: csvimport connection_string csv_file target_collection");

    var watch = new Stopwatch();

    watch.Start();
    var csvSchema = new CsvSchemaBuilder(args[0]).InferSchema(10000, false);

    watch.Stop();
    Console.WriteLine($"Schema inference took {watch.ElapsedMilliseconds} ms");


    watch.Restart();
    var count = 0;
    foreach (var _ in PackCsv(ReadLines(args[0]), "test", csvSchema))
    {
        count++;
        if (count % 50000 == 0) Console.Write(".");
    }

    watch.Stop();

    Console.WriteLine();
    Console.WriteLine($"Took {watch.ElapsedMilliseconds / 1000} seconds to parse {count} lines");

    return;
}

if (args.Length != 3)
{
    Console.WriteLine("SYNTAX: csvimport connection_string csv_file target_collection");

    return;
}


var connectionString = args[0];
var csvFile = args[1];
var collection = args[2];

IEnumerable<PackedObject> PackCsv(IEnumerable<string> lines, string collectionName, CsvSchema csvSchema)
{
    var primaryKey = 100;

    foreach (var line in lines)
    {
        yield return PackedObject.PackCsv(primaryKey, line, collectionName, csvSchema);

        primaryKey++;
    }
}

IEnumerable<string> ReadLines(string csvFileName)
{
    using var fileStream = File.OpenRead(csvFileName);
    using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 50000);

    var _ = reader.ReadLine(); // first one is the header => ignore it

    var line = reader.ReadLine();

    while (line != null)
    {
        yield return line;

        line = reader.ReadLine();
    }
}


try
{
    // passing "--internal" as connection string loads the csv into an in-process server
    using var connector = connectionString == "--internal" ? new() : new Connector(connectionString);

    var watch = new Stopwatch();


    var schema = connector.GetCollectionSchema(collection);

    const int linesToAnalyze = 10_000;

    Console.WriteLine($"Analyzing the first {linesToAnalyze} lines from the csv file...");

    watch.Start();
    
    // analyze the csv data
    var csvSchema = new CsvSchemaBuilder(csvFile).InferSchema(linesToAnalyze, false);
    watch.Stop();

    Console.WriteLine($"Done.Took {watch.ElapsedMilliseconds:F2} ms");

    Console.WriteLine();

    if (schema == null)
    {
        Console.WriteLine($"No schema defined for collection {collection}. It will be inferred from data");

        
        schema = csvSchema.ToCollectionSchema();
        connector.DeclareCollection(collection, schema);
    }
    else
    {
        Console.WriteLine("Collection already defined. Using existing schema");
    }

    Console.WriteLine();
    Console.WriteLine("Start feeding data...");


    connector.Progress += ConnectorProgress;

    watch.Restart();

    connector.FeedCsv(csvFile, collection, csvSchema);

    watch.Stop();

    Console.WriteLine();
    Console.WriteLine($"done in {watch.ElapsedMilliseconds / 1000.0:F2} seconds");


    if (connectionString == "--internal")
    {
        Console.WriteLine(
            $"Value pool: hit ratio={KeyValuePool.HitRatio * 100:F2}% complex_values={KeyValuePool.ComplexRatio * 100:F2}%");

        GC.Collect();

        var myself = Process.GetCurrentProcess();

        Console.WriteLine($"used memory={myself.WorkingSet64 / 1_000_000} MB");

        Console.WriteLine("press enter to stop");
        Console.ReadLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}

void ConnectorProgress(object? sender, ProgressEventArgs e)
{
    if (e.Type == ProgressEventArgs.ProgressNotification.Progress)
        Console.Write(".");
    else
        Console.WriteLine();
}