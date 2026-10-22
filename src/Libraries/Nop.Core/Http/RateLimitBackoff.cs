namespace Nop.Core.Http;

/// <summary>
/// Exponential backoff for HTTP 429 responses
/// </summary>
public static class RateLimitBackoff
{
    /// <summary>
    /// Delay in seconds for a 1-based retry attempt: 1, 2, 4, 8, 16, then <see cref="RateLimitDefaults.MaxDelaySeconds"/>
    /// </summary>
    /// <param name="attempt">1-based retry attempt</param>
    /// <returns>Delay in seconds</returns>
    public static int GetDelaySeconds(int attempt)
    {
        if (attempt < 1)
            attempt = 1;

        var shift = Math.Min(attempt - 1, 5);
        var delay = RateLimitDefaults.BaseDelaySeconds << shift;

        return Math.Min(delay, RateLimitDefaults.MaxDelaySeconds);
    }

    /// <summary>
    /// Prefer a Retry-After header when it is present. Otherwise use exponential backoff.
    /// </summary>
    /// <param name="attempt">1-based retry attempt</param>
    /// <param name="retryAfterHeader">Raw Retry-After header value</param>
    /// <returns>Delay before the next attempt</returns>
    public static TimeSpan GetDelay(int attempt, string retryAfterHeader)
    {
        if (TryParseRetryAfter(retryAfterHeader, out var serverDelay))
            return serverDelay;

        return TimeSpan.FromSeconds(GetDelaySeconds(attempt));
    }

    /// <summary>
    /// Parse a Retry-After header (delta-seconds or HTTP-date)
    /// </summary>
    /// <param name="header">Header value</param>
    /// <param name="delay">Parsed delay, capped at <see cref="RateLimitDefaults.MaxDelaySeconds"/></param>
    /// <returns>True when the header was understood</returns>
    public static bool TryParseRetryAfter(string header, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(header))
            return false;

        if (int.TryParse(header.Trim(), out var seconds) && seconds >= 0)
        {
            delay = TimeSpan.FromSeconds(Math.Min(seconds, RateLimitDefaults.MaxDelaySeconds));
            return true;
        }

        if (DateTimeOffset.TryParse(header, out var date))
        {
            var delta = date - DateTimeOffset.UtcNow;
            if (delta < TimeSpan.Zero)
                delta = TimeSpan.Zero;
            if (delta > TimeSpan.FromSeconds(RateLimitDefaults.MaxDelaySeconds))
                delta = TimeSpan.FromSeconds(RateLimitDefaults.MaxDelaySeconds);

            delay = delta;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Build the system-log message. It always starts with <see cref="RateLimitDefaults.MessageMarker"/>.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="target">Request path or absolute URI</param>
    /// <param name="delaySeconds">Backoff delay in seconds</param>
    /// <param name="attempt">1-based attempt</param>
    /// <param name="willRetry">Whether another attempt will be made</param>
    /// <returns>Log message</returns>
    public static string BuildLogMessage(string method, string target, int delaySeconds, int attempt, bool willRetry)
    {
        var action = willRetry
            ? $"Retrying in {delaySeconds}s (attempt {attempt} of {RateLimitDefaults.MaxRetryAttempts})."
            : $"Rejected. Client should retry after {delaySeconds}s (attempt {attempt}).";

        return $"{RateLimitDefaults.MessageMarker}: {method} {target}. {action}";
    }
}
