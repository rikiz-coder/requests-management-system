using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.Application.Requests.Exceptions;

namespace RequestsManagement.UnitTests.Requests;

public class ServiceRequestTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 1, 2, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Allowed_change_updates_status_and_time_and_records_history()
    {
        var request = NewRequest(RequestStatus.New);

        request.ChangeStatus(RequestStatus.InProgress, "דוד", Now);

        Assert.Equal(RequestStatus.InProgress, request.Status);
        Assert.Equal(Now, request.UpdatedAt);
        var entry = Assert.Single(request.StatusHistory);
        Assert.Equal((RequestStatus.New, RequestStatus.InProgress, "דוד", Now, 7),
            (entry.PreviousStatus, entry.NewStatus, entry.ChangedBy, entry.ChangedAt, entry.RequestId));
    }

    [Fact]
    public void Forbidden_change_throws_and_changes_nothing()
    {
        var request = NewRequest(RequestStatus.Completed);

        var ex = Assert.Throws<InvalidStatusTransitionException>(() =>
            request.ChangeStatus(RequestStatus.Waiting, "דוד", Now));

        Assert.Equal(RequestStatus.Completed, ex.CurrentStatus);
        Assert.Equal(new[] { RequestStatus.InProgress }, ex.AllowedStatuses);
        Assert.Equal(RequestStatus.Completed, request.Status);
        Assert.Equal(Created, request.UpdatedAt);
        Assert.Empty(request.StatusHistory);
    }

    private static ServiceRequest NewRequest(RequestStatus status) => new()
    {
        Id = 7,
        Title = "בקשה",
        OrganizationName = "ארגון",
        Status = status,
        Priority = RequestPriority.Medium,
        CreatedAt = Created,
        UpdatedAt = Created,
    };
}
