using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.Infrastructure.Persistence.Seeding;

namespace RequestsManagement.UnitTests.Seeding;

public class SeedDataGeneratorTests
{
    private const int Count = 5_000;

    [Fact]
    public void Same_seed_produces_identical_data()
    {
        var first = new SeedDataGenerator().Generate(Count);
        var second = new SeedDataGenerator().Generate(Count);

        Assert.Equal(Fingerprint(first), Fingerprint(second));
    }

    [Fact]
    public void Generates_requested_count_with_sequential_ids()
    {
        var data = new SeedDataGenerator().Generate(Count);

        Assert.Equal(Count, data.Requests.Count);
        Assert.Equal(Enumerable.Range(1, Count), data.Requests.Select(r => r.Id));
    }

    [Fact]
    public void Every_status_and_priority_is_represented()
    {
        var data = new SeedDataGenerator().Generate(Count);

        Assert.Equal(Enum.GetValues<RequestStatus>().Order(), data.Requests.Select(r => r.Status).Distinct().Order());
        Assert.Equal(Enum.GetValues<RequestPriority>().Order(), data.Requests.Select(r => r.Priority).Distinct().Order());
    }

    [Fact]
    public void Fields_respect_schema_limits_and_dates_are_consistent()
    {
        var data = new SeedDataGenerator().Generate(Count);

        Assert.All(data.Requests, r =>
        {
            Assert.InRange(r.Title.Length, 1, RequestFieldLimits.TitleMaxLength);
            Assert.InRange(r.OrganizationName.Length, 1, RequestFieldLimits.OrganizationNameMaxLength);
            Assert.True(r.AssignedTo is null || r.AssignedTo.Length <= RequestFieldLimits.PersonNameMaxLength);
            Assert.Equal(DateTimeKind.Utc, r.CreatedAt.Kind);
            Assert.True(r.UpdatedAt >= r.CreatedAt);
            Assert.True(r.UpdatedAt <= SeedDataGenerator.ReferenceDate);
        });
    }

    [Fact]
    public void History_follows_allowed_transitions_and_ends_in_the_current_status()
    {
        var data = new SeedDataGenerator().Generate(Count);
        var historyByRequest = data.History.ToLookup(h => h.RequestId);

        Assert.All(data.Requests, request =>
        {
            var steps = historyByRequest[request.Id].ToList();

            if (request.Status == RequestStatus.New)
            {
                Assert.Empty(steps);
                Assert.Equal(request.CreatedAt, request.UpdatedAt);
                return;
            }

            Assert.NotEmpty(steps);
            Assert.Equal(RequestStatus.New, steps[0].PreviousStatus);
            for (var i = 0; i < steps.Count; i++)
            {
                Assert.True(StatusTransitions.IsAllowed(steps[i].PreviousStatus, steps[i].NewStatus),
                    $"Request {request.Id}: {steps[i].PreviousStatus} -> {steps[i].NewStatus}");
                if (i > 0)
                {
                    Assert.Equal(steps[i - 1].NewStatus, steps[i].PreviousStatus);
                    Assert.True(steps[i].ChangedAt >= steps[i - 1].ChangedAt);
                }
            }

            Assert.Equal(request.Status, steps[^1].NewStatus);
            Assert.Equal(request.UpdatedAt, steps[^1].ChangedAt);
        });
    }

    private static string Fingerprint(SeedData data) => string.Join('\n',
        data.Requests.Select(r => $"{r.Id}|{r.Title}|{r.OrganizationName}|{r.Status}|{r.Priority}|{r.AssignedTo}|{r.CreatedAt:O}|{r.UpdatedAt:O}")
            .Concat(data.History.Select(h => $"{h.RequestId}|{h.PreviousStatus}|{h.NewStatus}|{h.ChangedAt:O}|{h.ChangedBy}")));
}
