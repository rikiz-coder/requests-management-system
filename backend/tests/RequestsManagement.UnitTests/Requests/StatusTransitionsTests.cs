using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.UnitTests.Requests;

public class StatusTransitionsTests
{
    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.InProgress)]
    [InlineData(RequestStatus.New, RequestStatus.Waiting)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Waiting)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Completed)]
    [InlineData(RequestStatus.Waiting, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Completed)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress)]
    public void Allowed_transitions_are_allowed(RequestStatus from, RequestStatus to) =>
        Assert.True(StatusTransitions.IsAllowed(from, to));

    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.Completed)]
    [InlineData(RequestStatus.InProgress, RequestStatus.New)]
    [InlineData(RequestStatus.Waiting, RequestStatus.New)]
    [InlineData(RequestStatus.Completed, RequestStatus.Waiting)]
    [InlineData(RequestStatus.Completed, RequestStatus.New)]
    public void Other_transitions_are_rejected(RequestStatus from, RequestStatus to) =>
        Assert.False(StatusTransitions.IsAllowed(from, to));

    [Fact]
    public void A_status_never_transitions_to_itself()
    {
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            Assert.DoesNotContain(status, StatusTransitions.AllowedFrom(status));
        }
    }

    [Fact]
    public void Every_status_has_a_way_forward()
    {
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            Assert.NotEmpty(StatusTransitions.AllowedFrom(status));
        }
    }
}
