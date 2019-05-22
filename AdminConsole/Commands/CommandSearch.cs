using System;
using System.Collections.Generic;
using Client;
using Client.Core;
using Client.Interface;
using Client.Profiling;

namespace AdminConsole.Commands
{
    public class CommandSearch : CommandBase
    {
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute)
                return client;

            Dbg.CheckThat(Params.Count == 2);

            IList<CachedObject> listResult = null;

            try
            {
                Dbg.CheckThat(Query != null);

                Profiler.IsActive = true;
                Profiler.Start("SEARCH");

                listResult = client.GetObjectDescriptions(Query);

               
                Logger.Write("[");
                for (int i = 0; i < listResult.Count; i++)
                {
                    Logger.Write(listResult[i].AsJson());
                    if (i < listResult.Count - 1)
                    {
                        Logger.Write(",");
                    }
                }

                Logger.Write("]");

                return client;

            }
            catch (CacheException ex)
            {
                Logger.WriteEror("Can not execute SEARCH : {0} {1}", ex.Message, ex.ServerMessage);
                return client;
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute SEARCH : {0}", ex.Message);
                return client;
            }
            finally
            {
                ProfilingData profilerResult = Profiler.End();

                int count = 0;
                if (listResult != null)
                    count = listResult.Count;

                Logger.Write("Found {0} items. The call took {1} milliseconds", count,
                    profilerResult.TotalTimeMiliseconds);
            }


        }
    }
}