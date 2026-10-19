using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Application.Requests.Contracts;

/// <summary>Body of <c>PATCH /api/requests/{id}/status</c>. The version comes in the If-Match header.</summary>
public sealed record UpdateStatusRequest
{
    /// <summary>Required: a request body without it is rejected (400) instead of defaulting to "New".</summary>
    public required RequestStatus NewStatus { get; init; }
}

public sealed record UpdateStatusCommand(int RequestId, RequestStatus NewStatus, byte[] ExpectedVersion, string ChangedBy);

/// <summary>
/// Body of <c>POST /api/requests/bulk/status</c>. Each item carries the version the user saw –
/// one If-Match header cannot hold 100 versions.
/// </summary>
public sealed record BulkUpdateStatusRequest
{
    public required RequestStatus NewStatus { get; init; }

    public IReadOnlyList<BulkUpdateItem>? Items { get; init; }
}

public sealed record BulkUpdateItem(int Id, string? Version);

public sealed record BulkUpdateStatusCommand(BulkUpdateStatusRequest Request, string ChangedBy);

public enum BulkItemOutcome
{
    Updated,
    NotFound,
    Conflict,
    InvalidTransition,
}

public sealed record BulkItemResult(int Id, BulkItemOutcome Outcome, string? Error, string? Version);

/// <summary>Partial success: the outcome of every item, in the order of the request.</summary>
public sealed record BulkUpdateStatusResult(int Requested, int Succeeded, int Failed, IReadOnlyList<BulkItemResult> Results);
