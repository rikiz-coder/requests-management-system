namespace RequestsManagement.Application.Requests.Entities;

/// <summary>Request priority. Stored as <c>tinyint</c> with explicit values.</summary>
public enum RequestPriority : byte
{
    Low = 0,
    Medium = 1,
    High = 2,
}
