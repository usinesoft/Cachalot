using System;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandDrop : CommandBase
    {
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute) return client;


            try
            {

                Console.WriteLine("This will delete ALL your data. Are you sure (y/n) ?");
                var answer = Console.ReadLine()?.ToLower().StartsWith("y");
                if (answer.HasValue && answer.Value)
                {
                    client.DropDatabase();
                }
                
            }
            catch (Exception)
            {
                // ignored
            }


            return client;
        }
    }
}