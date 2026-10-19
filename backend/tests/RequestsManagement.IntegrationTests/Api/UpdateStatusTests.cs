using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.IntegrationTests.Fixtures;

namespace RequestsManagement.IntegrationTests.Api;

/// <summary>PATCH /api/requests/{id}/status – optimistic concurrency, audit and error codes.</summary>
[Collection(ApiCollection.Name)]
public class UpdateStatusTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_change_returns_new_etag_and_writes_one_history_row()
    {
        var client = api.CreateClient("דוד");
        var id = await api.FindRequestAsync(RequestStatus.New);
        var (before, etag) = await GetAsync(client, id);
        var historyBefore = await HistoryCountAsync(id);

        var response = await PatchAsync(client, id, "InProgress", etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await ApiFixture.ReadAsync<RequestDetailsDto>(response);
        Assert.Equal(RequestStatus.InProgress, after.Status);
        Assert.NotEqual(before.Version, after.Version);
        Assert.Equal($"\"{after.Version}\"", response.Headers.ETag!.Tag);
        Assert.True(after.UpdatedAt > before.UpdatedAt);
        Assert.Equal(new[] { RequestStatus.Waiting, RequestStatus.Completed }, after.AllowedNextStatuses);

        Assert.Equal(historyBefore + 1, await HistoryCountAsync(id));
        var last = await api.QueryDbAsync(db => db.RequestStatusHistory.AsNoTracking()
            .Where(h => h.RequestId == id).OrderByDescending(h => h.Id).FirstAsync(Ct));
        Assert.Equal((RequestStatus.New, RequestStatus.InProgress, "דוד", after.UpdatedAt),
            (last.PreviousStatus, last.NewStatus, last.ChangedBy, last.ChangedAt));
    }

    [Fact]
    public async Task Second_update_with_the_same_etag_gets_409_with_current_state_and_last_change()
    {
        var dana = api.CreateClient("דנה");
        var yossi = api.CreateClient("יוסי");
        var id = await api.FindRequestAsync(RequestStatus.New);
        var (_, etag) = await GetAsync(dana, id);
        var historyBefore = await HistoryCountAsync(id);

        var first = await PatchAsync(dana, id, "InProgress", etag);
        var second = await PatchAsync(yossi, id, "Waiting", etag); // same, now outdated, version

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await ReadProblemAsync(second);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal("InProgress", problem.GetProperty("currentState").GetProperty("status").GetString());
        Assert.Equal("דנה", problem.GetProperty("lastChange").GetProperty("changedBy").GetString());
        Assert.Equal("InProgress", problem.GetProperty("lastChange").GetProperty("newStatus").GetString());

        // Nothing was overwritten: still InProgress, and only Dana's change was recorded.
        Assert.Equal(RequestStatus.InProgress, await StatusAsync(id));
        Assert.Equal(historyBefore + 1, await HistoryCountAsync(id));
    }

    [Fact]
    public async Task Concurrent_updates_with_the_same_etag_let_exactly_one_succeed()
    {
        const int parallel = 5;
        var id = await api.FindRequestAsync(RequestStatus.Waiting);
        var (_, etag) = await GetAsync(api.CreateClient(), id);
        var historyBefore = await HistoryCountAsync(id);

        var responses = await Task.WhenAll(Enumerable.Range(1, parallel).Select(i =>
            PatchAsync(api.CreateClient($"משתמש {i}"), id, i % 2 == 0 ? "InProgress" : "Completed", etag)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(parallel - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(historyBefore + 1, await HistoryCountAsync(id));
    }

    [Fact]
    public async Task Missing_if_match_returns_428_and_changes_nothing()
    {
        var id = await api.FindRequestAsync(RequestStatus.New);

        var response = await PatchAsync(api.CreateClient(), id, "InProgress", etag: null);

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        Assert.Equal(RequestStatus.New, await StatusAsync(id));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"AAAAAAAAB9E=\"")]
    [InlineData("\"garbage\"")]
    public async Task Unusable_if_match_returns_400_and_changes_nothing(string ifMatch)
    {
        var id = await api.FindRequestAsync(RequestStatus.New);

        var response = await PatchAsync(api.CreateClient(), id, "InProgress", ifMatch);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RequestStatus.New, await StatusAsync(id));
    }

    [Fact]
    public async Task Transition_that_is_not_allowed_returns_422_with_the_allowed_statuses()
    {
        var client = api.CreateClient();
        var id = await api.FindRequestAsync(RequestStatus.Completed);
        var (_, etag) = await GetAsync(client, id);

        var response = await PatchAsync(client, id, "Waiting", etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Completed", problem.GetProperty("currentStatus").GetString());
        Assert.Equal(new[] { "InProgress" }, problem.GetProperty("allowedStatuses").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(RequestStatus.Completed, await StatusAsync(id));
    }

    [Fact]
    public async Task Unknown_request_returns_404()
    {
        var response = await PatchAsync(api.CreateClient(), 999_999, "InProgress", "\"AAAAAAAAB9E=\"");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_user_or_unknown_status_returns_400()
    {
        var id = await api.FindRequestAsync(RequestStatus.New);
        var (_, etag) = await GetAsync(api.CreateClient(), id);

        var noUser = await PatchAsync(api.CreateClient(user: null), id, "InProgress", etag);
        var badStatus = await PatchAsync(api.CreateClient(), id, "Archived", etag);
        var noBody = await SendAsync(api.CreateClient(), id, new { }, etag);

        Assert.Equal(HttpStatusCode.BadRequest, noUser.StatusCode);
        Assert.Contains("X-User-Name", (await ApiFixture.ReadAsync<ValidationProblemDetails>(noUser)).Errors.Keys);
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);
        Assert.Equal(RequestStatus.New, await StatusAsync(id));
    }

    private static async Task<(RequestDetailsDto Request, string ETag)> GetAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync($"/api/requests/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ApiFixture.ReadAsync<RequestDetailsDto>(response), response.Headers.ETag!.Tag);
    }

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, int id, string newStatus, string? etag) =>
        SendAsync(client, id, new { newStatus }, etag);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, int id, object body, string? etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/requests/{id}/status")
        {
            Content = JsonContent.Create(body),
        };
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private Task<int> HistoryCountAsync(int id) =>
        api.QueryDbAsync(db => db.RequestStatusHistory.CountAsync(h => h.RequestId == id, Ct));

    private Task<RequestStatus> StatusAsync(int id) =>
        api.QueryDbAsync(db => db.Requests.Where(r => r.Id == id).Select(r => r.Status).SingleAsync(Ct));
}
