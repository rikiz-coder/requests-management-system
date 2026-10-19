using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Application.Requests.Exceptions;

/// <summary>The status change is not allowed by <see cref="StatusTransitions"/> (HTTP 422).</summary>
public sealed class InvalidStatusTransitionException(int requestId, RequestStatus currentStatus, RequestStatus requestedStatus)
    : Exception($"Request {requestId} cannot move from {currentStatus} to {requestedStatus}.")
{
    public int RequestId { get; } = requestId;

    public RequestStatus CurrentStatus { get; } = currentStatus;

    public RequestStatus RequestedStatus { get; } = requestedStatus;

    public IReadOnlyList<RequestStatus> AllowedStatuses { get; } = StatusTransitions.AllowedFrom(currentStatus);
}
