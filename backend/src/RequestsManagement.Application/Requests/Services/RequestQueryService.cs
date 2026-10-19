using FluentValidation;
using RequestsManagement.Application.Requests.Abstractions;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Exceptions;

namespace RequestsManagement.Application.Requests.Services;

/// <summary>Read side: list, single request, history, summary and lookups.</summary>
public sealed class RequestQueryService(
    IRequestRepository repository,
    ISummaryCache summaryCache,
    IValidator<RequestQuery> queryValidator,
    IValidator<RequestFilter> filterValidator)
{
    public async Task<PagedResult<RequestListItemDto>> SearchAsync(RequestQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);
        return await repository.SearchAsync(query, cancellationToken);
    }

    public async Task<RequestDetailsDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var request = await repository.GetAsync(id, cancellationToken) ?? throw new NotFoundException(id);
        return request.ToDetailsDto();
    }

    public async Task<IReadOnlyList<StatusHistoryEntryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken)
    {
        if (!await repository.ExistsAsync(id, cancellationToken))
        {
            throw new NotFoundException(id);
        }

        return await repository.GetHistoryAsync(id, cancellationToken);
    }

    /// <summary>
    /// Aggregations for the given filter. The unfiltered view – the screen every user opens and the most
    /// expensive aggregation – comes from the cache; filtered views are computed on each request.
    /// </summary>
    public async Task<RequestSummaryDto> GetSummaryAsync(RequestFilter filter, CancellationToken cancellationToken)
    {
        await filterValidator.ValidateAndThrowAsync(filter, cancellationToken);

        return filter.HasNoFilters()
            ? await summaryCache.GetOrCreateAsync(ct => repository.GetSummaryAsync(filter, ct), cancellationToken)
            : await repository.GetSummaryAsync(filter, cancellationToken);
    }

    public Task<LookupsDto> GetLookupsAsync(CancellationToken cancellationToken) =>
        repository.GetLookupsAsync(cancellationToken);
}
