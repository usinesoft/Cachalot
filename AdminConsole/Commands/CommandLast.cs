using System;
using Client;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Get the last N entries from the server log
    /// </summary>
    public class CommandLast : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute)
                return client;


            Dbg.CheckThat(Params.Count == 1);


            try
            {
                Profiler.IsActive = true;
                Profiler.Start("LAST");

                var lines = int.Parse(Params[0]);

                var response = client.GetLog(lines);

                var profilerResult = Profiler.End();

                if (response.Entries.Count > 0)
                {
                    Logger.Write("");
                    foreach (var entry in response.Entries) Logger.Write(entry);

                    Logger.Write("");
                    Logger.Write("Maximum access time was:");
                }

                Logger.Write("The call took {0} milliseconds", profilerResult.TotalTimeMiliseconds);
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute LAST : {0}", ex.Message);
            }


            return client;
        }
    }
}