using System;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandImport : CommandBase
    {
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute) return client;

            if (Params.Count != 1)
                Logger.CommandLogger.WriteError("please specify a json file to import");
            else
                try
                {
                    client.Import(Params[0]);
                    Logger.Write("Data successfully imported");
                }
                catch (Exception e)
                {
                    Logger.WriteEror("error importing  data:" + e.Message);
                }

            return client;
        }
    }
}