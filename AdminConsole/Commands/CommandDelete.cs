using System;
using Client;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    public class CommandDelete : CommandBase
    {
        /// <summary>
        /// </summary>
        /// <returns></returns>
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute)
                return null;

            Dbg.CheckThat(Params.Count >= 1);

            var deletedItems = 0;

            try
            {
                Dbg.CheckThat(Query != null);

                Profiler.IsActive = true;
                Profiler.Start("DELETE");

                deletedItems = client.RemoveMany(Query);
            }
            catch (CacheException ex)
            {
                Logger.WriteEror("Can not execute GET : {0} {1}", ex.Message, ex.ServerMessage);
                return null;
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute GET : {0}", ex.Message);
                return null;
            }
            finally
            {
                Profiler.End();

                Logger.Write("Deleted {0} items. The call took {1} miliseconds", deletedItems,
                    Profiler.TotalTimeMilliseconds);
            }


            return client;
        }
    }
}