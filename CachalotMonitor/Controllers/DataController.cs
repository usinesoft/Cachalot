using CachalotMonitor.Model;
using CachalotMonitor.Services;
using Microsoft.AspNetCore.Mvc;

namespace CachalotMonitor.Controllers;

[Route("[controller]")]
[ApiController]
public class DataController : ControllerBase
{
    private readonly IQueryService _queryService;

    public DataController(IQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("query/metadata/{collection}/{property}")]
    public QueryMetadata GetQueryMetadata(string collection, string property)
    {
        return _queryService.GetMetadata(collection, property);
    }

    [HttpPost("query/sql/{collection}")]
    public SqlResponse GetQueryMetadata(string collection, [FromBody]AndQuery clientQuery)
    {
        var sql = _queryService.ClientQueryToSql(collection, clientQuery);
        return new() { Sql = sql };
    }

    [HttpPost("query/execute")]
    public DataResponse ExecuteQuery([FromBody]InputQuery query)
    {
        if (query.Sql != null) return new() { Json = _queryService.QueryAsJson(query.Sql, query.FullText) };

        throw new ArgumentException("Empty sql");
    }
}