using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using RequestsManagement.Application.Requests.Abstractions;
using RequestsManagement.Application.Requests.Contracts;

namespace RequestsManagement.Infrastructure.Caching;

/// <summary>
/// In-process cache (IMemoryCache) for the summary of the unfiltered view.
/// Expiration: a safety net (default 60 seconds), also for changes that did not go through the API.
/// Invalidation: every saved status change removes the entry, so the next read is fresh.
/// With several server instances this would be replaced by IDistributedCache (Redis) – see README.
/// </summary>
internal sealed class MemorySummaryCache(IMemoryCache cache, IConfiguration configuration) : ISummaryCache
{
    private const string Key = "requests:summary:unfiltered";

    private const int DefaultTimeToLiveSeconds = 60;

    private readonly TimeSpan _timeToLive = TimeSpan.FromSeconds(
        int.TryParse(configuration["Cache:SummaryTtlSeconds"], out var seconds) && seconds > 0
            ? seconds
            : DefaultTimeToLiveSeconds);

    public async Task<RequestSummaryDto> GetOrCreateAsync(
        Func<CancellationToken, Task<RequestSummaryDto>> factory, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key, out RequestSummaryDto? cached) && cached is not null)
        {
            return cached;
        }

        var summary = await factory(cancellationToken);
        cache.Set(Key, summary, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _timeToLive });
        return summary;
    }

    public void Invalidate() => cache.Remove(Key);
}
