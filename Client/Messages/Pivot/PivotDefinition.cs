using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Client.Core;
using Client.Interface;
using Client.Queries;

namespace Client.Messages.Pivot;

/// <summary>
///     A pivot definition has three components. A filter expressed as a query, an ordered list of aggregation axis, a list
///     of properties to aggregate
/// </summary>
/// <typeparam name="T"></typeparam>
public class PivotDefinition<T>
{
    private readonly IDataClient _client;

    private readonly CollectionSchema _schema;


    internal PivotDefinition(OrQuery query, IDataClient client, CollectionSchema schema)
    {
        _client = client;
        _schema = schema;
        Query = query;
    }


    private OrQuery Query { get; }

    private List<int> AxisList { get; } = new();

    private List<int> ValuesList { get; } = new();


    /// <summary>
    ///     Fluent interface for initializing pivot requests
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="axis"></param>
    /// <returns></returns>
    public PivotDefinition<T> OnAxis(params Expression<Func<T, object>>[] axis)
    {
        var axisNames = axis.Select(ExpressionTreeHelper.PropertyName);

        foreach (var axisName in axisNames)
        {
            var metadata = _schema.ServerSide.FirstOrDefault(v => v.Name == axisName);
            if (metadata == null)
                throw new ArgumentException(
                    $"Property {axisName} can not be used as a pivot axis as it is not a server-side value");

            if (metadata.IsCollection)
                throw new ArgumentException(
                    $"Property {axisName} can not be used as a pivot axis as it is a collection");

            AxisList.Add(metadata.Order);
        }

        return this;
    }

    public PivotDefinition<T> AggregateValues(params Expression<Func<T, object>>[] valuesToAggregate)
    {
        var valueNames = valuesToAggregate.Select(ExpressionTreeHelper.PropertyName);

        foreach (var valueName in valueNames)
        {
            var metadata = _schema.ServerSide.FirstOrDefault(v => v.Name == valueName);
            if (metadata == null)
                throw new ArgumentException(
                    $"Property {valueName} can not be used as an aggregation value as it is not a server-side value");
            if (metadata.IsCollection)
                throw new ArgumentException(
                    $"Property {valueName} can not be used as a pivot aggregation value as it is a collection");

            ValuesList.Add(metadata.Order);
        }

        return this;
    }

    public PivotLevel Execute()
    {
        if (ValuesList.Count == 0)
            throw new NotSupportedException("At least an aggregation value must be specified");

        return _client.ComputePivot(Query, AxisList, ValuesList);
    }
}