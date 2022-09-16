using Client.Core;
using ProtoBuf;
using System;

namespace Client.Messages
{
    /// <summary>
    ///     Remove an object from the cache
    /// </summary>
    [ProtoContract]
    public class RemoveRequest : DataRequest
    {
        public RemoveRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="primaryKeyValue"></param>
        public RemoveRequest(Type type, KeyValue primaryKeyValue)
            : base(DataAccessType.Write, type.Name)
        {
            PrimaryKey = primaryKeyValue;
        }

        public RemoveRequest(string typeName, KeyValue primaryKeyValue)
            : base(DataAccessType.Write, typeName)
        {
            PrimaryKey = primaryKeyValue;
        }


        [field: ProtoMember(1)] public KeyValue PrimaryKey { get; }
    }
}