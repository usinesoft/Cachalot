using System;
using System.Collections.Generic;
using Client;
using Client.Core;
using Client.Interface;

namespace AdminConsole.Commands
{
    /// <summary>
    ///     Get only object description from the server. The concrete type of the object does not need to be available in order
    ///     to display the generic description
    /// </summary>
    public class CommandSelect : CommandBase
    {
        /// <summary>
        /// </summary>
        /// <returns></returns>
        internal override ICacheClient TryExecute(ICacheClient client)
        {
            if (!CanExecute)
                return client;

            Dbg.CheckThat(Params.Count >= 2);

            IList<CachedObject> listResult = null;

            try
            {
                Dbg.CheckThat(Query != null);

                Profiler.IsActive = true;
                Profiler.Start("SELECT");

                listResult = client.GetObjectDescriptions(Query);

                var dumpOk = true;

                // the third parameter(optional) is the output file name
                if (Params.Count == 3) dumpOk = Logger.DumpFile(Params[2]);

                if (dumpOk)
                {
                    Logger.Write("[");
                    for (var i = 0; i < listResult.Count; i++)
                    {
                        Logger.Write(listResult[i].AsJson());
                        if (i < listResult.Count - 1) Logger.Write(",");
                    }

                    Logger.Write("]");

                    if (Params.Count == 3) Logger.EndDump();
                }
            }
            catch (CacheException ex)
            {
                Logger.WriteEror("Can not execute GET : {0} {1}", ex.Message, ex.ServerMessage);
                return client;
            }
            catch (Exception ex)
            {
                Logger.WriteEror("Can not execute GET : {0}", ex.Message);
                return client;
            }
            finally
            {
                var profilerResult = Profiler.End();

                var count = 0;
                if (listResult != null)
                    count = listResult.Count;

                Logger.Write("Found {0} items. The call took {1} milliseconds", count,
                    profilerResult.TotalTimeMiliseconds);
            }


            return client;
        }
    }
}