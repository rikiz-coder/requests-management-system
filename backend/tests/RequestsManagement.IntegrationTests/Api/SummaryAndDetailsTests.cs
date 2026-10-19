using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.IntegrationTests.Fixtures;

namespace RequestsManagement.IntegrationTests.Api;

/// <summary>Summary (and its cache), a single request, history and lookups.</summary>
[Collection(ApiCollection.Name)]
public class SummaryAndDetailsTests(ApiFixture api)
{
    private readonly HttpClient _client = api.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unfiltered_summary_matches_the_database()
    {
        var summary = await GetSummaryAsync("/api/requests/summary");

        var expected = await api.QueryDbAsync(db => db.Requests.GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, Ct));
        Assert.Equal(expected.Values.Sum(), summary.Total);
        Assert.All(summary.ByStatus, s => Assert.Equal(expected.GetValueOrDefault(s.Status), s.Count));
        Assert.Equal(summary.Total, summary.ByPriority.Sum(p => p.Count));
    }

    [Fact]
    public async Task Filtered_summary_uses_the_same_filters_as_the_list()
    {
        var summary = await GetSummaryAsync("/api/requests/summary?priority=High");
        var list = await ApiFixture.ReadAsync<PagedResult<RequestListItemDto>>(
            await _client.GetAsync("/api/requests?priority=High", Ct));

        Assert.Equal(list.TotalCount, summary.Total);
        Assert.Equal(summary.Total, summary.ByPriority.Single(p => p.Priority == RequestPriority.High).Count);
    }

    [Fact]
    public async Task Cached_summary_is_refreshed_right_after_a_status_change()
    {
        var before = await GetSummaryAsync("/api/requests/summary"); // now cached
        var id = await api.FindRequestAsync(RequestStatus.New);
        var etag = (await _client.GetAsync($"/api/requests/{id}", Ct)).Headers.ETag!.Tag;

        var update = new HttpRequestMessage(HttpMethod.Patch, $"/api/requests/{id}/status")
        {
            Content = JsonContent.Create(new { newStatus = "InProgress" }),
        };
        update.Headers.TryAddWithoutValidation("If-Match", etag);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(update, Ct)).StatusCode);

        var after = await GetSummaryAsync("/api/requests/summary");
        Assert.Equal(Count(before, RequestStatus.New) - 1, Count(after, RequestStatus.New));
        Assert.Equal(Count(before, RequestStatus.InProgress) + 1, Count(after, RequestStatus.InProgress));
        Assert.Equal(before.Total, after.Total);
    }

    [Fact]
    public async Task Request_details_include_etag_and_allowed_next_statuses()
    {
        var id = await api.FindRequestAsync(RequestStatus.Waiting);

        var response = await _client.GetAsync($"/api/requests/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = await ApiFixture.ReadAsync<RequestDetailsDto>(response);
        Assert.Equal($"\"{request.Version}\"", response.Headers.ETag!.Tag);
        Assert.Equal(new[] { RequestStatus.InProgress, RequestStatus.Completed }, request.AllowedNextStatuses);
        Assert.Equal(DateTimeKind.Utc, request.CreatedAt.Kind);
    }

    [Fact]
    public async Task Unknown_request_returns_404_problem_details()
    {
        foreach (var url in new[] { "/api/requests/999999", "/api/requests/999999/history" })
        {
            var response = await _client.GetAsync(url, Ct);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(404, (await ApiFixture.ReadAsync<ProblemDetails>(response)).Status);
        }
    }

    [Fact]
    public async Task History_is_newest_first_and_ends_in_the_current_status()
    {
        var id = await api.FindRequestAsync(RequestStatus.Completed);

        var history = await ApiFixture.ReadAsync<List<StatusHistoryEntryDto>>(
            await _client.GetAsync($"/api/requests/{id}/history", Ct));

        Assert.NotEmpty(history);
        Assert.Equal(RequestStatus.Completed, history[0].NewStatus);
        Assert.Equal(RequestStatus.New, history[^1].PreviousStatus);
        Assert.True(history.Zip(history.Skip(1)).All(p => p.First.ChangedAt >= p.Second.ChangedAt));
    }

    [Fact]
    public async Task Lookups_list_handlers_and_organizations()
    {
        var lookups = await ApiFixture.ReadAsync<LookupsDto>(await _client.GetAsync("/api/lookups", Ct));

        Assert.NotEmpty(lookups.Handlers);
        Assert.NotEmpty(lookups.Organizations);
        Assert.Equal(lookups.Handlers.Distinct().Count(), lookups.Handlers.Count);
    }

    private async Task<RequestSummaryDto> GetSummaryAsync(string url)
    {
        var response = await _client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiFixture.ReadAsync<RequestSummaryDto>(response);
    }

    private static int Count(RequestSummaryDto summary, RequestStatus status) =>
        summary.ByStatus.Single(s => s.Status == status).Count;
}
