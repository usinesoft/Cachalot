namespace CachalotMonitor.Model;

public enum PropertyType
{
    SomeInteger = 0,
    SomeFloat = 1,
    Boolean = 2,
    Date = 3,
    String = 4,
    Null = 5,
    Unknown = 6,
}

public class SimpleQuery
{
    public string? PropertyName { get; set; }
    
    public string? Operator { get; set; }
    
    public PropertyType DataType { get; set; }
    public bool PropertyIsCollection { get; set; }

    public string[] Values { get; set; } = Array.Empty<string>();

    public bool CheckIsValid()
    {
        if(string.IsNullOrWhiteSpace(PropertyName))
            return false;

        if (string.IsNullOrWhiteSpace(Operator))
            return false;

        if (Operator is "is null" or "is not null")
        {
            if (Values.Any()) // this operator does not accept values
            {
                return false;
            }
        }
        else // for the other operators values are mandatory
        {
            if (Values.Length == 0)
            {
                return false;
            }

            if (Values.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }
        }

        return true;
    }

}

public class AndQuery
{
    public SimpleQuery[] SimpleQueries { get; set; } = Array.Empty<SimpleQuery>();
}