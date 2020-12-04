using System;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandImport : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return client;

            if (Params.Count != 2)
                Logger.CommandLogger.WriteError("please specify a json file to import and a collection name");
            else
                try
                {
                    client.Import(Params[0], Params[1]);
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