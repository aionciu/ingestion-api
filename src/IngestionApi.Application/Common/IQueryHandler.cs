namespace IngestionApi.Application.Common;

public interface IQueryHandler<TQuery, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct = default);
}
