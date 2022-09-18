using Client.Interface;
using System;

namespace AdminConsole.Commands
{
    public class CommandReadWrite : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return client;


            try
            {
                client.SetReadonlyMode(true);
            }
            catch (Exception)
            {
                // ignored
            }


            return client;
        }
    }
}