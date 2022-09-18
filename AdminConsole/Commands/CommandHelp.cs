using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Display help message
    /// </summary>
    public class CommandHelp : CommandBase
    {
        private static void DisplayQueryHelp()
        {
            Logger.Write("");
            Logger.Write("Queries are expressed in SQL language. See the 'SQL and LINQ referential' for a complete description");
            Logger.Write("Property names, collection names and operators are NOT case sensitives.");
            Logger.Write("");
            Logger.Write("Examples:");
            Logger.Write("priceineuros <= 100 and Town = Paris");
            Logger.Write("ValueDate = 2018-09-01 AND IsDestroyed=0");
            Logger.Write("");
            Logger.Write("literal values syntax");
            Logger.Write(" Dates                 : as yyyy-mm-dd. Quotes optional");
            Logger.Write(" Strings               : quotes optional. They ARE case sensitive");
            Logger.Write(" Integers              : as is");
            Logger.Write(" Booleans              : as 0 or 1");
            Logger.Write(" Enumerations          : as integer value");
            Logger.Write(" Floating point values : with a . decimal separator");
        }

        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return null;

            if (Params.Count == 0)
            {
                Logger.Write("Available commands:");
                Logger.Write("");
                Logger.Write("COUNT     : count the objects matching a specified query");
                Logger.Write("SELECT    : get the objects matching a specified query as JSON");
                Logger.Write("DESC      : display information about the server process and data tables");
                Logger.Write("CONNECT   : connect to a server or a cluster of servers");

                Logger.Write("EXIT      : stop the console");


                Logger.Write("READONLY  : switch on the readonly mode");
                Logger.Write("READWRITE : switch off the readonly mode");

                Logger.Write("STOP      : stop all the nodes in the cluster");

                Logger.Write("DROP      : delete ALL DATA");

                Logger.Write("DELETE    : remove all the objects matching a query");
                Logger.Write("TRUNCATE  : remove all the objects of a given type");
                Logger.Write("DUMP      : save all the database in a directory ");
                Logger.Write("RESTORE   : restore data saved with the DUMP command (same number of nodes)");
                Logger.Write("RECREATE  : restore data saved with the DUMP command (number of nodes changed)");
                Logger.Write("IMPORT    : import data from an external json file");
                Logger.Write("LAST      : display information on the last actions executed by the server");
                Logger.Write("LONGEST   : display information on the queries that took the longest time");
                Logger.Write("SEARCH    : perform full-text search");
                Logger.Write("");
                Logger.Write("Type HELP <command> for detailed information");
            }
            else
            {
                switch (Params[0].ToUpper().Trim())
                {
                    case "DESC":
                        Logger.Write("DESC          : display information about the server process and data tables");
                        Logger.Write("DESC table    : display the list of the index fields for the specified table");
                        break;
                    case "COUNT":
                        Logger.Write("COUNT FROM <table>  - counts all the items from a table");
                        Logger.Write("COUNT FROM <table> WHERE <query>");
                        DisplayQueryHelp();
                        break;
                    case "SELECT":
                        Logger.Write("SELECT [DISTINCT] [<property1>, ...] FROM <table> WHERE <query> [ORDER BY <property>] [DESCENDING] [INTO file.json]");
                        DisplayQueryHelp();
                        Logger.Write("");
                        Logger.Write("If INTO, is used the data is saved as a json array in an external file");
                        break;
                    case "SEARCH":
                        Logger.Write("SEARCH <table> whatever you want");
                        Logger.Write("Perform full-text search on a given table");
                        break;
                    case "DELETE":
                        Logger.Write("DELETE <table> WHERE <query>");
                        DisplayQueryHelp();

                        break;
                    case "LAST":
                        Logger.Write("LAST <number> ");
                        Logger.Write("Example: last 10 displays the most recent 10 actions on the server");
                        Logger.Write("Information includes server execution time and execution plan (for queries)");
                        break;

                    case "LONGEST":
                        Logger.Write("LONGEST <number> ");
                        Logger.Write("Example: longest 10 displays the 10 queries that took the longest time server-side");
                        Logger.Write("Information includes server execution time and execution plan");
                        break;

                    case "TRUNCATE":
                        Logger.Write("TRUNCATE <table> ");
                        break;

                    case "DUMP":
                        Logger.Write("DUMP <existent directory> ");
                        Logger.Write(
                            "Saves all data in an external directory. The directory is usually a network share ");
                        Logger.Write("It must be visible by ALL the servers");
                        Logger.Write("A sub-directory named yyyy-mm-dd will be created for the current date");
                        break;


                    case "RESTORE":
                        Logger.Write("RESTORE <existent directory> ");
                        Logger.Write("Import data from a dump. The directory is usually a network share ");
                        Logger.Write("It must be visible by ALL the servers");
                        Logger.Write(
                            "If the path contains yyyy-mm-dd this backup will be used. Otherwise the last one is restored");
                        Logger.Write("This method can be used ONLY if the number of nodes has not changed");
                        Logger.Write("ALL PREVIOUS DATA IS LOST");
                        break;
                    case "IMPORT":
                        Logger.Write("IMPORT <collection name> <JSON file> ");
                        Logger.Write("Import data from an external json file");
                        Logger.Write("Objects are inserted or updated");
                        break;
                    case "RECREATE":
                        Logger.Write("RECREATE <existent directory> ");
                        Logger.Write("Import data from a dump. The directory must be accessible by the console client");
                        Logger.Write("The database must be completely empty. New one or after a DROP operation");
                        Logger.Write(
                            "This method is slower than RESTORE but it can be used even if the number of nodes has changed");
                        Logger.Write("ALL PREVIOUS DATA IS LOST");
                        break;
                    case "CONNECT":
                        Logger.Write("Connect to a single node or a Cachalot cluster");
                        Logger.Write($"connect (no parameter): by default connect to localhost {Constants.DefaultPort}");
                        Logger.Write("connect server port   : connect to a specific node");
                        Logger.Write("connect config.xml    : connect to a cluster described by a configuration file");

                        break;
                }
            }

            return client;
        }
    }
}