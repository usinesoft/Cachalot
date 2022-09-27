using Client.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client.Tools
{
    /// <summary>
    /// Description of a CSV format that is automatically deduced by analysing a csv
    /// Contains column types, and statistical information that allows to decide which columns should be indexed 
    /// and which is the most discriminant key (it can be composed of multiple columns)
    /// It can be automatically converted to a <see cref="CollectionSchema"/>
    /// </summary>
    public class CsvSchema
    {

        public IList<CsvColumnInformation> Columns { get; } = new List<CsvColumnInformation>();

        /// <summary>
        /// The columns that will be combined to produce the most discriminant key (ideally unique)
        /// </summary>
        public IList<CsvColumnInformation> MostDiscriminantColumns { get; } = new List<CsvColumnInformation>();
        public char Separator { get; internal set; }


        /// <summary>
        /// Convert to <see cref="CollectionSchema"/> which will be used to store the CSV into Cachalot DB
        /// </summary>
        /// <returns></returns>
        public CollectionSchema ToCollectionSchema(int maxIndexes = 4)
        {
            var schema = new CollectionSchema { StorageLayout = Layout.Flat };


            // add a primary key
            schema.ServerSide.Add(new Messages.KeyInfo("@id", 0, IndexType.Primary));

            foreach (var column in Columns)
            {
                schema.ServerSide.Add(new Messages.KeyInfo(column.Name, column.ColumnIndex + 1, IndexType.None));
            }

            // index most discriminant columns
            var toIndex = Columns.Where(x => x.ColumnType != KeyValue.OriginalType.SomeFloat).OrderBy(x => x.AvgLinesInBucket).Take(maxIndexes);

            foreach (var column in toIndex)
            {
                schema.ServerSide[column.ColumnIndex + 1].IndexType = IndexType.Dictionary;
            }


            return schema;
        }

        public string AnalysisReport()
        {
            var result = new StringBuilder();


            result.AppendLine($"{"NAME",30} {"TYPE",20} {"AVG",10:F2} {"MAX",10:F2}");
            result.AppendLine($"--------------------------------------------------------------------------");


            foreach (var column in Columns)
            {
                result.AppendLine($"{column.Name,30} {column.ColumnType,20} {column.AvgLinesInBucket,10:F2} {column.MaxLinesInBucket,10:F2}");
            }

            result.AppendLine();
            result.Append("Most discriminant key=");
            foreach (var column in MostDiscriminantColumns)
            {
                result.Append(column.Name).Append("+");
            }

            return result.ToString().TrimEnd('+');
        }
    }
    public class CsvColumnInformation
    {
        public string Name { get; set; }

        public int ColumnIndex { get; set; }

        public int MaxLinesInBucket { get; set; }

        public double AvgLinesInBucket { get; set; }

        public KeyValue.OriginalType ColumnType { get; set; } = KeyValue.OriginalType.Null;

    }
}
