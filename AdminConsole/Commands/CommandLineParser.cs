#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Client;
using Client.Core;
using Client.Interface;
using Client.Parsing;
using Client.Queries;

#endregion

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Create <see cref="CommandBase" /> derived classes from a string
    /// </summary>
    public class CommandLineParser
    {
        private readonly IDictionary<string, CollectionSchema> _knownTypes;

        public CommandLineParser(ClusterInformation desc)
        {
            _knownTypes = desc?.Schema.ToDictionary(t => t.CollectionName);
        }

        /// <summary>
        ///     get a type description by case insensitive type name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private CollectionSchema GetTypeDescriptionByName(string name)
        {
            foreach (var keyValuePair in _knownTypes)
                if (keyValuePair.Value.CollectionName.ToUpper() == name.ToUpper())
                    return keyValuePair.Value;


            throw new NotSupportedException($"can not find type description for type {name}");
        }

        /// <summary>
        ///     Parse the string and if it is valid build an instance of <see cref="CommandBase" />
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public CommandBase Parse(string command)
        {
            CommandBase result = null;

            var atoms = new List<string>();

            command = command.Trim();

            //DUMP target directory
            if (Parse(command, "^DUMP\\s*(.*)", atoms))
            {
                result = new CommandDump {CmdType = CommandType.Dump};

                if (atoms.Count == 1)
                {
                    var dir = atoms[0];
                    result.Params.Add(dir);

                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "DUMP command needs one parameter";
                }

                return result;
            }

            //RESTORE source_directory
            if (Parse(command, "^RESTORE\\s*(.*)", atoms))
            {
                result = new CommandRestore {CmdType = CommandType.Restore};

                if (atoms.Count == 1)
                {
                    var dir = atoms[0];
                    result.Params.Add(dir);

                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Restore command needs one parameter";
                }

                return result;
            }

            //IMPORT source_directory
            if (Parse(command, "^IMPORT\\s*(.*)", atoms))
            {
                result = new CommandImport {CmdType = CommandType.Import};

                if (atoms.Count == 1)
                {
                    var parts = atoms[0].Split();

                    if (parts.Length == 2)
                    {
                        result.Params.Add(parts[0]); // collection name
                        result.Params.Add(parts[1]); // json file
                        result.Success = true;
                    }

                    
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Import command needs two parameters";
                }

                return result;
            }

            //RECREATE source directory
            if (Parse(command, "^RECREATE\\s*(.*)", atoms))
            {
                result = new CommandRecreate {CmdType = CommandType.Recreate};

                if (atoms.Count == 1)
                {
                    var dir = atoms[0];
                    result.Params.Add(dir);

                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Recreate command needs one parameter";
                }

                return result;
            }

            //HELP command/HELP
            if (Parse(command, "^HELP\\s*(.*)", atoms))
            {
                result = new CommandHelp {CmdType = CommandType.Help};

                if (atoms.Count <= 1)
                {
                    if (atoms.Count == 1)
                    {
                        var cmd = atoms[0];
                        result.Params.Add(cmd);
                    }

                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "usage HELP or HELP <command>";
                }

                return result;
            }
            

            //EXIT
            if (Parse(command, "^EXIT", atoms))
            {
                result = new CommandBase {CmdType = CommandType.Exit, Success = true};
                result.Success = true;
                return result;
            }

            //STOP
            if (Parse(command, "^STOP", atoms))
            {
                return new CommandStop
                {
                    CmdType = CommandType.Stop,
                    Success = true
                };

                
            }

            //STOP
            if (Parse(command, "^DROP", atoms))
            {
                return new CommandDrop
                {
                    CmdType = CommandType.Drop,
                    Success = true
                };
            }

            if (Parse(command, "^READONLY", atoms))
            {
                return new CommandReadOnly
                {
                    CmdType = CommandType.ReadOnly,
                    Success = true
                };
            }

            if (Parse(command, "^READWRITE", atoms))
            {
                result = new CommandReadWrite
                {
                    CmdType = CommandType.ReadWrite,
                    Success = true
                };
            }

            if (command.Trim().ToLower().StartsWith("select"))
            {
                result = new CommandSelect {CmdType = CommandType.Select};

                ParseSelectOrCount(command, result);

                return result;

            }

            if (command.Trim().ToLower().StartsWith("count"))
            {
                result = new CommandCount {CmdType = CommandType.Count};

                ParseSelectOrCount(command, result);

                return result;

            }


            if (command.Trim().ToLower().StartsWith("last")) // shorthand for select most recent from activity table
            {
                result = new CommandSelect {CmdType = CommandType.Select};

                int take = 1;

                var parts = command.Split();
                if (parts.Length > 1) // optional parameter (number of items to display)
                {
                    try
                    {
                        take = int.Parse(parts[1]);
                       
                    }
                    catch (Exception e)
                    {
                        Logger.WriteEror($"Error in command {e.Message}");

                        result.Success = false;
                        return result;
                    }
                }

            
                var alias = "select from @ACTIVITY order by TIMESTAMP descending";

                ParseSelectOrCount(alias, result);

                
                if (result.Success) // managed to parse the SQL
                {
                    result.Query.Take = take;
                }

                return result;

            }

            if (command.Trim().ToLower().StartsWith("longest")) // shorthand for select queries that took longest time to execute server-side
            {
                result = new CommandSelect {CmdType = CommandType.Select};

                int take = 1;

                var parts = command.Split();
                if (parts.Length > 1) // optional parameter (number of items to display)
                {
                    try
                    {
                        take = int.Parse(parts[1]);
                       
                    }
                    catch (Exception e)
                    {
                        Logger.WriteEror($"Error in command {e.Message}");

                        result.Success = false;
                        return result;
                    }
                }

            
                var alias = "select from @ACTIVITY where type=SELECT order by ExecutionTimeInMicroseconds descending";

                ParseSelectOrCount(alias, result);

                
                if (result.Success) // managed to parse the SQL
                {
                    result.Query.Take = take;
                }

                return result;

            }
            
            
            
            if (Parse(command, "(^SEARCH)\\s+([a-zA-Z0-9\\.]+)\\s+(.+)", atoms))
            {
                result = new CommandSearch {CmdType = CommandType.Search};

                if (atoms.Count != 3)
                {
                    Logger.WriteEror("Invalid syntax for SEARCH. Type HELP SEARCH for more information");
                }
                else
                {
                    result.Params.Add(atoms[1]); //table
                    result.Params.Add(atoms[2]); //query

                    try
                    {
                        var typeDescription = GetTypeDescriptionByName(atoms[1]);
                        

                        var ftQuery = atoms[2];

                        result.Query = new OrQuery(typeDescription.CollectionName)
                        {
                            FullTextSearch = ftQuery, Take = 10
                        };
                        // limit the number of results when searching in admin console
                        result.Success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteEror("Invalid full-text search request. Type HELP SEARCH for more information",
                            ex.Message);
                    }
                }
            }
            else if (command.Trim().ToLower().StartsWith("delete"))
            {
                result = new CommandDelete {CmdType = CommandType.Delete};

                // create the correspondent select and use the select parser to parse the WHERE clause
                var alias = command.Replace("delete", "select", StringComparison.InvariantCultureIgnoreCase); 

                ParseSelectOrCount(alias, result);

                
                return result;
            }
            else if (Parse(command, "^TRUNCATE\\s+(.+)", atoms))
            {
                result = new CommandDelete {CmdType = CommandType.Delete};

                if (atoms.Count != 1)
                {
                    Logger.WriteEror("Invalid syntax for TRUNCATE. Type HELP TRUNCATE for more information");
                }
                else
                {
                    result.Params.Add(atoms[0]); //table

                    try
                    {
                        var typeDescription = GetTypeDescriptionByName(atoms[0]);
                        result.Query = new OrQuery(typeDescription.CollectionName);
                        result.Success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteEror("Invalid command: {0}", ex.Message);
                    }
                }
            }
           
            //DESC table /  DESC (the second form displays information on the server)
            else if (Parse(command, "^DESC\\s*(.*)", atoms))
            {
                result = new CommandDesc {CmdType = CommandType.Desc};

                if (atoms.Count <= 1)
                {
                    if (atoms.Count == 1)
                    {
                        var fileName = atoms[0];
                        result.Params.Add(fileName);
                    }

                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "DESC command has at most one parameter";
                }
            }
            //CONNECT server port or CONNECT config.xml
            else if (Parse(command, "^CONNECT\\s*(.*)", atoms))
            {
                result = new CommandConnect {CmdType = CommandType.Connect};

                if (atoms.Count == 1)
                {
                    
                    result.Params.Add(atoms[0].Trim());
                }

                result.Success = true;
            }

            if (result != null)
                return result;

            return new CommandBase();
        }

        private void ParseSelectOrCount(string command, CommandBase result)
        {
            try
            {
                var sqlParser = new Parser();
                var selectNode = sqlParser.ParseSql(command);
                var table = selectNode.Children.FirstOrDefault(n => n.Token == "from")?.Children.FirstOrDefault()
                    ?.Token;

                if (table == null)
                {
                    throw new NotSupportedException("Invalid syntax. No table name or missing FROM clause");
                }


                var schema = GetTypeDescriptionByName(table);

                if (schema == null)
                {
                    throw new NotSupportedException($"Unknown table {table}");
                }

                var query = selectNode.ToQuery(schema);

                var into = selectNode.Children.FirstOrDefault(n => n.Token == "into")?.Children.FirstOrDefault();
                if (into != null)
                {
                    result.Params.Add(into.Token);
                }

                result.Query = query;

                result.Success = true;
            }
            catch (Exception e)
            {
                Logger.WriteEror($"Error in command {e.Message}");

                result.Success = false;
            }
        }

        /// <summary>
        ///     Match a regular expression against a string. If successful return the captured strings
        /// </summary>
        /// <param name="toParse">string to parse</param>
        /// <param name="regex">regular expression as text</param>
        /// <param name="captures">list to be filled with captured strings</param>
        /// <returns>true if successful match</returns>
        private static bool Parse(string toParse, string regex, ICollection<string> captures)
        {
            var expression = new Regex(regex, RegexOptions.IgnoreCase);
            var match = expression.Match(toParse);
            if (!match.Success || match.Captures.Count != 1)
                return false;

            //group 0 is the full matched expression 
            //captures start at group 1
            Dbg.CheckThat(match.Groups.Count >= 1);
            for (var i = 1; i < match.Groups.Count; i++)
            {
                var value = match.Groups[i].Value;
                if (!string.IsNullOrEmpty(value))
                    captures.Add(value);
            }

            return true;
        }
    }
}