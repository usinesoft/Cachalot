using System;
using System.Collections.Generic;
using Client.ChannelInterface;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    /// 
    /// </summary>
    [ProtoContract]
    public class LockRequest : Request
    {
        public override RequestClass RequestClass => RequestClass.DataAccess;

        /// <summary>
        /// If false red-only mode
        /// </summary>
        [ProtoMember(1)] public bool WriteMode { get; set; }

        /// <summary>
        /// If false lock
        /// </summary>
        [ProtoMember(2)] public bool Unlock { get; set; }
        

        /// <summary>
        /// The maximum time it waits to acquire a lock (never infinite to avoid deadlocks)
        /// </summary>
        [ProtoMember(3)] public int WaitDelayInMilliseconds { get; set; }

        [ProtoMember(4)] public Guid SessionId { get; set; }

        [ProtoMember(5)] public IList<string> CollectionsToLock { get; set; } = new List<string>();

        
    }
}