using BenchmarkDotNet.Attributes;
using Cachalot.Linq;

namespace Benchmark;

public class QueryTest
{
    private Connector _connector;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _connector = new Connector("localhost:5432");
            
    }

    [GlobalCleanup]
    public void TearDown()
    {
        _connector.Dispose();
    }

    [Benchmark(Description = "1   item:SELECT FROM Invoice WHERE Id = '50011'")]
    public void InvoiceByPrimaryKey()
    {
        _ = _connector.SqlQueryAsJson("SELECT FROM Invoice WHERE Id = '50011'").ToList();
    }

    
    [Benchmark(Description = "27  items:SELECT FROM Invoice WHERE Date = '2023-04-23' AND DiscountPercentage > 0")]
    public void InvoiceTwoCriteriaReturn27Items()
    {
        _ = _connector.SqlQueryAsJson("SELECT FROM Invoice WHERE Date = '2023-04-23' AND DiscountPercentage > 0").ToList();
    }

    [Benchmark(Description = "57  items:SELECT FROM Invoice WHERE Date in ('2023-04-22','2023-04-23') AND  IsPayed = false")]
    public void InvoiceTwoCriteriaReturn57Items()
    {
        _ = _connector.SqlQueryAsJson("SELECT FROM Invoice WHERE Date in ('2023-04-22','2023-04-23') AND  IsPayed = false").ToList();
    }
    
    [Benchmark(Description = "86  items:SELECT FROM Client WHERE LastName = 'Corwin'")]
    public void ClientOneCriteriaReturn86Items()
    {
        _ = _connector.SqlQueryAsJson("SELECT FROM Client WHERE LastName = 'Corwin'").ToList();
    }

    [Benchmark(Description = "314 items:SELECT FROM Home WHERE Town = 'Paris' AND  AvailableDates contains '2024-05-05' AND Rooms = 2 ORDER BY PriceInEuros ")]
    public void HomeFourCriteriaReturns314Items()
    {
        _ = _connector.SqlQueryAsJson("SELECT FROM Home WHERE Town = 'Paris' AND  AvailableDates contains '2024-05-05' AND Rooms = 2 ORDER BY PriceInEuros ").ToList();
    }

    [Benchmark(Description = "SELECT DISTINCT Town from Home")]
    public void HomeSelectDistinctTown()
    {
        _ = _connector.SqlQueryAsJson("SELECT DISTINCT Town from Home").ToList();
    }

}