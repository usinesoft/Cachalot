using System;
using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Client.Console
{
    /// <summary>
    ///     Count the items matching a specified query
    /// </summary>
    internal class CommandCount : CommandBase
    {
        private readonly Core.CacheClient _client;

        /// <summary>
        ///     Instantiate a command attached to a cache client
        /// </summary>
        /// <param name="client"></param>
        public CommandCount(Core.CacheClient client)
        {
            _client = client;
        }

        public override bool TryExecute()
        {
            if (!CanExecute)
                return false;

            Dbg.CheckThat(Params.Count == 2);

            var result = new KeyValuePair<bool, int>();
            try
            {
                Dbg.CheckThat(Query != null);

                Profiler.IsActive = true;
                Profiler.Start("COUNT");

                result = _client.EvalQuery(Query);
            }
            catch (CacheException ex)
            {
                Logger.WriteEror("Can not execute COUNT : {0} {1}", ex.Message, ex.ServerMessage);
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute COUNT : {0}", ex.Message);
                return false;
            }
            finally
            {
                var profilerResult = Profiler.End();


                Logger.Write("Found {0} items. The call took {1} miliseconds, data is complete = {2}", result.Value,
                    profilerResult.TotalTimeMiliseconds, result.Key);
            }


            return true;
        }
    }
}