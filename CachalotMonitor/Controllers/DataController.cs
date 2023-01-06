using System.Collections.Concurrent;
using CachalotMonitor.Model;
using CachalotMonitor.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    [HttpPost("query/stream")]
    public async Task<IActionResult> ExecuteQueryAsStream([FromBody]InputQuery query)
    {
        var fileName = "data";
        // get the collection name from query
        var parts =  query.Sql?.Split(' ', StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts?.Length; i++)
        {
            if (parts[i].ToLower() == "from")
            {
                if (parts.Length > i + 1)
                {
                    fileName = parts[i + 1];
                }
            }
        }
        
        HttpContext.Response.Headers["content-type"] = "application/json";
        HttpContext.Response.Headers["content-disposition"] = $"attachment; filename={fileName}";

        await _queryService.QueryAsStream(HttpContext.Response.Body, query.Sql, query.FullText);

        await HttpContext.Response.CompleteAsync();

        return new EmptyResult();
    }

    

    [HttpPost("put/stream/{collectionName}")]
    public async Task<IActionResult> PutManyAsStream(string collectionName, IFormFile file)
    {
        
        await _queryService.PutManyAsStream(file.OpenReadStream(), collectionName);
        
        return new EmptyResult();
    }
}