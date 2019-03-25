using Client.ChannelInterface;
using Client.Core;
using ProtoBuf;

namespace Client.Messages
{
    /// <summary>
    ///     Base class for the Get/Put/Remove requests. These request need to declare an <see cref="AccessType" />
    ///     and are attached to an unique data type used for serialization
    /// </summary>
    [ProtoContract]
    [ProtoInclude(604, typeof(DomainDeclarationRequest))]
    [ProtoInclude(605, typeof(EvalRequest))]
    [ProtoInclude(606, typeof(GetAvailableRequest))]
    [ProtoInclude(607, typeof(GetDescriptionRequest))]
    [ProtoInclude(608, typeof(GetRequest))]
    [ProtoInclude(609, typeof(PutRequest))]
    [ProtoInclude(610, typeof(RemoveManyRequest))]
    [ProtoInclude(611, typeof(RemoveRequest))]
    public abstract class DataRequest : Request
    {
        [ProtoMember(1)] private readonly DataAccessType _accessType;
        [ProtoMember(2)] private readonly string _fullTypeName;

        /// <summary>
        ///     Create an abstract data request (always attached to a data type and has a <see cref="DataAccessType" />)
        /// </summary>
        /// <param name="accessType">read-only or read-write access</param>
        /// <param name="fullTypeName"></param>
        protected DataRequest(DataAccessType accessType, string fullTypeName)
        {
            _accessType = accessType;
            _fullTypeName = fullTypeName;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        /// <summary>
        ///     read-only or read-write
        /// </summary>
        public DataAccessType AccessType => _accessType;

        /// <summary>
        ///     Full name as specified in the class <see cref="Type" />
        /// </summary>
        public virtual string FullTypeName => _fullTypeName;
    }
    
}