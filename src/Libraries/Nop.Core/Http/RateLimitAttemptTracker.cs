using System.Collections.Concurrent;

namespace Nop.Core.Http;

/// <summary>
/// In-memory attempt counts used to grow Retry-After for a rate-limited partition
/// </summary>
public static class RateLimitAttemptTracker
{
    private static readonly ConcurrentDictionary<string, AttemptState> _attempts = new();

    /// <summary>
    /// Increase the attempt count for a partition. The count resets after <see cref="RateLimitDefaults.AttemptResetMinutes"/>.
    /// </summary>
    /// <param name="key">Partition key</param>
    /// <returns>1-based attempt</returns>
    public static int Increment(string key)
    {
        if (string.IsNullOrEmpty(key))
            key = "anonymous";

        var now = DateTime.UtcNow;
        var resetAfter = TimeSpan.FromMinutes(RateLimitDefaults.AttemptResetMinutes);
        var updated = _attempts.AddOrUpdate(
            key,
            _ => new AttemptState(1, now),
            (_, current) => now - current.SeenUtc > resetAfter
                ? new AttemptState(1, now)
                : new AttemptState(current.Attempt + 1, now));

        return updated.Attempt;
    }

    /// <summary>
    /// Forget a partition after a request is allowed through
    /// </summary>
    /// <param name="key">Partition key</param>
    public static void Reset(string key)
    {
        if (!string.IsNullOrEmpty(key))
            _attempts.TryRemove(key, out _);
    }

    /// <summary>
    /// Drop every partition. Used by tests.
    /// </summary>
    public static void Clear()
    {
        _attempts.Clear();
    }

    /// <summary>
    /// Partition key matching the application rate limiter: authenticated name, otherwise host
    /// </summary>
    /// <param name="identityName">Authenticated user name</param>
    /// <param name="host">Request host</param>
    /// <returns>Partition key</returns>
    public static string GetPartitionKey(string identityName, string host)
    {
        if (!string.IsNullOrEmpty(identityName))
            return identityName;

        return string.IsNullOrEmpty(host) ? "anonymous" : host;
    }

    private readonly record struct AttemptState(int Attempt, DateTime SeenUtc);
}
