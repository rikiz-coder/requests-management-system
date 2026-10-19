using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Application.Requests.Abstractions;

/// <summary>
/// Data access for requests. Implemented in Infrastructure with EF Core; every query runs in the
/// database (filtering, sorting, paging, aggregation) and returns only what was asked for.
/// </summary>
public interface IRequestRepository
{
    Task<PagedResult<RequestListItemDto>> SearchAsync(RequestQuery query, CancellationToken cancellationToken);

    Task<RequestSummaryDto> GetSummaryAsync(RequestFilter filter, CancellationToken cancellationToken);

    /// <summary>Read-only load (not tracked).</summary>
    Task<ServiceRequest?> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>Tracked load, to change the status and save.</summary>
    Task<ServiceRequest?> GetForUpdateAsync(int id, CancellationToken cancellationToken);

    /// <summary>Tracked load of several requests in one query.</summary>
    Task<IReadOnlyList<ServiceRequest>> GetManyForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);

    /// <summary>History of one request, newest first.</summary>
    Task<IReadOnlyList<StatusHistoryEntryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken);

    Task<StatusHistoryEntryDto?> GetLastChangeAsync(int id, CancellationToken cancellationToken);

    Task<LookupsDto> GetLookupsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves a status change made with <see cref="ServiceRequest.ChangeStatus"/>, together with its history row,
    /// only if the row version in the database still equals <paramref name="expectedVersion"/>.
    /// Throws <see cref="Exceptions.StaleVersionException"/> otherwise; nothing is saved.
    /// </summary>
    Task SaveStatusChangeAsync(ServiceRequest request, byte[] expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Saves several status changes in one transaction, each guarded by its own expected version.
    /// A request whose version changed in the meantime is left out and the rest are saved.
    /// Returns the ids that were left out because of a version conflict.
    /// </summary>
    Task<IReadOnlyCollection<int>> SaveStatusChangesAsync(
        IReadOnlyList<(ServiceRequest Request, byte[] ExpectedVersion)> changes, CancellationToken cancellationToken);
}
