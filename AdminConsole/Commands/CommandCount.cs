using System;
using System.Collections.Generic;
using Client;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Count the items matching a specified query
    /// </summary>
    public class CommandCount : CommandBase
    {
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute)
                return client;

            Dbg.CheckThat(Params.Count == 2 || Params.Count == 1);

            var result = new KeyValuePair<bool, int>();
            try
            {
                Dbg.CheckThat(Query != null);

                Profiler.IsActive = true;
                Profiler.Start("COUNT");

                result = client.EvalQuery(Query);
            }
            catch (CacheException ex)
            {
                Logger.WriteEror("Can not execute COUNT : {0} {1}", ex.Message, ex.ServerMessage);
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute COUNT : {0}", ex.Message);
                return client;
            }
            finally
            {
                var profilerResult = Profiler.End();


                Logger.Write("Found {0} items. The call took {1:F4} miliseconds", result.Value,
                    profilerResult.TotalTimeMiliseconds);
            }


            return client;
        }
    }
}