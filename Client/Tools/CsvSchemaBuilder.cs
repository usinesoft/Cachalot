using Client.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Client.Tools
{
    public class CsvSchemaBuilder
    {

        private class BucketMetrics
        {
            public BucketMetrics(int max, double avg, int bucketIndex)
            {
                Max = max;
                Avg = avg;
                BucketIndex = bucketIndex;
            }

            public int Max { get;  }
            public double Avg { get;  }
            public int BucketIndex { get;  }
        }

        public string FilePath { get; set; }

        public CsvSchemaBuilder(string filePath)
        {
            FilePath = filePath;
        }

        /// <summary>
        /// Generate a schema based on heuristics that process a fragment of the file 
        /// </summary>
        /// <param name="linesToUse"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public CollectionSchema InfereSchema(int linesToUse = 10_000)
        {
            var schema = new CollectionSchema();

            var lineCache = new List<string>();

            // add a primary key
            schema.ServerSide.Add(new Messages.KeyInfo("@id", 0, IndexType.Primary));

            if (!File.Exists(FilePath)) 
            {
                throw new Exception($"The specified file was not found: {FilePath}");
            }

            using var reader = new StreamReader(FilePath);

            var header = reader.ReadLine();

            schema.StorageLayout = Layout.Flat;
            
            schema.ServerSide.Add(new Messages.KeyInfo("@id", 0, IndexType.Primary));

            ProcessHeader(header, schema);

            InitBuckets(schema);

            for (int i = 0; i < linesToUse; i++)
            {
                var line = reader.ReadLine();
                
                if(line == null)
                {
                    break;
                }

                ProcessLine(line, schema, i);
            }

            var toBeIndexed = GetMostDiscriminantColumns();


            return schema;
        }

        /// <summary>
        /// For each column contains the indexes of the lines by corresponding value
        /// It is used to infere the otimum indexing policy
        /// </summary>
        List<Dictionary<KeyValue, HashSet<int>>> Buckets { get; } = new List<Dictionary<KeyValue, HashSet<int>>>();
        List<KeyValue.OriginalType> ColumnTypes { get; } = new List<KeyValue.OriginalType>();

        private void InitBuckets(CollectionSchema schema)
        {
            int values = schema.ServerSide.Count;
            for (int i = 0; i < values - 1; i++) // no need to create a bucket for the primary key
            {
                Buckets.Add(new Dictionary<KeyValue, HashSet<int>>());
            }
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

        private void ProcessValueTypes(List<KeyValue> values)
        {
            if(ColumnTypes.Count == 0)// initialize types
            {
                foreach(KeyValue kv in values)
                {
                    ColumnTypes.Add(kv.Type);
                }
            }
            else // upgrade types if a new value is more general than the previously found ones for the column: any type replaces null, float replaces int
            {
                
                for (int i = 0; i < values.Count; i++)
                {
                    var oldType = ColumnTypes[i];
                    var newType = values[i].Type;

                    if(oldType != newType)
                    {
                        if(oldType == KeyValue.OriginalType.Null)
                        {
                            ColumnTypes[i] = newType;
                            continue;
                        }

                        if (newType == KeyValue.OriginalType.Null) 
                        {
                            // nothing to do i's ok
                            continue;
                        }

                        if(oldType == KeyValue.OriginalType.SomeInteger && newType == KeyValue.OriginalType.SomeFloat)
                        {
                            ColumnTypes[i] = newType;
                            continue;
                        }

                        if (oldType == KeyValue.OriginalType.SomeInteger && newType == KeyValue.OriginalType.SomeFloat)
                        {
                            // nothing to do i's ok
                            continue;
                        }

                        
                    }
                }
            }

        }

        private void ProcessLine(string line, CollectionSchema schema, int lineIndex)
        {
            List<KeyValue> values = new List<KeyValue>();

            bool ignoreSeparator = false;

            var currentValue = new StringBuilder();

            foreach(char c in line)
            {
                if (c == '"') // ignore separator inside "" according to csv specification
                {
                    if (ignoreSeparator)
                    {
                        ignoreSeparator = false;
                    }
                    else
                    {
                        ignoreSeparator = true;
                    }                    
                }
                else if (c == Separator)
                {
                    values.Add(new KeyValue(JExtensions.SmartParse(currentValue.ToString())));
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }

            // add the last column
            if (!line.EndsWith(Separator))
            {
                values.Add(new KeyValue(JExtensions.SmartParse(currentValue.ToString())));
            }

            if (schema.ServerSide.Count - 1 != values.Count)
            {
                throw new FormatException($"Columns in line different from column in header: Header has {schema.ServerSide.Count} columns, this line has {values.Count} : {line}");
            }


            for (int i = 0; i < values.Count; i++) 
            {
                AddValueToBucket(i, values[i], lineIndex);
            }

            ProcessValueTypes(values);

            
        }

        public char Separator { get; private set; }

        private void ProcessHeader(string header, CollectionSchema result)
        {
            DetectSeparator(header);

            var parts = header.Split(Separator);

            int order = 1;// zero is the primary key
            foreach (var part in parts) 
            { 
                result.ServerSide.Add(new Messages.KeyInfo(part, order++, IndexType.None));
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

        /// <summary>
        /// Identify the columns that must be indexed (ignore float values)
        /// </summary>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        List<int> GetMostDiscriminantColumns(int maxCount = 4)
        {
            
            var metrics = new List<BucketMetrics>();

            for (int i = 0; i < Buckets.Count; i++)
            {
                if (ColumnTypes[i] != KeyValue.OriginalType.SomeFloat)
                {
                    metrics.Add(ComputeBucketMetrics(i));
                }
                
            }

            var ordered = metrics.OrderBy(m=>m.Avg).ToList();

            return ordered.Select(x => x.BucketIndex).ToList();

            
        }

        BucketMetrics ComputeBucketMetrics(int bucket)
        {
            
            int max = Buckets[bucket].Max(x => x.Value.Count);
            double avg = Buckets[bucket].Average(x => x.Value.Count);

            return new BucketMetrics(max, avg, bucket);
        }
    }
}
