using Client;
using Client.Core;
using Client.Interface;
using System;

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Get a detailed description of the available tables (types registered on the server)
    /// </summary>
    public class CommandDesc : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute)
                return null;


            Dbg.CheckThat(Params.Count <= 1);


            try
            {
                Profiler.IsActive = true;
                Profiler.Start("DESC");

                var serverInfo = client.GetClusterInformation();


                Profiler.End();


                if (Params.Count == 1)
                {
                    var tableName = Params[0];
                    foreach (var typeDescription in serverInfo.Schema)
                        if (tableName.ToUpper() == typeDescription.CollectionName.ToUpper())
                        {
                            LogTypeInfo(typeDescription);
                            break;
                        }
                }
                else
                {
                    foreach (var info in serverInfo.ServersStatus)
                    {
                        Logger.Write("");
                        Logger.Write("Server process");
                        Logger.Write("------------------------------------------------------------------");

                        //Logger.Write("    server name  = {0}", serverInfo.ServerProcessInfo.Host);
                        Logger.Write("      image type = {0} bits", info.Bits);
                        Logger.Write("      started at = {0}", info.StartTime);
                        Logger.Write("  active clients = {0}", info.ConnectedClients);
                        Logger.Write("         threads = {0}", info.Threads);
                        Logger.Write(" physical memory = {0} MB", info.WorkingSet / 1000000);
                        Logger.Write("  virtual memory = {0} MB", info.VirtualMemory / 1000000);
                        Logger.Write("software version = {0} ", info.SoftwareVersion);
                        Logger.Write("");
                    }

                    Logger.Write("Tables");


                    var header =
                        $"| {"Name",35} | {"Layout",10} |";

                    var line = new string('-', header.Length);

                    Logger.Write(line);
                    Logger.Write(header);
                    Logger.Write(line);

                    foreach (var typeDescription in serverInfo.Schema)
                    {
                        var compression = typeDescription.StorageLayout.ToString();


                        Logger.Write("| {0,35} | {1,10} |",
                            typeDescription.CollectionName,
                            compression
                        );
                    }

                    Logger.Write(line);
                }


                Logger.Write("The call took {0} milliseconds", Profiler.TotalTimeMilliseconds);
            }
            catch (Exception ex)
            {
                Profiler.End();
                Logger.WriteEror("Can not execute DESC : {0}", ex.Message);

                return null;
            }


            return client;
        }


        private static void LogTypeInfo(CollectionSchema desc)
        {
            Logger.Write("");
            Logger.Write("{0} ({1})", desc.CollectionName.ToUpper(), desc.CollectionName);
            var header = $"| {"property",45} | {"index type",13} |";

            var line = new string('-', header.Length);

            Logger.Write(line);


            Logger.Write(header);
            Logger.Write(line);


            foreach (var keyInfo in desc.ServerSide)
            {
                Logger.Write($"| {keyInfo.Name,45} | {keyInfo.IndexType,13} | ");
            }

            Logger.Write(line);
        }
    }
}