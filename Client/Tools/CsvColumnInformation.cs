using Client.Core;

namespace Client.Tools;

public class CsvColumnInformation
{
    public string Name { get; set; }

    public int ColumnIndex { get; set; }

    public int MaxLinesInBucket { get; set; }

    public double AvgLinesInBucket { get; set; }

    public KeyValue.OriginalType ColumnType { get; set; } = KeyValue.OriginalType.Null;
}