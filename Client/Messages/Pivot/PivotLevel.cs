using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Core;
using ProtoBuf;

namespace Client.Messages.Pivot
{
    [ProtoContract]
    public class PivotLevel
    {
        /// <summary>
        /// This one is null for root level
        /// </summary>
        [field: ProtoMember(1)]public KeyValue AxisValue { get; private set; }

        /// <summary>
        /// Aggregate for each value of the first axis
        /// </summary>
        [field: ProtoMember(2)] public Dictionary<KeyValue, PivotLevel> Children { get; } = new Dictionary<KeyValue, PivotLevel>();


        /// <summary>
        /// Aggregate value for each server-side visible value
        /// </summary>
        [field: ProtoMember(3)] public List<AggregatedValue> AggregatedValues { get; } = new List<AggregatedValue>();

        public void AggregateOneObject(PackedObject @object, params string[] axis)
        {
            if(@object.Values.Length == 0)
                throw new NotSupportedException($"At least one property of type {@object.CollectionName} must be declared as [ServerSideVisible]");

            //TODO explicitly specify the valuesto be aggregated
            var valuesForPivot = @object.Values
                .Union(@object.IndexKeys.Where(k => k.Type == KeyValue.OriginalType.SomeFloat));

            foreach (var value in valuesForPivot)
            {
                // first aggregate the root level
                var agg = AggregatedValues.FirstOrDefault(v => v.ColumnName == value.KeyName);
                if (agg == null)
                {
                    agg = new AggregatedValue {ColumnName = value.KeyName};
                    AggregatedValues.Add(agg);
                }

                agg.Count++;
                agg.Sum += (decimal) value.NumericValue;

            }

            if (axis.Length > 0)
            {
                var name = axis[0];
                var kv = @object[name];

                if (!Children.TryGetValue(kv, out var child))
                {
                    child = new PivotLevel {AxisValue = kv};
                    Children.Add(kv, child);
                }

                child.AggregateOneObject(@object, axis.Skip(1).ToArray());
            }
            
        }

        public override string ToString()
        {
            var result = new StringBuilder();

            ToString(result, 0);

            return result.ToString();
        }

        void ToString(StringBuilder builder, int indentLevel)
        {
            string padding = new string(' ', indentLevel*4); // four spaces tabulation
            if (!Equals(AxisValue, null))
            {
                builder.Append(padding);
                builder.AppendLine($"{AxisValue.KeyName} = {AxisValue.StringValue}");
            }

            foreach (var aggregatedValue in AggregatedValues)
            {
                builder.Append(padding);
                builder.AppendLine(aggregatedValue.ToString());
            }

            foreach (var child in Children)
            {
                child.Value.ToString(builder, indentLevel + 1);
            }
            
        }


        /// <summary>
        /// Merge two pivot result. Useful for multiple nodes clusters
        /// </summary>
        /// <param name="other"></param>
        public void MergeWith(PivotLevel other)
        {
            if (!Equals(AxisValue, other.AxisValue))
            {
                throw new NotSupportedException("Error in pivot merging. Mismatching axis");
            }

            foreach (var aggregatedValue in other.AggregatedValues)
            {
                var myValue = AggregatedValues.FirstOrDefault(v => v.ColumnName == aggregatedValue.ColumnName);
                if (myValue == null)
                {
                    AggregatedValues.Add(aggregatedValue);
                }
                else
                {
                    myValue.Count += aggregatedValue.Count;
                    myValue.Sum += aggregatedValue.Sum;
                }
            }

            foreach (var otherChild in other.Children)
            {
                if (!Children.TryGetValue(otherChild.Key, out var myChild))
                {
                    Children.Add(otherChild.Key, otherChild.Value);
                }
                else
                {
                    myChild.MergeWith(otherChild.Value);
                }
            }
        }

        /// <summary>
        /// Mostly for tests check that each level is the sum of the children
        /// </summary>
        /// <returns></returns>
        public void CheckPivot()
        {
            if (Children.Count == 0) // we reach the last level
                return;

            foreach (var value in AggregatedValues)
            {
                var mySum = value.Sum;
                var myCount = value.Count;

                var childrenSum = Children.Sum(c =>
                    c.Value.AggregatedValues.First(v => v.ColumnName == value.ColumnName).Sum);

                var childrenCount = Children.Sum(c =>
                    c.Value.AggregatedValues.First(v => v.ColumnName == value.ColumnName).Count);

                if(mySum != childrenSum)
                    throw new InvalidDataException($"incoherent sum vor column {value.ColumnName}");

                if(myCount != childrenCount)
                    throw new InvalidDataException($"incoherent count vor column {value.ColumnName}");

                // recursively check each level
                foreach (var pivotLevel in Children)
                {
                    pivotLevel.Value.CheckPivot();
                }
            }
        }
    }
}