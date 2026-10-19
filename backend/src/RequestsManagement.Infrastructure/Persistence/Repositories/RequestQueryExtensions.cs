using Microsoft.EntityFrameworkCore;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Infrastructure.Persistence.Repositories;

/// <summary>Translates the filter and the sort into the SQL query. Nothing here runs in memory.</summary>
internal static class RequestQueryExtensions
{
    public static IQueryable<ServiceRequest> ApplyFilter(this IQueryable<ServiceRequest> query, RequestFilter filter)
    {
        if (filter.Status is { Count: > 0 } statuses)
        {
            var values = statuses.Distinct().ToArray();
            query = query.Where(r => values.Contains(r.Status));
        }

        if (filter.Priority is { Count: > 0 } priorities)
        {
            var values = priorities.Distinct().ToArray();
            query = query.Where(r => values.Contains(r.Priority));
        }

        if (!string.IsNullOrWhiteSpace(filter.OrganizationName))
        {
            // "Starts with" can use IX_Requests_OrganizationName (index seek).
            var organization = filter.OrganizationName.Trim();
            query = query.Where(r => r.OrganizationName.StartsWith(organization));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssignedTo))
        {
            var handler = filter.AssignedTo.Trim();
            query = query.Where(r => r.AssignedTo == handler);
        }

        if (filter.CreatedFrom is { } from)
        {
            var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt >= fromUtc);
        }

        if (filter.CreatedTo is { } to)
        {
            // Inclusive date: everything before the start of the next day.
            var toExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt < toExclusiveUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Translated to LIKE '%term%' (EF escapes %, _ and [ in the term).
            var term = filter.Search.Trim();
            query = query.Where(r => r.Title.Contains(term) || r.OrganizationName.Contains(term));
        }

        return query;
    }

    /// <summary>Sort by a whitelisted field, always followed by Id so that paging is stable.</summary>
    public static IOrderedQueryable<ServiceRequest> ApplySort(
        this IQueryable<ServiceRequest> query, RequestSortField field, SortDirection direction)
    {
        var desc = direction == SortDirection.Desc;
        var ordered = field switch
        {
            RequestSortField.UpdatedAt => desc ? query.OrderByDescending(r => r.UpdatedAt) : query.OrderBy(r => r.UpdatedAt),
            RequestSortField.Priority => desc ? query.OrderByDescending(r => r.Priority) : query.OrderBy(r => r.Priority),
            RequestSortField.Status => desc ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            RequestSortField.Title => desc ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
            RequestSortField.OrganizationName => desc ? query.OrderByDescending(r => r.OrganizationName) : query.OrderBy(r => r.OrganizationName),
            _ => desc ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
        };

        return desc ? ordered.ThenByDescending(r => r.Id) : ordered.ThenBy(r => r.Id);
    }
}
