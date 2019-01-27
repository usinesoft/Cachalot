using System;
using Client.Interface;

namespace AdminConsole.Commands
{
    
    public class CommandReadOnly : CommandBase
    {
        
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute) return client;


            try
            {
                client.SetReadonlyMode(false);
            }
            catch (Exception)
            {
                // ignored
            }


            return client;
        }
    }
}