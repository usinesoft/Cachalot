

using Cachalot.Extensions;
using Cachalot.Linq;

Console.WriteLine("CSV import tool for cachalot");

if(args.Length != 3)
{
    Console.WriteLine("SYNTAX: csvimport connection_string csv_file target_collection");

    return;
}


var connectionString = args[0];
var csvFile = args[1];
var collection = args[2];


try
{
    using var connector = new Connector(connectionString);

    connector.FeedCsvWithAutomaticSchema(csvFile, collection);

    Console.WriteLine("done");
}
catch(Exception ex)
{
    Console.WriteLine(ex.ToString());
}


