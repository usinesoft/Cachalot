

using System.Diagnostics;
using System.Text;
using Cachalot.Extensions;
using Cachalot.Linq;
using Client.Core;

Console.WriteLine("------------------------------");
Console.WriteLine(" CSV import tool for Cachalot");
Console.WriteLine("------------------------------");
if (args.Length == 1) // just simulate reading
{


    var watch = new Stopwatch();

    watch.Start();
    int count = 0;
    foreach (var packedObject in PackCsv(ReadLines(args[0]), "test"))
    {
        count++;
        if (count % 50000 == 0)
        {
            Console.Write(".");
        }

    }

    watch.Stop();
    
    Console.WriteLine();
    Console.WriteLine($"Took {watch.ElapsedMilliseconds} ms");

    return;
}

if(args.Length != 3)
{
    Console.WriteLine("SYNTAX: csvimport connection_string csv_file target_collection");

    return;
}


var connectionString = args[0];
var csvFile = args[1];
var collection = args[2];

IEnumerable<PackedObject> PackCsv(IEnumerable<string> lines, string collectionName, char separator = ',')
{
    int primaryKey = 100; 

    foreach (var line in lines)
    {
        yield return PackedObject.PackCsv(primaryKey, line, collectionName, separator);

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

    using var connector = connectionString == "--internal" ? new Connector(): new Connector(connectionString);

    var watch = new Stopwatch();
    watch.Start();
    connector.FeedCsvWithAutomaticSchema(csvFile, collection);
    watch.Stop();

    Console.WriteLine($"done in {watch.ElapsedMilliseconds / 1000}");
    if (connectionString == "--internal")
    {
        GC.Collect();

        
        var myself = Process.GetCurrentProcess();

        Console.WriteLine($"used memory={myself.WorkingSet64 / 1_000_000} MB");
        
        Console.WriteLine("press enter to stop");
        Console.ReadLine();
    }

    
}
catch(Exception ex)
{
    Console.WriteLine(ex.ToString());
}


