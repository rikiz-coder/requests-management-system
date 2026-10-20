using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.IntegrationTests.Fixtures;

namespace RequestsManagement.IntegrationTests.Api;

/// <summary>GET /api/requests – filtering, search, sorting and paging in the database.</summary>
[Collection(ApiCollection.Name)]
public class SearchTests(ApiFixture api)
{
    private readonly HttpClient _client = api.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Default_list_is_the_first_page_newest_first()
    {
        var page = await GetAsync("/api/requests");

        Assert.Equal(1, page.Page);
        Assert.Equal(RequestQuery.DefaultPageSize, page.PageSize);
        Assert.Equal(RequestQuery.DefaultPageSize, page.Items.Count);
        Assert.Equal(await api.QueryDbAsync(db => db.Requests.CountAsync(Ct)), page.TotalCount);
        AssertOrdered(page.Items, (a, b) => a.CreatedAt > b.CreatedAt || (a.CreatedAt == b.CreatedAt && a.Id > b.Id));
    }

    [Fact]
    public async Task Combined_filters_return_exactly_the_matching_requests()
    {
        var from = new DateOnly(2025, 6, 1);
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var page = await GetAsync(
            "/api/requests?status=InProgress&status=Waiting&priority=High&createdFrom=2025-06-01&pageSize=100");

        var expected = await api.QueryDbAsync(db => db.Requests.CountAsync(r =>
            (r.Status == RequestStatus.InProgress || r.Status == RequestStatus.Waiting)
            && r.Priority == RequestPriority.High && r.CreatedAt >= fromUtc, Ct));
        Assert.True(expected > 0);
        Assert.Equal(expected, page.TotalCount);
        Assert.All(page.Items, r =>
        {
            Assert.Contains(r.Status, new[] { RequestStatus.InProgress, RequestStatus.Waiting });
            Assert.Equal(RequestPriority.High, r.Priority);
            Assert.True(r.CreatedAt >= fromUtc);
        });
    }

    [Fact]
    public async Task Search_matches_title_or_organization()
    {
        const string term = "היתר";

        var page = await GetAsync($"/api/requests?search={Uri.EscapeDataString(term)}&pageSize=100");

        var expected = await api.QueryDbAsync(db => db.Requests.CountAsync(
            r => r.Title.Contains(term) || r.OrganizationName.Contains(term), Ct));
        Assert.True(expected > 0);
        Assert.Equal(expected, page.TotalCount);
        Assert.All(page.Items, r => Assert.True(r.Title.Contains(term) || r.OrganizationName.Contains(term)));
    }

    [Fact]
    public async Task Search_never_matches_across_the_title_and_the_organization()
    {
        var sample = await api.QueryDbAsync(db => db.Requests.AsNoTracking().OrderBy(r => r.Id).Skip(77).FirstAsync(Ct));
        var term = $"{sample.Title.Split(' ')[^1]} {sample.OrganizationName.Split(' ')[0]}"; // end of title + start of organization

        var page = await GetAsync($"/api/requests?search={Uri.EscapeDataString(term)}&pageSize=100");

        var expected = await api.QueryDbAsync(db => db.Requests.CountAsync(
            r => r.Title.Contains(term) || r.OrganizationName.Contains(term), Ct));
        Assert.Equal(expected, page.TotalCount);
        Assert.DoesNotContain(page.Items, r => r.Id == sample.Id && !r.Title.Contains(term) && !r.OrganizationName.Contains(term));
    }

    [Fact]
    public async Task Search_ignores_the_case_of_latin_letters()
    {
        var created = DateTime.UtcNow.AddYears(-5);
        var request = new ServiceRequest
        {
            Title = "Payroll Report Q3", OrganizationName = "Acme Logistics Ltd", Status = RequestStatus.New,
            Priority = RequestPriority.Low, CreatedAt = created, UpdatedAt = created,
        };
        await api.QueryDbAsync(async db => { db.Requests.Add(request); return await db.SaveChangesAsync(Ct); });
        try
        {
            foreach (var term in new[] { "payroll", "PAYROLL", "acme log" })
            {
                var page = await GetAsync($"/api/requests?search={Uri.EscapeDataString(term)}&pageSize=100");
                Assert.Contains(page.Items, r => r.Id == request.Id);
            }
        }
        finally
        {
            await api.QueryDbAsync(db => db.Requests.Where(r => r.Id == request.Id).ExecuteDeleteAsync(Ct));
        }
    }

    [Fact]
    public async Task Search_by_number_finds_the_request_with_that_id()
    {
        var id = await api.QueryDbAsync(db => db.Requests.OrderBy(r => r.Id).Skip(1234).Select(r => r.Id).FirstAsync(Ct));

        var page = await GetAsync($"/api/requests?search={id}&pageSize=100");

        Assert.Contains(page.Items, r => r.Id == id);
        Assert.All(page.Items, r => Assert.True(
            r.Id == id || r.Title.Contains(id.ToString()) || r.OrganizationName.Contains(id.ToString())));
    }

    [Fact]
    public async Task Handler_and_organization_filters_use_exact_and_prefix_match()
    {
        var sample = await api.QueryDbAsync(db => db.Requests.AsNoTracking()
            .Where(r => r.AssignedTo != null).OrderBy(r => r.Id).FirstAsync(Ct));
        var prefix = sample.OrganizationName.Split(' ')[0];

        var page = await GetAsync(
            $"/api/requests?assignedTo={Uri.EscapeDataString(sample.AssignedTo!)}&organizationName={Uri.EscapeDataString(prefix)}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, r =>
        {
            Assert.Equal(sample.AssignedTo, r.AssignedTo);
            Assert.StartsWith(prefix, r.OrganizationName, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Sorting_is_stable_across_pages()
    {
        var first = await GetAsync("/api/requests?sortBy=title&sortDirection=asc&pageSize=50&page=1");
        var second = await GetAsync("/api/requests?sortBy=title&sortDirection=asc&pageSize=50&page=2");

        var both = first.Items.Concat(second.Items).ToList();
        Assert.Equal(100, both.Select(r => r.Id).Distinct().Count());
        AssertOrdered(both, (a, b) =>
        {
            var byTitle = string.CompareOrdinal(a.Title, b.Title);
            return byTitle != 0 || a.Id < b.Id;
        }, compareOnlyEqualTitles: true);
    }

    [Fact]
    public async Task Page_after_the_last_one_is_empty_with_the_total()
    {
        var page = await GetAsync("/api/requests?page=10000&pageSize=100");

        Assert.Empty(page.Items);
        Assert.Equal(await api.QueryDbAsync(db => db.Requests.CountAsync(Ct)), page.TotalCount);
    }

    [Theory]
    [InlineData("pageSize=500", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("createdFrom=2026-02-01&createdTo=2026-01-01", "createdTo")]
    [InlineData("status=99", "status")]
    [InlineData("sortBy=password", "sortBy")]
    [InlineData("createdFrom=yesterday", "createdFrom")]
    public async Task Invalid_parameters_return_400_naming_the_field(string queryString, string field)
    {
        var response = await _client.GetAsync($"/api/requests?{queryString}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiFixture.ReadAsync<ValidationProblemDetails>(response);
        Assert.Contains(problem.Errors.Keys, k => k.StartsWith(field, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PagedResult<RequestListItemDto>> GetAsync(string url)
    {
        var response = await _client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiFixture.ReadAsync<PagedResult<RequestListItemDto>>(response);
    }

    private static void AssertOrdered(
        IReadOnlyList<RequestListItemDto> items, Func<RequestListItemDto, RequestListItemDto, bool> inOrder,
        bool compareOnlyEqualTitles = false)
    {
        for (var i = 1; i < items.Count; i++)
        {
            if (compareOnlyEqualTitles && items[i - 1].Title != items[i].Title)
            {
                continue; // SQL collation order for different titles is checked by SQL Server itself
            }

            Assert.True(inOrder(items[i - 1], items[i]), $"Items {items[i - 1].Id} and {items[i].Id} are out of order.");
        }
    }
}
