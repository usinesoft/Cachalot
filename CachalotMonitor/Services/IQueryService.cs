using CachalotMonitor.Model;
using Client.Core;

namespace CachalotMonitor.Services;

public interface IQueryService
{
    public QueryMetadata GetMetadata(string collection, string property);

    public string ClientQueryToSql(string collection, AndQuery query);

    string QueryAsJson(string? sql, string? fullTextQuery = null, Guid queryId = default);

    Task QueryAsStream(Stream targetStream, string? sql, string? fullTextQuery = null);

    Task PutManyAsStream(Stream stream, string collectionName);

    ExecutionPlan? GetExecutionPlan(Guid queryId);
}