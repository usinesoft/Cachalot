using System;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandReadWrite : CommandBase
    {

        internal override ICacheClient TryExecute(ICacheClient client)
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