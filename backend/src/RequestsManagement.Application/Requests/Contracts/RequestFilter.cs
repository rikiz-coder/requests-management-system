using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Application.Requests.Contracts;

/// <summary>
/// Filters shared by the list and the summary. All optional, combined with AND.
/// Bound from the query string: <c>?status=New&amp;status=Waiting&amp;search=היתר</c>.
/// </summary>
public class RequestFilter
{
    /// <summary>One or more statuses.</summary>
    public List<RequestStatus>? Status { get; set; }

    /// <summary>One or more priorities.</summary>
    public List<RequestPriority>? Priority { get; set; }

    /// <summary>Organization name starts with this value.</summary>
    public string? OrganizationName { get; set; }

    /// <summary>Exact handler name.</summary>
    public string? AssignedTo { get; set; }

    /// <summary>Created on or after this date (UTC).</summary>
    public DateOnly? CreatedFrom { get; set; }

    /// <summary>Created on or before this date (UTC, inclusive).</summary>
    public DateOnly? CreatedTo { get; set; }

    /// <summary>
    /// Free text: contained in the title or in the organization name.
    /// A number alone also finds the request with that Id.
    /// </summary>
    public string? Search { get; set; }

    public bool HasNoFilters() =>
        (Status is null || Status.Count == 0)
        && (Priority is null || Priority.Count == 0)
        && string.IsNullOrWhiteSpace(OrganizationName)
        && string.IsNullOrWhiteSpace(AssignedTo)
        && CreatedFrom is null
        && CreatedTo is null
        && string.IsNullOrWhiteSpace(Search);
}
