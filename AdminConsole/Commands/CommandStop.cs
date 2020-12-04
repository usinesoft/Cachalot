using System;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandStop : CommandBase
    {
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute) return client;


            try
            {
                client.Stop(false);
            }
            catch (Exception)
            {
                // ignored
            }


            return client;
        }
    }
}