using Client.Interface;
using System;

namespace AdminConsole.Commands
{
    public class CommandReadOnly : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return client;


            try
            {
                client.SetReadonlyMode();
            }
            catch (Exception)
            {
                // ignored
            }


            return client;
        }
    }
}