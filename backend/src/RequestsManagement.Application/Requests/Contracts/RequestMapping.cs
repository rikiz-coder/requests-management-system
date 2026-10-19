using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Application.Requests.Contracts;

internal static class RequestMapping
{
    public static RequestDetailsDto ToDetailsDto(this ServiceRequest r) => new(
        r.Id,
        r.Title,
        r.OrganizationName,
        r.Status,
        r.Priority,
        r.AssignedTo,
        r.CreatedAt,
        r.UpdatedAt,
        RowVersions.Encode(r.RowVersion),
        StatusTransitions.AllowedFrom(r.Status));
}
