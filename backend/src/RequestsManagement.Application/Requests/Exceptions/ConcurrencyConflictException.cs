using RequestsManagement.Application.Requests.Contracts;

namespace RequestsManagement.Application.Requests.Exceptions;

/// <summary>
/// The client's version of the request is outdated: someone else changed it in the meantime (HTTP 409).
/// Carries the current state and the last change, so the client can explain what happened.
/// </summary>
public sealed class ConcurrencyConflictException(int requestId, RequestDetailsDto? currentState, StatusHistoryEntryDto? lastChange)
    : Exception($"Request {requestId} was modified by another user. Reload it and try again.")
{
    public int RequestId { get; } = requestId;

    public RequestDetailsDto? CurrentState { get; } = currentState;

    public StatusHistoryEntryDto? LastChange { get; } = lastChange;
}

/// <summary>
/// Thrown by the repository when the database rejected an update because the row version changed.
/// The service translates it into <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class StaleVersionException(int requestId)
    : Exception($"Request {requestId} has a newer version in the database.")
{
    public int RequestId { get; } = requestId;
}
