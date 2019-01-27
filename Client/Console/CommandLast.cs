using System;
using Client.Core;
using Client.Messages;

namespace Client.Console
{
    /// <summary>
    ///     Get the last N entries from the server log
    /// </summary>
    internal class CommandLast : CommandBase
    {
        private readonly Core.CacheClient _client;

        /// <summary>
        /// </summary>
        /// <param name="client"></param>
        public CommandLast(Core.CacheClient client)
        {
            _client = client;
        }

        public override bool TryExecute()
        {
            if (!CanExecute)
                return false;


            Dbg.CheckThat(Params.Count == 1);


            try
            {
                Profiler.IsActive = true;
                Profiler.Start("LAST");

                var lines = int.Parse(Params[0]);

                var response = _client.GetServerLog(lines);

                var profilerResult = Profiler.End();

                if (response.Entries.Count > 0)
                {
                    Logger.Write("");
                    foreach (var entry in response.Entries) Logger.Write("{0}", entry.ToString());

                    Logger.Write("");
                    Logger.Write("Maximum access time was:");
                    Logger.Write(response.MaxLockEntry.ToString());
                }

                Logger.Write("The call took {0} miliseconds", profilerResult.TotalTimeMiliseconds);
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute LAST : {0}", ex.Message);
            }


            return true;
        }


        protected static void logTypeInfo(TypeDescription desc, bool detailed)
        {
            Logger.Write("");
            Logger.Write("{0} ({1})", desc.TypeName.ToUpper(), desc.FullTypeName);
            Logger.Write("------------------------------------");

            Logger.Write(desc.PrimaryKeyField.ToString());

            foreach (var keyInfo in desc.UniqueKeyFields) Logger.Write(keyInfo.ToString());

            foreach (var keyInfo in desc.IndexFields) Logger.Write(keyInfo.ToString());

            foreach (var keyInfo in desc.ListFields) Logger.Write(keyInfo.ToString());

            Logger.Write("");
        }
    }
}