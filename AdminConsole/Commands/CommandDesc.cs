using System;
using Client;
using Client.Core;
using Client.Interface;
using Client.Messages;

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Get a detailed description of the available tables (types registered on the server)
    /// </summary>
    public class CommandDesc : CommandBase
    {
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute)
                return null;


            Dbg.CheckThat(Params.Count <= 1);


            try
            {
                Profiler.IsActive = true;
                Profiler.Start("DESC");

                var serverInfo = client.GetClusterInformation();


                var profilerResult = Profiler.End();


                if (Params.Count == 1)
                {
                    var tableName = Params[0];
                    foreach (var typeDescription in serverInfo.Schema)
                        if (tableName.ToUpper() == typeDescription.TypeName.ToUpper())
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
                        Logger.Write("----------------------------------------------------------------------------");

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
                    Logger.Write("-----------------------------------------------------------------------------");

                    var header =
                        $"| {"Name",15} | {"Zip",5} |";

                    Logger.Write(header);
                    Logger.Write("-----------------------------------------------------------------------------");

                    foreach (var typeDescription in serverInfo.Schema)
                    {
                        var compression = typeDescription.UseCompression.ToString();


                        Logger.Write("| {0,15} | {1,5} |",
                            typeDescription.TypeName,
                            compression
                        );
                    }

                    Logger.Write("-----------------------------------------------------------------------------");
                }


                Logger.Write("The call took {0} milliseconds", profilerResult.TotalTimeMiliseconds);
            }
            catch (Exception ex)
            {
                Profiler.End();
                Logger.WriteEror("Can not execute DESC : {0}", ex.Message);

                return null;
            }


            return client;
        }


        private static void LogTypeInfo(TypeDescription desc)
        {
            Logger.Write("");
            Logger.Write("{0} ({1})", desc.TypeName.ToUpper(), desc.FullTypeName);
            Logger.Write("------------------------------------------------------------------------------");
            var header = $"| {"property",25} | {"index type",13} | {"data type",9} | {"ordered",8} | {"full txt",8}|";

            Logger.Write(header);
            Logger.Write("------------------------------------------------------------------------------");

            Logger.Write(desc.PrimaryKeyField.ToString());

            foreach (var keyInfo in desc.UniqueKeyFields) Logger.Write(keyInfo.ToString());

            foreach (var keyInfo in desc.IndexFields) Logger.Write(keyInfo.ToString());

            foreach (var keyInfo in desc.ListFields) Logger.Write(keyInfo.ToString());

            Logger.Write("------------------------------------------------------------------------------");
        }
    }
}