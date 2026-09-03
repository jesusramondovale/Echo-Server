using System.Collections.Concurrent;
using Echo.Application.Abstractions;

namespace Echo.Infrastructure.Services;

public sealed class InMemoryRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public bool TryAcquire(string key, int maxCount, TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = _windows.AddOrUpdate(
            key,
            _ => new Window(now.Add(window), 1),
            (_, existing) =>
            {
                if (existing.ExpiresAt <= now)
                {
                    return new Window(now.Add(window), 1);
                }

                return existing with { Count = existing.Count + 1 };
            });

        return entry.Count <= maxCount;
    }

    private sealed record Window(DateTimeOffset ExpiresAt, int Count);
}
