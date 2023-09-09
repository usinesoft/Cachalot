using System.Collections.Generic;
using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using JetBrains.Annotations;
using ProtoBuf;

namespace Client.Messages.Pivot;

[ProtoContract]
public class PivotRequest : DataRequest
{
    /// <summary>
    ///     Mostly for serialization
    /// </summary>
    [UsedImplicitly]
    private PivotRequest() : base(DataAccessType.Read, string.Empty)
    {
    }

    internal PivotRequest(OrQuery query)
        : base(DataAccessType.Read, query.CollectionName)
    {
        Query = query;
    }


    public override RequestClass RequestClass => RequestClass.DataAccess;


    [field: ProtoMember(1)] public OrQuery Query { get; }

    [field: ProtoMember(2)] public List<int> AxisList { get; } = new();

    [field: ProtoMember(3)] public List<int> ValuesList { get; } = new();
}