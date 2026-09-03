namespace Echo.Application.Abstractions;

public interface IRateLimiter
{
    bool TryAcquire(string key, int maxCount, TimeSpan window);
}
