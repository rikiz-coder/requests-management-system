namespace RequestsManagement.Application.Requests.Entities;

/// <summary>
/// The allowed status transitions – the single source of truth, used by the update logic,
/// returned to the client as <c>allowedNextStatuses</c>, and respected by the test data.
/// The specification requires blocking invalid transitions but does not define them;
/// these rules are the documented assumption.
/// </summary>
public static class StatusTransitions
{
    private static readonly IReadOnlyDictionary<RequestStatus, RequestStatus[]> Allowed =
        new Dictionary<RequestStatus, RequestStatus[]>
        {
            [RequestStatus.New] = [RequestStatus.InProgress, RequestStatus.Waiting],
            [RequestStatus.InProgress] = [RequestStatus.Waiting, RequestStatus.Completed],
            [RequestStatus.Waiting] = [RequestStatus.InProgress, RequestStatus.Completed],
            [RequestStatus.Completed] = [RequestStatus.InProgress], // reopen
        };

    /// <summary>Statuses a request in <paramref name="current"/> may move to (never itself).</summary>
    public static IReadOnlyList<RequestStatus> AllowedFrom(RequestStatus current) =>
        Allowed.TryGetValue(current, out var next) ? next : [];

    public static bool IsAllowed(RequestStatus from, RequestStatus to) => AllowedFrom(from).Contains(to);
}
