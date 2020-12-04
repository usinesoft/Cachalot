using System;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Save all the database into a directory
    ///     Usage:
    ///     - dump directory (usually a network share as it needs to be visible by all servers)
    /// </summary>
    public class CommandDump : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return client;

            if (Params.Count != 1)
                Logger.CommandLogger.WriteError("please specify a dump directory");
            else
                try
                {
                    client.Dump(Params[0]);
                    Logger.Write("Database successfully saved");
                }
                catch (Exception e)
                {
                    Logger.WriteEror("error saving database:" + e.Message);
                }

            return client;
        }
    }
}