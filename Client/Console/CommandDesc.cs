using System;
using Client.Core;
using Client.Messages;

namespace Client.Console
{
    /// <summary>
    ///     Get a detailed description of the available tables (types registered on the server)
    /// </summary>
    internal class CommandDesc : CommandBase
    {
        private readonly Core.CacheClient _client;

        /// <summary>
        /// </summary>
        /// <param name="client"></param>
        public CommandDesc(Core.CacheClient client)
        {
            _client = client;
        }


        public override bool TryExecute()
        {
            if (!CanExecute)
                return false;


            Dbg.CheckThat(Params.Count <= 1);


            try
            {
                Profiler.IsActive = true;
                Profiler.Start("DESC");

                var serverInfo = _client.GetServerDescription();


                var profilerResult = Profiler.End();


                if (Params.Count == 1)
                {
                    var tableName = Params[0];
                    foreach (var keyValuePair in serverInfo.KnownTypesByFullName)
                        if (tableName.ToUpper() == keyValuePair.Value.TypeName.ToUpper())
                        {
                            LogTypeInfo(keyValuePair.Value, false);
                            break;
                        }
                }
                else
                {
                    Logger.Write("");
                    Logger.Write("Server process");
                    Logger.Write("----------------------------------------------------------------------------");

                    //Logger.Write("    server name  = {0}", serverInfo.ServerProcessInfo.Host);
                    Logger.Write("      image type = {0} bits", serverInfo.ServerProcessInfo.Bits);
                    Logger.Write("      started at = {0}", serverInfo.ServerProcessInfo.StartTime);
                    Logger.Write("  active clients = {0}", serverInfo.ServerProcessInfo.ConnectedClients);
                    Logger.Write("         threads = {0}", serverInfo.ServerProcessInfo.Threads);
                    Logger.Write(" physical memory = {0} MB", serverInfo.ServerProcessInfo.WorkingSet / 1000000);
                    Logger.Write("  virtual memory = {0} MB", serverInfo.ServerProcessInfo.VirtualMemory / 1000000);
                    Logger.Write("software version = {0} ", serverInfo.ServerProcessInfo.SoftwareVersion);
                    Logger.Write("");
                    Logger.Write("Tables");
                    Logger.Write("-----------------------------------------------------------------------------");


                    var header = string.Format("| {0,15} | {1,9} | {2,5} | {3,6} | {4, 16} | {5, 7} |", "Name",
                        "Count",
                        "Zip", "Hits", "Eviction", "Serial.");

                    Logger.Write(header);
                    Logger.Write("-----------------------------------------------------------------------------");

                    foreach (var keyValuePair in serverInfo.DataStoreInfoByFullName)
                    {
                        var compression = keyValuePair.Value.DataCompression.ToString();
                        var hitCount = keyValuePair.Value.ReadCount == 0
                            ? 0
                            : keyValuePair.Value.HitCount * 100 / keyValuePair.Value.ReadCount;

                        var serialization = ".Net";

                        Logger.Write("| {0,15} | {1,9} | {2,5} | {3,6}%| {4, 16} | {5, 7} |",
                            keyValuePair.Value.TableName,
                            keyValuePair.Value.Count,
                            compression,
                            hitCount,
                            keyValuePair.Value.EvictionPolicyDescription,
                            serialization);
                    }

                    Logger.Write("-----------------------------------------------------------------------------");
                }


                Logger.Write("The call took {0} milliseconds", profilerResult.TotalTimeMiliseconds);
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute DESC : {0}", ex.Message);
                return false;
            }


            return true;
        }


        protected static void LogTypeInfo(TypeDescription desc, bool detailed)
        {
            Logger.Write("");
            Logger.Write("{0} ({1})", desc.TypeName.ToUpper(), desc.FullTypeName);
            Logger.Write("-------------------------------------------------------------------------");
            var header = string.Format("| {0,25} | {1,15} | {2,8} | {3,8} | {4,8} |", "property", "index type",
                "data type",
                "ordered",
                "full text");

            Logger.Write(header);
            Logger.Write("-------------------------------------------------------------------------");

            Logger.Write(desc.PrimaryKeyField.ToString());

            foreach (var keyInfo in desc.UniqueKeyFields) Logger.Write(keyInfo.ToString());

            foreach (var keyInfo in desc.IndexFields) Logger.Write(keyInfo.ToString());

            foreach (var keyInfo in desc.ListFields) Logger.Write(keyInfo.ToString());

            Logger.Write("-------------------------------------------------------------------------");
        }
    }
}