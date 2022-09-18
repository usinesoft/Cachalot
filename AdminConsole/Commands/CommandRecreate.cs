using Client.Core;
using Client.Interface;
using System;

namespace AdminConsole.Commands
{
    public class CommandRecreate : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return client;

            if (Params.Count != 1)
                Logger.CommandLogger.WriteError("please specify a directory containing database dump(s)");
            else
                try
                {
                    client.InitializeFromDump(Params[0]);
                    Logger.Write("Database successfully recreated");
                }
                catch (Exception e)
                {
                    Logger.WriteEror("error recreating database data:" + e.Message);
                }

            return client;
        }
    }
}