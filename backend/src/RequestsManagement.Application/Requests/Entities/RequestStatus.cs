namespace RequestsManagement.Application.Requests.Entities;

/// <summary>
/// Lifecycle status of a request. Stored as <c>tinyint</c>; values are explicit so that
/// reordering the enum never changes the meaning of stored data.
/// </summary>
public enum RequestStatus : byte
{
    New = 0,
    InProgress = 1,
    Waiting = 2,
    Completed = 3,
}
