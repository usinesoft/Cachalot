using System.Diagnostics;
using CachalotMonitor.Model;
using CachalotMonitor.Services;
using Client.Core;
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
    public SqlResponse GetQueryAsSql(string collection, [FromBody] AndQuery clientQuery)
    {
        var sql = _queryService.ClientQueryToSql(collection, clientQuery);
        return new() { Sql = sql };
    }

    [HttpGet("query/plan/{queryId}")]
    public ExecutionPlan? GetExecutionPlan(Guid queryId)
    {
        return _queryService.GetExecutionPlan(queryId);
    }

    [HttpPost("query/execute")]
    public DataResponse ExecuteQuery([FromBody] InputQuery query)
    {
        var result = new DataResponse();

        var watch = new Stopwatch();

        var queryId = Guid.NewGuid();
        try
        {
            watch.Start();
            result.Json = _queryService.QueryAsJson(query.Sql, query.FullText, queryId);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            watch.Stop();
            result.ClientTimeInMilliseconds = (int)watch.ElapsedMilliseconds;
            result.QueryId = queryId;
        }


        return result;
    }

    /// <summary>
    /// Can not use the DELETE verb as we need a body
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    [HttpPost("delete/execute")]
    public DataResponse DeleteMany([FromBody] InputQuery query)
    {
        var result = new DataResponse();

        var watch = new Stopwatch();

        var queryId = Guid.NewGuid();
        try
        {
            if (string.IsNullOrWhiteSpace(query.Sql))
            {
                throw new ArgumentException("empty query");
            }
            watch.Start();
            result.ItemsChanged = _queryService.DeleteMany(query.Sql);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            watch.Stop();
            result.ClientTimeInMilliseconds = (int)watch.ElapsedMilliseconds;
            result.QueryId = queryId;
        }


        return result;
    }

    [HttpPost("query/stream")]
    public async Task<IActionResult> ExecuteQueryAsStream([FromBody] InputQuery query)
    {
        var fileName = "data";
        // get the collection name from query
        var parts = query.Sql?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts?.Length; i++)
            if (parts[i].ToLower() == "from")
                if (parts.Length > i + 1)
                    fileName = parts[i + 1];

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