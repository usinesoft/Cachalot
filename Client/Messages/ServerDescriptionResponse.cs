using System;
using System.Collections.Generic;
using Client.ChannelInterface;
using Client.Core;
using ProtoBuf;

namespace Client.Messages;

[ProtoContract]
[Serializable]
public class ServerDescriptionResponse : Response
{
    [ProtoMember(3)] private readonly Dictionary<string, DataStoreInfo> _dataStoreInfoByFullName = new();

    [ProtoMember(2)] private readonly Dictionary<string, CollectionSchema> _knownTypesByFullName = new();

    [ProtoMember(1)] private ServerInfo _serverProcessInfo;

    public override ResponseType ResponseType => ResponseType.Data;

    public IDictionary<string, CollectionSchema> KnownTypesByFullName => _knownTypesByFullName;

    public IDictionary<string, DataStoreInfo> DataStoreInfoByFullName => _dataStoreInfoByFullName;

    public ServerInfo ServerProcessInfo
    {
        get => _serverProcessInfo;
        set => _serverProcessInfo = value;
    }


    /// <summary>
    ///     Not persistent on-purpose ; filled client-side
    /// </summary>
    public bool ConnectionError { get; set; }

    public void AddTypeDescription(CollectionSchema description)
    {
        KnownTypesByFullName.Add(description.CollectionName, description);
    }

    public void AddDataStoreInfo(DataStoreInfo info)
    {
        DataStoreInfoByFullName.Add(info.FullTypeName, info);
    }
}