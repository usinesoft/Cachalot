using System;
using System.Collections.Generic;
using Client.ChannelInterface;
using Client.Core;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    [Serializable]
    public class ServerDescriptionResponse : Response
    {
        [ProtoMember(3)] private readonly Dictionary<string, DataStoreInfo> _dataStoreInfoByFullName =
            new Dictionary<string, DataStoreInfo>();

        [ProtoMember(2)] private readonly Dictionary<string, TypeDescription> _knownTypesByFullName =
            new Dictionary<string, TypeDescription>();

        [ProtoMember(1)] private ServerInfo _serverProcessInfo;

        public override ResponseType ResponseType => ResponseType.Data;

        public IDictionary<string, TypeDescription> KnownTypesByFullName => _knownTypesByFullName;

        public IDictionary<string, DataStoreInfo> DataStoreInfoByFullName => _dataStoreInfoByFullName;

        public ServerInfo ServerProcessInfo
        {
            get => _serverProcessInfo;
            set => _serverProcessInfo = value;
        }

        public void AddTypeDescription(TypeDescription description)
        {
            KnownTypesByFullName.Add(description.FullTypeName, description);
        }

        public void AddDataStoreInfo(DataStoreInfo info)
        {
            DataStoreInfoByFullName.Add(info.FullTypeName, info);
        }
    }
}