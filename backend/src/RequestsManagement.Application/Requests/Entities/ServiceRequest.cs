namespace RequestsManagement.Application.Requests.Entities;

/// <summary>
/// A request (פנייה) received from an organization or employer.
/// </summary>
public class ServiceRequest
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OrganizationName { get; set; } = string.Empty;

    public RequestStatus Status { get; set; }

    public RequestPriority Priority { get; set; }

    /// <summary>Name of the handler; <c>null</c> while the request is not assigned.</summary>
    public string? AssignedTo { get; set; }

    /// <summary>Creation time, UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Time of the last change, UTC.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Concurrency token. SQL Server <c>rowversion</c>: changed by the database on every update,
    /// so an update based on an outdated version can be detected and rejected.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    public List<RequestStatusHistory> StatusHistory { get; } = [];
}
