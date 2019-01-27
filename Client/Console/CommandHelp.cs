using Client.Core;

namespace Client.Console
{
    /// <summary>
    ///     Display help message
    /// </summary>
    public class CommandHelp : CommandBase
    {
        public override bool TryExecute()
        {
            if (!CanExecute) return false;

            if (Params.Count == 0)
            {
                Logger.Write("Available commands:");
                Logger.Write("");
                Logger.Write("COUNT     : count the objects matching a specified query");
                Logger.Write("SELECT    : get a short description the objects matching a specified query");
                Logger.Write("DESC      : display information about the server process and data tables");
                Logger.Write("CONNECT   : connect to a server or a cluster of multiple servers");

                Logger.Write("EXIT      : guess what?");

                Logger.Write("DELETE    : remove all the objects matching a query");
                Logger.Write("TRUNCATE  : remove all the objects of a given type");
                Logger.Write("DUMP      : save all the database in a directory ");
                Logger.Write("IMPORT    : import data saved with the DUMP command (same number of nodes)");
                Logger.Write("RECREATE  : import data saved with the DUMP command (number of nodes changed)");
                Logger.Write(
                    "LAST      : display the last actions executed by the server");
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
                        Logger.Write(
                            "COUNT <table> WHERE <property1> <operator1> <value1>, <property2> <operator2> <value2>");
                        Logger.Write("Example: count trade where notional >= 0, Portfolio=CFEUR");
                        Logger.Write("table and property names are case insensitive, ',' stands for AND");
                        break;
                    case "SELECT":
                        Logger.Write(
                            "SELECT <table> WHERE <property1> <operator1> <value1>, <property2> <operator2> <value2> [INTO file.json]");
                        Logger.Write("Example: select trade where notional >= 0, Portfolio=SWAPEUR");
                        Logger.Write(" Table and property names are case insensitive, ',' stands for AND");
                        Logger.Write(" The where clause applies only on index keys");
                        Logger.Write(
                            " If INTO is used the data is saved as a json array in an external file");
                        break;
                    case "DELETE":
                        Logger.Write(
                            "DELETE <table> WHERE <property1> <operator1> <value1>, <property2> <operator2> <value2>");
                        Logger.Write("Example: delete trade where Portfolio=SWAPEUR");
                        Logger.Write(" Table and property names are case insensitive, ',' stands for AND");

                        break;
                    case "LAST":
                        Logger.Write("LAST <number> ");
                        Logger.Write("Exmple: last 10  = displays the most recent 10 actions on the server");
                        break;
                    case "TRUNCATE":
                        Logger.Write("TRUNCATE <table> ");
                        break;

                    case "DUMP":
                        Logger.Write("DUMP <existent directory> ");
                        Logger.Write(
                            "Saves all data in an external directory. The directory is usualy a network share ");
                        Logger.Write("It must be visible by ALL the servers");
                        Logger.Write("A subdirectory named yyyy-mm-dd will be created");
                        break;

                    case "IMPORT":
                        Logger.Write("IMPORT <existent directory> ");
                        Logger.Write("Import data from a dump. The directory is usualy a network share ");
                        Logger.Write("It must be visible by ALL the servers");
                        Logger.Write("This method can be used ONLY if the number of nodes has not changed");
                        Logger.Write("ALL DATA IS LOST");
                        break;
                    case "RECREATE":
                        Logger.Write("RECREATE <existent directory> ");
                        Logger.Write("Import data from a dump. The directory is usualy a network share ");
                        Logger.Write("It must be visible by ALL the servers");
                        Logger.Write(
                            "This method is slower than import but it can be used even if the numbar of nodes has changed");
                        Logger.Write("ALL DATA IS LOST");
                        break;
                    case "CONNECT":
                        Logger.Write("Connect to a single node or a Cachalot cluster");
                        Logger.Write("connect (no parameter): by default connect to localhost 4848");
                        Logger.Write("connect server port   : connect to a specific node");
                        Logger.Write("connect config.xml    : connect to a cluster described by a configuration file");

                        break;
                }
            }

            return true;
        }
    }
}