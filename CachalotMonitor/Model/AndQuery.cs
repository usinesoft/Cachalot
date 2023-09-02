using Client.Interface;

namespace CachalotMonitor.Model;



public class AndQuery
{
    public int Take { get; set; } = 100;
    
    /// <summary>
    /// Optional column to order by
    /// </summary>
    public string? OrderBy { get; set; }

    public bool Descending { get; set; }

    public SimpleQuery[] SimpleQueries { get; set; } = Array.Empty<SimpleQuery>();
}