namespace RequestsManagement.Application.Requests.Entities;

/// <summary>Audit row written in the same transaction as every status change.</summary>
public class RequestStatusHistory
{
    public long Id { get; set; }

    public int RequestId { get; set; }

    public RequestStatus PreviousStatus { get; set; }

    public RequestStatus NewStatus { get; set; }

    /// <summary>Time of the change, UTC.</summary>
    public DateTime ChangedAt { get; set; }

    public string ChangedBy { get; set; } = string.Empty;
}
