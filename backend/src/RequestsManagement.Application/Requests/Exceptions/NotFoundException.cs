namespace RequestsManagement.Application.Requests.Exceptions;

/// <summary>The requested resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException(int requestId)
    : Exception($"Request {requestId} was not found.")
{
    public int RequestId { get; } = requestId;
}
