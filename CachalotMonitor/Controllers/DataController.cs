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
}