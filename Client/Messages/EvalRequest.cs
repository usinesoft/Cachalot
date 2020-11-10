using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Client.ChannelInterface;
using Client.Core;
using Client.Queries;
using ProtoBuf;

namespace Client.Messages
{
    [ProtoContract]
    public class EvalRequest : DataRequest
    {
        public EvalRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public EvalRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            Query = query;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        [field: ProtoMember(1)] public OrQuery Query { get; }
    }

    [ProtoContract]
    public class PivotRequest : DataRequest
    {
        public PivotRequest() : base(DataAccessType.Read, string.Empty)
        {
        }

        public PivotRequest(OrQuery query)
            : base(DataAccessType.Read, query.TypeName)
        {
            Query = query;
        }

        public override RequestClass RequestClass => RequestClass.DataAccess;

        public void DefineAxis<T>(params Expression<Func<T, object>>[] propertySelectors)
        {
            foreach (var propertySelector in propertySelectors)
            {
                var name = ExpressionTreeHelper.PropertyName(propertySelector);
                _axisList.Add(name);
            }
        }

        public void DefineAggregationColumns<T>(params Expression<Func<T, object>>[] propertySelectors)
        {
            foreach (var propertySelector in propertySelectors)
            {
                var name = ExpressionTreeHelper.PropertyName(propertySelector);
                _aggregationColumns.Add(name);
            }
        }

        [field: ProtoMember(1)] public OrQuery Query { get; }
        
        [ProtoMember(2)] private List<string> _axisList = new List<string>();
        [ProtoMember(3)] private List<string> _aggregationColumns = new List<string>();
    }


    [ProtoContract]
    public class AggregatedValue
    {
        [field: ProtoMember(1)]public string ColumnName { get; set; }

        [field: ProtoMember(2)]public int Count{get; set; }
        
        [field: ProtoMember(3)]public decimal Sum{get; set; }
    }

    [ProtoContract]
    public class PivotLevel
    {
        /// <summary>
        /// This one is null for root level
        /// </summary>
        [field: ProtoMember(1)]private KeyValue AxisValue { get; set; }

        [field: ProtoMember(2)] public List<PivotLevel> Children { get; } = new List<PivotLevel>();
        
        [field: ProtoMember(3)] public List<AggregatedValue> AggregatedValues { get; } = new List<AggregatedValue>();

        public void AddValue(ServerSideValue value, params string[] axis)
        {

            var agg = AggregatedValues.FirstOrDefault(v => v.ColumnName == value.Name);
            if (agg == null)
            {
                agg = new AggregatedValue {ColumnName = value.Name};
                AggregatedValues.Add(agg);
            }

            agg.Count++;
            agg.Sum += value.Value;


        }
    }

    public class PivotResponse
    {

    }
}