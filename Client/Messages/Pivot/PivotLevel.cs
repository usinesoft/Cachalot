using Client.Core;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Client.Messages.Pivot
{
    [ProtoContract]
    public class PivotLevel
    {
        /// <summary>
        /// This one is null for root level
        /// </summary>
        [field: ProtoMember(1)] public NamedValue AxisValue { get; private set; }

        /// <summary>
        /// Aggregate for each value of the first axis
        /// </summary>
        [field: ProtoMember(2)] public Dictionary<KeyValue, PivotLevel> Children { get; } = new Dictionary<KeyValue, PivotLevel>();


        /// <summary>
        /// Aggregate value for each server-side visible value
        /// </summary>
        [field: ProtoMember(3)] public List<AggregatedValue> AggregatedValues { get; } = new List<AggregatedValue>();

        public PivotLevel(CollectionSchema schema, List<int> axisIndexes, List<int> valueIndexes)
        {
            _schema = schema;
            _axisIndexes = axisIndexes;
            _valueIndexes = valueIndexes;

            _valueNames = _schema.NamesOfScalarFields(valueIndexes.ToArray()).ToList();
            _axisNames = _schema.NamesOfScalarFields(axisIndexes.ToArray()).ToList();
        }


        /// <summary>
        /// This one is used for persistence only
        /// </summary>
        public PivotLevel()
        {
        }




        #region non persistent properties

        private CollectionSchema _schema;



        private List<int> _axisIndexes;
        private List<int> _valueIndexes;

        private List<string> _valueNames;
        private List<string> _axisNames;



        #endregion




        public void AggregateOneObject(PackedObject @object)
        {
            if (@object.Values.Length == 0)
                throw new NotSupportedException($"At least one property of type {@object.CollectionName} must be declared as [ServerSideVisible]");


            var valuesForPivot = new List<NamedValue>();// contains values and their names

            for (int i = 0; i < _valueIndexes.Count; i++)
            {
                var val = @object.Values[_valueIndexes[i]];

                if (double.IsNaN(val.NumericValue))
                    throw new NotSupportedException("Only numeric values can be used for pivot calculations");


                var name = _valueNames[i];

                valuesForPivot.Add(new NamedValue(val, name));

            }


            foreach (var value in valuesForPivot)
            {
                // first aggregate the root level
                var agg = AggregatedValues.FirstOrDefault(v => v.ColumnName == value.Name);
                if (agg == null)
                {
                    agg = new AggregatedValue { ColumnName = value.Name };
                    AggregatedValues.Add(agg);
                }

                agg.Count++;
                agg.Sum += (decimal)value.Value.NumericValue;

            }

            if (_axisIndexes.Count > 0)
            {

                var kv = @object[_axisIndexes[0]];
                var axisName = _axisNames[0];

                if (!Children.TryGetValue(kv, out var child))
                {
                    child = new PivotLevel(_schema, _axisIndexes.Skip(1).ToList(), _valueIndexes) { AxisValue = new NamedValue(kv, axisName) };
                    Children.Add(kv, child);
                }

                child.AggregateOneObject(@object);
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
            string padding = new string(' ', indentLevel * 4); // four spaces tabulation
            if (!Equals(AxisValue, null))
            {
                builder.Append(padding);
                builder.AppendLine($"{AxisValue.Name} = {AxisValue.Value.StringValue}");
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

                if (mySum != childrenSum)
                    throw new InvalidDataException($"incoherent sum vor column {value.ColumnName}");

                if (myCount != childrenCount)
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