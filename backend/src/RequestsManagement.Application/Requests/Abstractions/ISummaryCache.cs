using RequestsManagement.Application.Requests.Contracts;

namespace RequestsManagement.Application.Requests.Abstractions;

/// <summary>Cache for the summary of the unfiltered view. Invalidated on every saved status change.</summary>
public interface ISummaryCache
{
    Task<RequestSummaryDto> GetOrCreateAsync(Func<CancellationToken, Task<RequestSummaryDto>> factory, CancellationToken cancellationToken);

    void Invalidate();
}
