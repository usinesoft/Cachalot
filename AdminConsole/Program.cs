using System;
using System.Linq;
using System.Runtime.InteropServices;
using AdminConsole.AutoCompleteUtils;
using AdminConsole.Commands;
using AdminConsole.ConsoleUtils;
using Channel;
using Client.Core;
using Client.Interface;

namespace AdminConsole;

internal class Program
{
    private static void Main(string[] args)
    {
        // by default connect to the local server
        var server = "localhost";
        if (args.Length > 0)
            server = args[0];

        // default port
        var port = Constants.DefaultPort;

        if (args.Length > 1)
            if (int.TryParse(args[1], out var customPort))
                port = customPort;


        var channel = new TcpClientChannel(new(1, 1, server, port));
        IDataClient client = new DataClient { Channel = channel };


        Logger.CommandLogger = new ConsoleLogger();

        Logger.Write("connecting to server {0} on port {1}", server, port);

        try
        {
            ClusterInformation serverDesc = null;

            try
            {
                serverDesc = client.GetClusterInformation();
            }
            catch (Exception)
            {
                Logger.WriteEror("Not connected to server: Only CONNECT and HELP commands are available");
            }


            //Profiler.Logger = new ProfileOutput(Console.Out);
            var parser = new CommandLineParser(serverDesc);
            Logger.Write("Type HELP for command list. Advanced auto-completion is also available ...");


            ConsoleExt.SetLine(">>");


            var running = true;
            var cyclingAutoComplete = new CyclingAutoComplete { KnownTypes = serverDesc?.Schema.ToList() };
            while (running)
            {
                var result = ConsoleExt.ReadKey();
                switch (result.Key)
                {
                    case ConsoleKey.Enter:
                        var line = result.LineBeforeKeyPress.Line;
                        line = line.TrimStart('>');
                        var cmd = parser.Parse(line);
                        if (cmd.Success)
                        {
                            if (cmd.CmdType != CommandType.Exit)
                            {
                                var title = "";
                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                {
                                    title = Console.Title;
                                    Console.Title = " WORKING...";
                                }

                                client = cmd.TryExecute(client);

                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Console.Title = title;

                                // table definitions may have changed by command execution (connect, import, recreate) 
                                // or by external action

                                try
                                {
                                    serverDesc = client.GetClusterInformation();
                                    // if the connection changed reinitialize the autocomplete with the new schema
                                    cyclingAutoComplete = new() { KnownTypes = serverDesc?.Schema.ToList() };
                                    parser = new(serverDesc);
                                }
                                catch (CacheException ex)
                                {
                                    Logger.WriteEror(
                                        $"Error while connecting: {ex.Message}");
                                }
                                catch (Exception)
                                {
                                    Logger.WriteEror(
                                        "Not connected to server: Only CONNECT and HELP commands are available");
                                }
                            }
                            else
                            {
                                running = false;
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(line)) Logger.WriteEror("invalid command");
                        }

                        ConsoleExt.SetLine(">>");

                        break;
                    case ConsoleKey.Tab:
                        var shiftPressed = (result.Modifiers & ConsoleModifiers.Shift) != 0;
                        var cyclingDirection =
                            shiftPressed ? CyclingDirections.Backward : CyclingDirections.Forward;

                        var lineBefore = result.LineBeforeKeyPress.LineBeforeCursor.TrimStart('>');
                        var lineAfter = result.LineBeforeKeyPress.LineAfterCursor;

                        var autoCompletedLine =
                            cyclingAutoComplete.AutoComplete(lineBefore, lineAfter, cyclingDirection);

                        ConsoleExt.SetLine(">>" + autoCompletedLine);
                        break;
                }
            }

            Logger.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
        }
    }
}