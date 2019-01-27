using System;
using System.Collections.Generic;
using Client.Core;
using Client.Interface;

namespace Client.Console
{
    /// <summary>
    ///     Get only object description from the server. The concrete type of the object does not need to be available in order
    ///     to display the generic description
    /// </summary>
    internal class CommandSelect : CommandBase
    {
        private readonly Core.CacheClient _client;

        /// <summary>
        ///     Instantiate a command attached to a cache client
        /// </summary>
        /// <param name="client"></param>
        public CommandSelect(Core.CacheClient client)
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

            Dbg.CheckThat(Params.Count >= 2);

            IList<CachedObject> listResult = null;

            try
            {
                Dbg.CheckThat(Query != null);

                Profiler.IsActive = true;
                Profiler.Start("SELECT");

                listResult = _client.GetObjectDescriptions(Query);

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

                var count = 0;
                if (listResult != null)
                    count = listResult.Count;

                Logger.Write("Found {0} items. The call took {1} miliseconds", count,
                    profilerResult.TotalTimeMiliseconds);
            }


            return true;
        }
    }
}