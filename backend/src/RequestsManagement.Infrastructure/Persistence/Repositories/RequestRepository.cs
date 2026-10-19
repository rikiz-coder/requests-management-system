using Microsoft.EntityFrameworkCore;
using RequestsManagement.Application.Requests.Abstractions;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.Application.Requests.Exceptions;

namespace RequestsManagement.Infrastructure.Persistence.Repositories;

internal sealed class RequestRepository(RequestsDbContext db) : IRequestRepository
{
    public async Task<PagedResult<RequestListItemDto>> SearchAsync(RequestQuery query, CancellationToken cancellationToken)
    {
        var filtered = db.Requests.AsNoTracking().ApplyFilter(query);

        // Total for the pager: a separate COUNT with the same filters.
        var total = await filtered.CountAsync(cancellationToken);
        var skip = (query.Page - 1) * query.PageSize;
        if (total == 0 || skip >= total)
        {
            return new PagedResult<RequestListItemDto>([], query.Page, query.PageSize, total);
        }

        // One SQL statement: WHERE + ORDER BY + OFFSET/FETCH, projected to the DTO columns only.
        var rows = await filtered
            .ApplySort(query.SortBy, query.SortDirection)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(r => new
            {
                r.Id, r.Title, r.OrganizationName, r.Status, r.Priority, r.AssignedTo, r.CreatedAt, r.UpdatedAt, r.RowVersion,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new RequestListItemDto(r.Id, r.Title, r.OrganizationName, r.Status, r.Priority, r.AssignedTo,
                r.CreatedAt, r.UpdatedAt, RowVersions.Encode(r.RowVersion)))
            .ToList();

        return new PagedResult<RequestListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<RequestSummaryDto> GetSummaryAsync(RequestFilter filter, CancellationToken cancellationToken)
    {
        var filtered = db.Requests.AsNoTracking().ApplyFilter(filter);

        // GROUP BY in the database: at most 4 + 3 rows come back.
        var byStatus = await filtered
            .GroupBy(r => r.Status)
            .Select(g => new StatusCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var byPriority = await filtered
            .GroupBy(r => r.Priority)
            .Select(g => new PriorityCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        // Every status and priority appears, with 0 where there are none.
        var statusCounts = Enum.GetValues<RequestStatus>()
            .Select(s => new StatusCountDto(s, byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .ToList();
        var priorityCounts = Enum.GetValues<RequestPriority>()
            .Select(p => new PriorityCountDto(p, byPriority.FirstOrDefault(x => x.Priority == p)?.Count ?? 0))
            .ToList();

        return new RequestSummaryDto(statusCounts.Sum(s => s.Count), statusCounts, priorityCounts);
    }

    public Task<ServiceRequest?> GetAsync(int id, CancellationToken cancellationToken) =>
        db.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ServiceRequest?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        db.Requests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ServiceRequest>> GetManyForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        var values = ids.ToArray();
        return await db.Requests.Where(r => values.Contains(r.Id)).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken) =>
        db.Requests.AnyAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StatusHistoryEntryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken) =>
        await db.RequestStatusHistory.AsNoTracking()
            .Where(h => h.RequestId == id)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .Select(h => new StatusHistoryEntryDto(h.Id, h.PreviousStatus, h.NewStatus, h.ChangedAt, h.ChangedBy))
            .ToListAsync(cancellationToken);

    public Task<StatusHistoryEntryDto?> GetLastChangeAsync(int id, CancellationToken cancellationToken) =>
        db.RequestStatusHistory.AsNoTracking()
            .Where(h => h.RequestId == id)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .Select(h => new StatusHistoryEntryDto(h.Id, h.PreviousStatus, h.NewStatus, h.ChangedAt, h.ChangedBy))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LookupsDto> GetLookupsAsync(CancellationToken cancellationToken)
    {
        var handlers = await db.Requests.AsNoTracking()
            .Where(r => r.AssignedTo != null)
            .Select(r => r.AssignedTo!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var organizations = await db.Requests.AsNoTracking()
            .Select(r => r.OrganizationName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        return new LookupsDto(handlers, organizations);
    }

    public async Task SaveStatusChangeAsync(ServiceRequest request, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        // EF sends: UPDATE Requests SET ... WHERE Id = @id AND RowVersion = @expected
        // and inserts the history row – both in one transaction. If the version changed, no row is
        // updated, EF throws, and the transaction (including the history row) is rolled back.
        db.Entry(request).Property(r => r.RowVersion).OriginalValue = expectedVersion;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            Discard(request);
            throw new StaleVersionException(request.Id);
        }
    }

    public async Task<IReadOnlyCollection<int>> SaveStatusChangesAsync(
        IReadOnlyList<(ServiceRequest Request, byte[] ExpectedVersion)> changes, CancellationToken cancellationToken)
    {
        foreach (var (request, expectedVersion) in changes)
        {
            db.Entry(request).Property(r => r.RowVersion).OriginalValue = expectedVersion;
        }

        var conflicted = new HashSet<int>();
        while (true)
        {
            try
            {
                // All remaining changes in one transaction. If any request changed between our read and
                // this write, the database rejects it and the whole transaction is rolled back.
                await db.SaveChangesAsync(cancellationToken);
                return conflicted;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Leave out only the requests that conflicted, then save the rest again.
                // Each round removes at least one request, so the loop always ends.
                var failed = ex.Entries.Select(e => e.Entity).OfType<ServiceRequest>().ToList();
                if (failed.Count == 0)
                {
                    throw;
                }

                foreach (var request in failed)
                {
                    conflicted.Add(request.Id);
                    Discard(request);
                }
            }
        }
    }

    /// <summary>Stops tracking a request whose change was rejected, together with its pending history rows.</summary>
    private void Discard(ServiceRequest request)
    {
        foreach (var history in request.StatusHistory.Where(h => db.Entry(h).State == EntityState.Added).ToList())
        {
            db.Entry(history).State = EntityState.Detached;
        }

        db.Entry(request).State = EntityState.Detached;
    }
}
