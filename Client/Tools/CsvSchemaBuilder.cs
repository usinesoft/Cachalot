﻿using Client.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Client.Tools
{

    /// <summary>
    /// Infer a <see cref="CollectionSchema"/> by analysing a fragment of CSV file.
    /// For this case, tha layout is always <see cref="Layout.Flat"/> 
    /// </summary>
    public class CsvSchemaBuilder
    {
        
        public event EventHandler<ProgressEventArgs> Progress;

        /// <summary>
        /// A bucket = lines grouped by values in a column
        /// Used to detect the most discriminant columns so they are indexed server-side
        /// </summary>
        private class BucketMetrics
        {
            public BucketMetrics(int max, double avg, int bucketIndex)
            {
                Max = max;
                Avg = avg;
                BucketIndex = bucketIndex;
            }

            public int Max { get; }
            public double Avg { get; }
            public int BucketIndex { get; }
        }

        public string FilePath { get; set; }

        public CsvSchemaBuilder(string filePath)
        {
            FilePath = filePath;
        }

        private void ReportProgress(string message)
        {
            Progress?.Invoke(this, new ProgressEventArgs(message));
        }


        /// <summary>
        /// Generate a schema based on heuristics that process a fragment of the file 
        /// </summary>
        /// <param name="linesToUse"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public CsvSchema InfereSchema(int linesToUse = 10_000)
        {


            var schema = new CsvSchema();


            if (!File.Exists(FilePath))
            {
                throw new Exception($"The specified file was not found: {FilePath}");
            }

            using var reader = new StreamReader(FilePath);

            var header = reader.ReadLine();

            ReportProgress("Start schema inference");

            ProcessHeader(header, schema);

            ReportProgress("Header processed");

            InitBuckets(schema);

            for (int i = 0; i < linesToUse; i++)
            {
                var line = reader.ReadLine();

                if (line == null)
                {
                    break;
                }

                ProcessLine(line, schema, i);
            }

            ReportProgress("Lines parsed");


            for (int i = 0; i < schema.Columns.Count; i++)
            {
                var metrics = ComputeBucketMetrics(i);
                schema.Columns[i].AvgLinesInBucket = metrics.Avg;
                schema.Columns[i].MaxLinesInBucket = metrics.Max;
            }

            ReportProgress("Determining the most discriminant composite key");

            (var col1, var col2, var max) = DetermineMostDiscriminantCompositeKey(schema);

            if (col1 != col2) // both are zero if no composite key is better then the most discriminant single column
            {
                schema.MostDiscriminantColumns.Add(schema.Columns[col1]);
                schema.MostDiscriminantColumns.Add(schema.Columns[col2]);

                ReportProgress($"Most discriminant composite key: {schema.Columns[col1]}");
            }

            schema.Separator = Separator;

            return schema;
        }

        /// <summary>
        /// For each column contains the indexes of the lines by corresponding value
        /// It is used to infere the otimum indexing policy
        /// </summary>
        List<Dictionary<KeyValue, HashSet<int>>> Buckets { get; } = new List<Dictionary<KeyValue, HashSet<int>>>();

        private void InitBuckets(CsvSchema schema)
        {
            if (Buckets.Count != 0) // in case it is reused
            {
                Buckets.Clear();
            }

            int values = schema.Columns.Count;
            for (int i = 0; i < values; i++) // no need to create a bucket for the primary key
            {
                Buckets.Add(new Dictionary<KeyValue, HashSet<int>>());
            }
        }

        /// <summary>
        /// If no simple column is a unique key, try to intersect two columns to get a better key 
        /// </summary>
        /// <param name="schema"></param>
        /// <returns>firstColumnIndex, secondColumnIndex, max items for the found composite key</returns>
        private (int, int, int) DetermineMostDiscriminantCompositeKey(CsvSchema schema)
        {
            var ordered = schema.Columns.Where(x => x.ColumnType != KeyValue.OriginalType.SomeFloat && x.ColumnType != KeyValue.OriginalType.Null).OrderBy(x => x.AvgLinesInBucket).ToArray();

            int minMax = ordered.First().MaxLinesInBucket;// the minimum of the maximum bucket size for the composite column

            if (minMax == 1) // a single column already is a unique key, no need to find a composite one
            {
                return (0, 0, minMax);
            }

            int col1 = 0;
            int col2 = 0;

            for (int i = 0; i < ordered.Length; i++)
            {
                for (int j = i + 1; j < ordered.Length; j++)
                {
                    var countByCompositeKey = new Dictionary<string, int>();

                    var colIndex1 = ordered[i].ColumnIndex;
                    var colIndex2 = ordered[j].ColumnIndex;

                    foreach (var line in LinesCache)
                    {
                        var val1 = line[colIndex1];
                        var val2 = line[colIndex2];

                        var composite = $"{val1}-{val2}";

                        int count = 0;
                        countByCompositeKey.TryGetValue(composite, out count);
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

                    if (max == 1)// found a unique key, no need to continue
                    {
                        return (col1, col2, 1);
                    }
                }
            }



            return (col1, col2, minMax);

        }

        private void AddValueToBucket(int bucket, KeyValue value, int lineIndex)
        {
            if (!Buckets[bucket].TryGetValue(value, out HashSet<int> lines))
            {
                lines = new HashSet<int>();
                Buckets[bucket][value] = lines;
            }

            lines.Add(lineIndex); // To do : float values not in the basket
        }

        private void ProcessValueTypes(List<KeyValue> values, CsvSchema schema)
        {
            // upgrade types if a new value is more general than the previously found ones for the column: any type replaces null, float replaces int

            for (int i = 0; i < values.Count; i++)
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
                    {
                        // nothing to do it's ok
                        continue;
                    }

                    if (oldType == KeyValue.OriginalType.SomeInteger && newType == KeyValue.OriginalType.SomeFloat)
                    {
                        schema.Columns[i].ColumnType = newType;
                        continue;
                    }

                    if (oldType == KeyValue.OriginalType.SomeFloat && newType == KeyValue.OriginalType.SomeInteger)
                    {
                        // nothing to do it's ok
                        continue;
                    }

                    throw new FormatException($"Inconsistent tipe:value '{values[i]}' of type {newType} found on column of type {oldType} ");
                }
            }


        }

        List<List<string>> LinesCache { get; set; } = new List<List<string>>();


        private void ProcessLine(string line, CsvSchema schema, int lineIndex)
        {
            
            var stringValues = CsvHelper.SplitCsvLine(line, Separator);
            LinesCache.Add(stringValues);

            List<KeyValue> values = stringValues.Select(x => CsvHelper.GetTypedValue(x)).ToList();

            if (schema.Columns.Count != values.Count)
            {
                throw new FormatException($"Columns in line different from column in header: Header has {schema.Columns.Count} columns, this line has {values.Count} : {line}");
            }


            for (int i = 0; i < values.Count; i++)
            {
                AddValueToBucket(i, values[i], lineIndex);
            }

            ProcessValueTypes(values, schema);


        }

        public char Separator { get; private set; }

        private void ProcessHeader(string header, CsvSchema result)
        {
            Separator = CsvHelper.DetectSeparator(header);

            var parts = header.Split(Separator);

            int index = 0;
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    throw new FormatException($"Empty column name in header:{header}");
                }
                result.Columns.Add(new CsvColumnInformation { Name = part.Trim(), ColumnIndex = index++ });
            }

        }

        private void DetectSeparator(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                throw new ArgumentException($"'{nameof(header)}' cannot be null or whitespace.", nameof(header));
            }


            // automatically detect ",", "\t" or ";" used as separator; 
            if (header.Contains(','))
            {
                Separator = ',';
            }
            else if (header.Contains(';'))
            {
                Separator = ';';
            }
            else if (header.Contains('\t'))
            {
                Separator = '\t';
            }

            if (Separator == default)
            {
                throw new FormatException($"Can not detect column separator from header {header}");
            }
        }



        BucketMetrics ComputeBucketMetrics(int bucket)
        {

            int max = Buckets[bucket].Max(x => x.Value.Count);
            double avg = Buckets[bucket].Average(x => x.Value.Count);

            return new BucketMetrics(max, avg, bucket);
        }


    }
}