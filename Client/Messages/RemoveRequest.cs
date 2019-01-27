using System;
using Client.Core;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Remove an object from the cache
    /// </summary>
    [ProtoContract]
    public class RemoveRequest : DataRequest
    {
        [ProtoMember(1)] private readonly KeyValue _primaryKey;

        public RemoveRequest() : base(DataAccessType.Write, string.Empty)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="primaryKeyValue"></param>
        public RemoveRequest(Type type, KeyValue primaryKeyValue)
            : base(DataAccessType.Write, type.FullName)
        {
            _primaryKey = primaryKeyValue;
        }

        public RemoveRequest(string typeName, KeyValue primaryKeyValue)
            : base(DataAccessType.Write, typeName)
        {
            _primaryKey = primaryKeyValue;
        }


        public KeyValue PrimaryKey => _primaryKey;
    }
}