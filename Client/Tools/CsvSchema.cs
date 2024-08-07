﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Core;

namespace Client.Tools;

/// <summary>
///     Description of a CSV format that is automatically deduced by analysing a csv
///     Contains column types, and statistical information that allows to decide which columns should be indexed
///     and which is the most discriminant key (it can be composed of multiple columns)
///     It can be automatically converted to a <see cref="CollectionSchema" />
/// </summary>
public class CsvSchema
{
    public IList<CsvColumnInformation> Columns { get; } = new List<CsvColumnInformation>();

    /// <summary>
    ///     The
    /// that will be combined to produce the most discriminant key (ideally unique)
    /// </summary>
    public IList<CsvColumnInformation> MostDiscriminantColumns { get; } = new List<CsvColumnInformation>();

    public char Separator { get; internal set; }

    
    private List<Func<string, object>> Parsers { get; } = new();
    
    // if true, parse dates as US dates MM/dd...
    public bool UsFormat { get; set; }


    /// <summary>
    ///     Convert to <see cref="CollectionSchema" /> which will be used to store the CSV into Cachalot DB
    /// </summary>
    /// <returns></returns>
    public CollectionSchema ToCollectionSchema(int maxIndexes = 4)
    {
        var schema = new CollectionSchema { StorageLayout = Layout.Flat };


        // add a primary key
        schema.ServerSide.Add(new("@id", 0, IndexType.Primary));

        foreach (var column in Columns) schema.ServerSide.Add(new(column.Name, column.ColumnIndex + 1));

        // index most discriminant columns
        var toIndex = Columns.Where(x => x.ColumnType != KeyValue.OriginalType.SomeFloat)
            .OrderBy(x => x.AvgLinesInBucket).Take(maxIndexes);

        foreach (var column in toIndex) schema.ServerSide[column.ColumnIndex + 1].IndexType = IndexType.Dictionary;


        return schema;
    }

    private object BoolParser(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (bool.TryParse(value, out var result)) return result;

        return null;
    }

    private object IntParser(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (int.TryParse(value, out var result)) return result;

        return null;
    }

    private object FloatParser(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (double.TryParse(value, out var result)) return result;

        return null;
    }

    private object DateParser(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return DateHelper.ParseDateTime(value, UsFormat) ?? DateHelper.ParseDateTimeOffset(value);
        
    }

    /// <summary>
    ///     Set parsing code for each column for faster parsing
    /// </summary>
    public void GenerateParsers()
    {
        Parsers.Clear();

        foreach (var columnInformation in Columns)
            switch (columnInformation.ColumnType)
            {
                case KeyValue.OriginalType.Null:
                    Parsers.Add(x => null);
                    break;
                case KeyValue.OriginalType.Boolean:
                    Parsers.Add(BoolParser);
                    break;
                case KeyValue.OriginalType.SomeInteger:
                    Parsers.Add(IntParser);
                    break;
                case KeyValue.OriginalType.Date:
                    Parsers.Add(DateParser);
                    break;
                case KeyValue.OriginalType.String:
                    Parsers.Add(x => x);
                    break;

                case KeyValue.OriginalType.SomeFloat:
                    Parsers.Add(FloatParser);
                    break;
            }
    }

    public IList<KeyValue> ParseLine(string line)
    {
        var result = new List<KeyValue>();

        var stringValues = CsvHelper.SplitCsvLine(line, Separator);

        // in case line ends with separator and column missing
        if (stringValues.Count == Parsers.Count - 1)
        {
            stringValues.Add(string.Empty);
        }

        if (stringValues.Count != Parsers.Count)
            throw new ArgumentException(
                $"Line does not match schema: line has {stringValues.Count} columns, schema has {Parsers.Count}");

        for (var i = 0; i < stringValues.Count; i++)
        {
            var value = Parsers[i](stringValues[i]);
            result.Add(new(value));
        }


        return result;
    }


    public string AnalysisReport()
    {
        var result = new StringBuilder();


        result.AppendLine($"{"NAME",30} {"TYPE",20} {"AVG",10:F2} {"MAX",10:F2}");
        result.AppendLine("--------------------------------------------------------------------------");


        foreach (var column in Columns)
            result.AppendLine(
                $"{column.Name,30} {column.ColumnType,20} {column.AvgLinesInBucket,10:F2} {column.MaxLinesInBucket,10:F2}");

        result.AppendLine();
        result.Append("Most discriminant key=");
        foreach (var column in MostDiscriminantColumns) result.Append(column.Name).Append("+");

        return result.ToString().TrimEnd('+');
    }
}