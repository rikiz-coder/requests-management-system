using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Application.Requests.Contracts;

/// <summary>A row in the list. <see cref="Version"/> is needed for bulk updates.</summary>
public sealed record RequestListItemDto(
    int Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Version);

/// <summary>A single request, including the statuses it may move to.</summary>
public sealed record RequestDetailsDto(
    int Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Version,
    IReadOnlyList<RequestStatus> AllowedNextStatuses);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record StatusHistoryEntryDto(
    long Id,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    DateTime ChangedAt,
    string ChangedBy);

public sealed record StatusCountDto(RequestStatus Status, int Count);

public sealed record PriorityCountDto(RequestPriority Priority, int Count);

/// <summary>Aggregations for the current filter.</summary>
public sealed record RequestSummaryDto(
    int Total,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<PriorityCountDto> ByPriority);

/// <summary>Values for the filter drop-downs and the "current user" picker.</summary>
public sealed record LookupsDto(IReadOnlyList<string> Handlers, IReadOnlyList<string> Organizations);
