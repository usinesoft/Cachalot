using System.Collections.Generic;
using Client.ChannelInterface;
using Client.Core;
using Client.Interface;
using Client.Messages;
using Client.Queries;
using JetBrains.Annotations;
using ProtoBuf;

namespace Cachalot.Linq
{
    [ProtoContract]
    public class PivotRequest : DataRequest
    {
        private readonly IDataClient _client;

        /// <summary>
        /// Mostly for serialization
        /// </summary>
        [UsedImplicitly]
        private PivotRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        internal  PivotRequest(OrQuery query, IDataClient client)
            : base(DataAccessType.Read, query.CollectionName)
        {
            _client = client;
            Query = query;
        }



        public override RequestClass RequestClass => RequestClass.DataAccess;


        [field: ProtoMember(1)] public OrQuery Query { get; }
        
        [field: ProtoMember(2)] public  List<int> AxisList { get; }= new List<int>();

        [field: ProtoMember(3)] public  List<int> ValuesList { get; }= new List<int>();


        
    }
}