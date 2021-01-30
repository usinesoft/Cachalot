using System;
using System.Collections.Generic;
using Client.Core;
using Client.Tools;

namespace Server.Queries
{
    /// <summary>
    /// Large data sets are fed by chunk. This singleton class allows to collect all the chunks from a feed session
    /// </summary>
    public class FeedSessionManager : IFeedSessionManager
    {
        readonly SafeDictionary<Guid, List<PackedObject>> _objectsBySession = new SafeDictionary<Guid, List<PackedObject>>(()=> new List<PackedObject>());

        public void AddToSession(Guid sessionId, IList<PackedObject> objects)
        {
            var list = _objectsBySession.GetOrCreate(sessionId);
            lock (list)
            {
                list.AddRange(objects);
            }
            
        }


        public IList<PackedObject> EndSession(Guid sessionId)
        {
            var all = _objectsBySession.TryRemove(sessionId);

            if (all == null)
            {
                throw new NotSupportedException("Ending a feed session that was not opened");
            }

            return all;
        }


    }
}