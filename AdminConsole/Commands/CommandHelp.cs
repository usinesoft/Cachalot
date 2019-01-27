using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    /// <summary>
    /// Display help message
    /// </summary>
    public class CommandHelp : CommandBase
    {

        private static void DisplayQueryHelp()
        {
            Logger.Write("Queries are expressed in a SQL-like language.");
            Logger.Write("Index names are NOT query sensitives.");
            Logger.Write("");
            Logger.Write("Examples:");
            Logger.Write("priceineuros <= 100., Town = Paris");
            Logger.Write("ValueDate = 2018-09-01 , IsDestroyed=0");
            Logger.Write("");
            Logger.Write("Comma symbol stands for AND");
            Logger.Write("");
            Logger.Write("Dates                 as yyyy-mm-dd. No quotes");
            Logger.Write("Strings               as is. No quotes. They ARE case sensitive");
            Logger.Write("Integers              as is");
            Logger.Write("Booleans              as 0 or 1");
            Logger.Write("Enumerations          as integer value");
            Logger.Write("Floating point values with a mandatory '.' decimal separator like 200. or 200.0");

        }

        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute)
            {
                return null;
            }

            if (Params.Count == 0)
            {
                Logger.Write("Available commands:");
                Logger.Write("");
                Logger.Write("COUNT     : count the objects matching a specified query");
                Logger.Write("SELECT    : get the objects matching a specified query as JSON");
                Logger.Write("DESC      : display information about the server process and data tables");
                Logger.Write("CONNECT   : connect to a server or a cluster of servers");

                Logger.Write("EXIT      : guess what?");
                

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
                        Logger.Write("COUNT <table>  - counts all the items from a table");
                        Logger.Write("COUNT <table> WHERE <query>");
                        DisplayQueryHelp();
                        break;
                    case "SELECT":
                        Logger.Write("SELECT <table> WHERE <query> [INTO file.json]");
                        DisplayQueryHelp();
                        Logger.Write( "If INTO is used the data is saved as a json array in an external file");
                        break;
                    case "DELETE":
                        Logger.Write( "DELETE <table> WHERE <query>");
                        DisplayQueryHelp();
                        
                        break;
                    case "LAST":
                        Logger.Write("LAST <number> ");
                        Logger.Write("Exmple: last 10  = displays the most recent 10 actions on the server");
                        Logger.Write("Information includes server execution time and execution plan");
                        break;
                    case "TRUNCATE":
                        Logger.Write("TRUNCATE <table> ");
                        break;

                    case "DUMP":
                        Logger.Write("DUMP <existent directory> ");
                        Logger.Write("Saves all data in an external directory. The directory is usualy a network share ");
                        Logger.Write("It must be visible by ALL the servers");
                        Logger.Write("A subdirectory named yyyy-mm-dd will be created for the current date");
                        break;


                    case "RESTORE":
                        Logger.Write("RESTORE <existent directory> ");
                        Logger.Write("Import data from a dump. The directory is usualy a network share ");
                        Logger.Write("It must be visible by ALL the servers");
                        Logger.Write("If the path contains yyyy-mm-dd this backup will be used. Otherwise the last one is restored");
                        Logger.Write("This method can be used ONLY if the number of nodes has not changed");
                        Logger.Write("ALL PREVIOUS DATA IS LOST");
                        break;
                    case "IMPORT":
                        Logger.Write("IMPORT <JSON file> ");
                        Logger.Write("Import data from an external json file");
                        Logger.Write("Objects are inserted or updated");                        
                        break;
                    case "RECREATE":
                        Logger.Write("RECREATE <existent directory> ");
                        Logger.Write("Import data from a dump. The directory must be accessible by the console client");                        
                        Logger.Write("The database must be completely empty. New one or after a DROP operation");                        
                        Logger.Write("This method is slower than RESTORE but it can be used even if the number of nodes has changed");
                        Logger.Write("ALL PREVIOUS DATA IS LOST");
                        break;
                    case "CONNECT":
                        Logger.Write("Connect to a single node or a Cachalot cluster");
                        Logger.Write("connect (no parameter): by default connect to localhost 4848");
                        Logger.Write("connect server port   : connect to a specific node");
                        Logger.Write("connect config.xml    : connect to a cluster described by a configuration file");
                        
                        break;
                }
            }

            return client;
        }
    }
}