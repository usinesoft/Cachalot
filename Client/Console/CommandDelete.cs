using System;
using Client.Core;
using Client.Interface;

namespace Client.Console
{
    internal class CommandDelete : CommandBase
    {
        private readonly Core.CacheClient _client;

        /// <summary>
        ///     Instantiate a command attached to a cache client
        /// </summary>
        /// <param name="client"></param>
        public CommandDelete(Core.CacheClient client)
        {
            _client = client;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override bool TryExecute()
        {
            if (!CanExecute)
                return false;

            Dbg.CheckThat(Params.Count >= 1);

            var deletedItems = 0;

            try
            {
                Dbg.CheckThat(Query != null);

                Profiler.IsActive = true;
                Profiler.Start("DELETE");

                deletedItems = _client.RemoveMany(Query);
            }
            catch (CacheException ex)
            {
                Logger.WriteEror("Can not execute GET : {0} {1}", ex.Message, ex.ServerMessage);
                return false;
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute GET : {0}", ex.Message);
                return false;
            }
            finally
            {
                var profilerResult = Profiler.End();

                Logger.Write("Deleted {0} items. The call took {1} miliseconds", deletedItems,
                    profilerResult.TotalTimeMiliseconds);
            }


            return true;
        }
    }
}