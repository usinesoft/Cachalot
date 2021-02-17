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
        internal override IDataClient TryExecute(IDataClient client)
        {
            if (!CanExecute)
                return client;

            Dbg.CheckThat(Params.Count == 2 || Params.Count == 1);

            var result = new Tuple<bool, int>(false, 0);
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
                Profiler.End();

                Logger.Write("Found {0} items. The call took {1:F4} miliseconds", result.Item2,
                    Profiler.TotalTimeMilliseconds);
            }


            return client;
        }
    }
}