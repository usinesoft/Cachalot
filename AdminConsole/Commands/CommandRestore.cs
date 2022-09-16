using Client.Core;
using Client.Interface;
using System;

namespace AdminConsole.Commands
{
    public class CommandRestore : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return client;

            if (Params.Count != 1)
                Logger.CommandLogger.WriteError("please specify a directory containing database dump(s)");
            else
                try
                {
                    client.ImportDump(Params[0]);
                    Logger.Write("Database successfully imported");
                }
                catch (Exception e)
                {
                    Logger.WriteEror("error importing database data:" + e.Message);
                }

            return client;
        }
    }
}