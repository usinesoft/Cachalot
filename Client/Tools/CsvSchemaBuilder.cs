﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Core;
using JetBrains.Annotations;

namespace Client.Tools;

/// <summary>
///     Infer a <see cref="CollectionSchema" /> by analyzing a fragment of CSV file.
///     For this case, tha layout is always <see cref="Layout.Flat" />
/// </summary>
public class CsvSchemaBuilder
{
    public CsvSchemaBuilder(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; set; }

    /// <summary>
    ///     For each column contains the indexes of the lines by corresponding value
    ///     It is used to infer the optimum indexing policy
    /// </summary>
    private List<Dictionary<KeyValue, HashSet<int>>> Buckets { get; } = new();

    private List<List<string>> LinesCache { get; } = new();

    public char Separator { get; private set; }

    public event EventHandler<ProgressEventArgs> Progress;

    private void ReportProgress(string message)
    {
        Progress?.Invoke(this, new(message));
    }


    /// <summary>
    ///     Generate a schema based on heuristics that process a fragment of the file
    /// </summary>
    /// <param name="linesToUse"></param>
    /// <param name="usFormat">Parse US dates </param>
    /// <param name="findCompositeKey">If true also find the most discriminant combination of two keys</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public CsvSchema InferSchema(int linesToUse = 10_000, bool usFormat = false, bool findCompositeKey = true)
    {
        var schema = new CsvSchema
        {
            UsFormat = usFormat
        };

        if (!File.Exists(FilePath)) throw new($"The specified file was not found: {FilePath}");

        using var reader = new StreamReader(FilePath, Encoding.UTF8);

        var header = reader.ReadLine();

        ReportProgress("Start schema inference");

        ProcessHeader(header, schema);

        ReportProgress("Header processed");

        InitBuckets(schema);

        for (var i = 0; i < linesToUse; i++)
        {
            var line = reader.ReadLine();

            if (line == null) break;

            ProcessLine(line, schema, i);
        }

        schema.GenerateParsers();


        ReportProgress("Lines parsed");


        for (var i = 0; i < schema.Columns.Count; i++)
        {
            var metrics = ComputeBucketMetrics(i);
            schema.Columns[i].AvgLinesInBucket = metrics.Avg;
            schema.Columns[i].MaxLinesInBucket = metrics.Max;
        }

        if (findCompositeKey)
        {
            ReportProgress("Determining the most discriminant composite key");

            var (col1, col2, _) = DetermineMostDiscriminantCompositeKey(schema);

            if (col1 != col2) // both are zero if no composite key is better then the most discriminant single column
            {
                schema.MostDiscriminantColumns.Add(schema.Columns[col1]);
                schema.MostDiscriminantColumns.Add(schema.Columns[col2]);

                ReportProgress($"Most discriminant composite key: {schema.Columns[col1]}");
            }
        }


        schema.Separator = Separator;

        return schema;
    }

    private void InitBuckets(CsvSchema schema)
    {
        if (Buckets.Count != 0) // in case it is reused
            Buckets.Clear();

        var values = schema.Columns.Count;
        for (var i = 0; i < values; i++) // no need to create a bucket for the primary key
            Buckets.Add(new());
    }

    /// <summary>
    ///     If no simple column is a unique key, try to intersect two columns to get a better key
    /// </summary>
    /// <param name="schema"></param>
    /// <returns>firstColumnIndex, secondColumnIndex, max items for the found composite key</returns>
    private (int, int, int) DetermineMostDiscriminantCompositeKey(CsvSchema schema)
    {
        var ordered = schema.Columns
            .Where(x => x.ColumnType != KeyValue.OriginalType.SomeFloat && x.ColumnType != KeyValue.OriginalType.Null)
            .OrderBy(x => x.AvgLinesInBucket).ToArray();

        var minMax =
            ordered.First().MaxLinesInBucket; // the minimum of the maximum bucket size for the composite column

        if (minMax == 1) // a single column already is a unique key, no need to find a composite one
            return (0, 0, minMax);

        var col1 = 0;
        var col2 = 0;

        for (var i = 0; i < ordered.Length; i++)
        {
            for (var j = i + 1; j < ordered.Length; j++)
            {
                var countByCompositeKey = new Dictionary<string, int>();

                var colIndex1 = ordered[i].ColumnIndex;
                var colIndex2 = ordered[j].ColumnIndex;

                foreach (var line in LinesCache)
                {
                    var val1 = line[colIndex1];
                    var val2 = line[colIndex2];

                    var composite = $"{val1}-{val2}";

                    countByCompositeKey.TryGetValue(composite, out var count);
                    countByCompositeKey[composite] = count + 1;
                }

                var max = countByCompositeKey.Max(x => x.Value);
                var avg = countByCompositeKey.Average(x => x.Value);

                if (max < minMax)
                {
                    minMax = max;

                    col1 = colIndex1;
                    col2 = colIndex2;
                }

                if (max == 1) // found a unique key, no need to continue
                    return (col1, col2, 1);
            }
        }


        return (col1, col2, minMax);
    }

    private void AddValueToBucket(int bucket, KeyValue value, int lineIndex)
    {
        if (!Buckets[bucket].TryGetValue(value, out var lines))
        {
            lines = new();
            Buckets[bucket][value] = lines;
        }

        lines.Add(lineIndex); // To do : float values not in the basket
    }

    private void ProcessValueTypes(List<KeyValue> values, CsvSchema schema)
    {
        // upgrade types if a new value is more general than the previously found ones for the column: any type replaces null, float replaces int

        for (var i = 0; i < values.Count; i++)
        {
            var oldType = schema.Columns[i].ColumnType;
            var newType = values[i].Type;

            if (oldType != newType)
            {
                if (oldType == KeyValue.OriginalType.Null)
                {
                    schema.Columns[i].ColumnType = newType;
                    continue;
                }

                if (newType == KeyValue.OriginalType.Null)
                    // nothing to do it's ok
                    continue;

                if (oldType == KeyValue.OriginalType.SomeInteger && newType == KeyValue.OriginalType.SomeFloat)
                {
                    schema.Columns[i].ColumnType = newType;
                    continue;
                }

                if (oldType == KeyValue.OriginalType.SomeFloat && newType == KeyValue.OriginalType.SomeInteger)
                    // nothing to do it's ok
                    continue;

                schema.Columns[i].ColumnType = KeyValue.OriginalType.String;
                
            }
        }
    }


    private void ProcessLine(string line, CsvSchema schema, int lineIndex)
    {
        var stringValues = CsvHelper.SplitCsvLine(line, Separator);
        LinesCache.Add(stringValues);

        var values = stringValues.Select(x=> CsvHelper.GetTypedValue(x, schema.UsFormat)).ToList();

        if (schema.Columns.Count != values.Count)
            throw new FormatException(
                $"Columns in line different from column in header: Header has {schema.Columns.Count} columns, this line has {values.Count} : {line}");


        for (var i = 0; i < values.Count; i++) AddValueToBucket(i, values[i], lineIndex);

        ProcessValueTypes(values, schema);
    }

    public static string FirstCharToUpper(string input) =>
        input switch
        {
            null => throw new ArgumentNullException(nameof(input)),
            "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
            _ => string.Concat(input[0].ToString().ToUpper(), input.AsSpan(1))
        };

    /// <summary>
    /// Remove spaces from column name
    /// </summary>
    /// <returns></returns>
    private static string NormalizeColumnName([NotNull] string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name), "Empty column name");

        name = name.Trim();
        
        if (!name.Contains(' ')) return FirstCharToUpper(name);
        
        var parts = name.Split(' ')
            .Where(x=> !string.IsNullOrWhiteSpace(x))
            .Select(FirstCharToUpper)
            .ToList();

        return string.Join(null, parts);

    }

    private void ProcessHeader(string header, CsvSchema result)
    {
        Separator = CsvHelper.DetectSeparator(header);

        var parts = header.Split(Separator);

        var index = 0;
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) throw new FormatException($"Empty column name in header:{header}");
            result.Columns.Add(new() { Name = NormalizeColumnName(part), ColumnIndex = index++ });
        }
    }

    
    private BucketMetrics ComputeBucketMetrics(int bucket)
    {
        var max = Buckets[bucket].Max(x => x.Value.Count);
        var avg = Buckets[bucket].Average(x => x.Value.Count);

        return new(max, avg);
    }

    /// <summary>
    ///     A bucket = lines grouped by values in a column
    ///     Used to detect the most discriminant columns so they are indexed server-side
    /// </summary>
    private class BucketMetrics
    {
        public BucketMetrics(int max, double avg)
        {
            Max = max;
            Avg = avg;
        }

        public int Max { get; }
        public double Avg { get; }
    }
}