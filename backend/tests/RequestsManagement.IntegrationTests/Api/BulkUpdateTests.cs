using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.IntegrationTests.Fixtures;

namespace RequestsManagement.IntegrationTests.Api;

/// <summary>POST /api/requests/bulk/status – partial success with an outcome per item.</summary>
[Collection(ApiCollection.Name)]
public class BulkUpdateTests(ApiFixture api)
{
    private readonly HttpClient _client = api.CreateClient("מנהל");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Mixed_batch_updates_what_it_can_and_explains_the_rest()
    {
        // Two valid requests, one that another user changes after it was selected,
        // one that does not exist, and one whose transition is not allowed.
        var validA = await api.FindRequestAsync(RequestStatus.InProgress);
        var validB = await api.FindRequestAsync(RequestStatus.Waiting);
        var changedMeanwhile = await api.FindRequestAsync(RequestStatus.InProgress, validA);
        var notAllowed = await api.FindRequestAsync(RequestStatus.New); // New -> Completed is not allowed

        var versions = new Dictionary<int, string>();
        foreach (var id in new[] { validA, validB, changedMeanwhile, notAllowed })
        {
            versions[id] = (await ApiFixture.ReadAsync<RequestDetailsDto>(await _client.GetAsync($"/api/requests/{id}", Ct))).Version;
        }

        // Someone else moves one of the selected requests in the meantime.
        var other = new HttpRequestMessage(HttpMethod.Patch, $"/api/requests/{changedMeanwhile}/status")
        {
            Content = JsonContent.Create(new { newStatus = "Waiting" }),
        };
        other.Headers.TryAddWithoutValidation("If-Match", $"\"{versions[changedMeanwhile]}\"");
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient("משתמש אחר").SendAsync(other, Ct)).StatusCode);

        var response = await _client.PostAsJsonAsync("/api/requests/bulk/status", new
        {
            newStatus = "Completed",
            items = new object[]
            {
                new { id = validA, version = versions[validA] },
                new { id = 999_999, version = versions[validA] },
                new { id = changedMeanwhile, version = versions[changedMeanwhile] },
                new { id = notAllowed, version = versions[notAllowed] },
                new { id = validB, version = versions[validB] },
            },
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ApiFixture.ReadAsync<BulkUpdateStatusResult>(response);
        Assert.Equal((5, 2, 3), (result.Requested, result.Succeeded, result.Failed));
        Assert.Equal(
            new[]
            {
                (validA, BulkItemOutcome.Updated),
                (999_999, BulkItemOutcome.NotFound),
                (changedMeanwhile, BulkItemOutcome.Conflict),
                (notAllowed, BulkItemOutcome.InvalidTransition),
                (validB, BulkItemOutcome.Updated),
            },
            result.Results.Select(r => (r.Id, r.Outcome)));
        Assert.All(result.Results.Where(r => r.Outcome == BulkItemOutcome.Updated), r => Assert.NotNull(r.Version));
        Assert.All(result.Results.Where(r => r.Outcome != BulkItemOutcome.Updated), r => Assert.False(string.IsNullOrEmpty(r.Error)));

        // Database: only the valid ones changed, each with one audit row by this user.
        var states = await api.QueryDbAsync(db => db.Requests.AsNoTracking()
            .Where(r => new[] { validA, validB, changedMeanwhile, notAllowed }.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Status, Ct));
        Assert.Equal(RequestStatus.Completed, states[validA]);
        Assert.Equal(RequestStatus.Completed, states[validB]);
        Assert.Equal(RequestStatus.Waiting, states[changedMeanwhile]); // the other user's change was kept
        Assert.Equal(RequestStatus.New, states[notAllowed]);

        var audit = await api.QueryDbAsync(db => db.RequestStatusHistory.AsNoTracking()
            .Where(h => h.ChangedBy == "מנהל" && (h.RequestId == validA || h.RequestId == validB))
            .CountAsync(Ct));
        Assert.Equal(2, audit);
    }

    [Fact]
    public async Task Invalid_batches_are_rejected_as_a_whole()
    {
        var id = await api.FindRequestAsync(RequestStatus.InProgress);
        var version = (await ApiFixture.ReadAsync<RequestDetailsDto>(await _client.GetAsync($"/api/requests/{id}", Ct))).Version;

        var empty = await PostAsync(new { newStatus = "Completed", items = Array.Empty<object>() });
        var tooMany = await PostAsync(new
        {
            newStatus = "Completed",
            items = Enumerable.Range(1, 101).Select(i => new { id = i, version }).ToArray(),
        });
        var duplicates = await PostAsync(new { newStatus = "Completed", items = new[] { new { id, version }, new { id, version } } });
        var noUser = await api.CreateClient(user: null).PostAsJsonAsync("/api/requests/bulk/status",
            new { newStatus = "Completed", items = new[] { new { id, version } } }, Ct);

        Assert.All(new[] { empty, tooMany, duplicates, noUser }, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Equal(RequestStatus.InProgress, await api.QueryDbAsync(db =>
            db.Requests.Where(r => r.Id == id).Select(r => r.Status).SingleAsync(Ct)));
    }

    private Task<HttpResponseMessage> PostAsync(object body) =>
        _client.PostAsJsonAsync("/api/requests/bulk/status", body, Ct);
}
