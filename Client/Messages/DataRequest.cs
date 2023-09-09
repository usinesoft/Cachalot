using System;
using Client.ChannelInterface;
using Client.Core;
using Client.Messages.Pivot;
using ProtoBuf;

namespace Client.Messages;

/// <summary>
///     Base class for the Get/Put/Remove requests. These request need to declare an <see cref="AccessType" />
///     and are attached to an unique data type used for serialization
/// </summary>
[ProtoContract]
[ProtoInclude(604, typeof(DomainDeclarationRequest))]
[ProtoInclude(605, typeof(EvalRequest))]
[ProtoInclude(608, typeof(GetRequest))]
[ProtoInclude(609, typeof(PutRequest))]
[ProtoInclude(610, typeof(RemoveManyRequest))]
[ProtoInclude(611, typeof(RemoveRequest))]
[ProtoInclude(612, typeof(EvictionSetupRequest))]
[ProtoInclude(613, typeof(PivotRequest))]
public abstract class DataRequest : Request
{
    /// <summary>
    ///     Create an abstract data request (always attached to a data type and has a <see cref="DataAccessType" />)
    /// </summary>
    /// <param name="accessType">read-only or read-write access</param>
    /// <param name="collectionName"></param>
    /// <param name="sessionId">optional session id</param>
    protected DataRequest(DataAccessType accessType, string collectionName, Guid sessionId = default)
    {
        AccessType = accessType;
        CollectionName = collectionName;
        SessionId = sessionId;
    }

    public override RequestClass RequestClass => RequestClass.DataAccess;

    /// <summary>
    ///     read-only or read-write
    /// </summary>
    [field: ProtoMember(1)]
    public DataAccessType AccessType { get; }

    /// <summary>
    ///     Full name as specified in the class <see cref="Type" />
    /// </summary>
    [field: ProtoMember(2)]
    public virtual string CollectionName { get; }

    [field: ProtoMember(3)] public Guid SessionId { get; set; }
}