namespace RequestsManagement.Application.Requests.Contracts;

/// <summary>List query: filters + server-side sorting and paging.</summary>
public sealed class RequestQuery : RequestFilter
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 100_000;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public RequestSortField SortBy { get; set; } = RequestSortField.CreatedAt;

    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}

/// <summary>Sortable fields – a fixed list, so sorting is never built from free text.</summary>
public enum RequestSortField
{
    CreatedAt,
    UpdatedAt,
    Priority,
    Status,
    Title,
    OrganizationName,
}

public enum SortDirection
{
    Asc,
    Desc,
}
