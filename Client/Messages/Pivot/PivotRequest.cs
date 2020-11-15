using System.Collections.Generic;
using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages.Pivot
{
    [ProtoContract]
    public class PivotRequest : DataRequest
    {

        /// <summary>
        /// Mostly for serialization
        /// </summary>
        public PivotRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public PivotRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            Query = query;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;


        [field: ProtoMember(1)] public OrQuery Query { get; }
        
        [field: ProtoMember(2)] public  List<string> AxisList { get; }= new List<string>();
        
    }
}