using System;
using System.Collections.Generic;
using Client.Core;

namespace Server.Queries;

public interface IFeedSessionManager
{
    void AddToSession(Guid sessionId, IList<PackedObject> objects);
    IList<PackedObject> EndSession(Guid sessionId);
}