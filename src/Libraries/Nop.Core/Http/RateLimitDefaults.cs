namespace Nop.Core.Http;

/// <summary>
/// Shared constants for HTTP 429 rate-limit handling
/// </summary>
public static class RateLimitDefaults
{
    /// <summary>
    /// Marker stored on system log entries so the admin rate-limit log can find them
    /// </summary>
    public const string MessageMarker = "Rate limit (HTTP 429)";

    /// <summary>
    /// Machine-readable error code returned to API clients
    /// </summary>
    public const string ErrorCode = "rate_limit_exceeded";

    /// <summary>
    /// Total send attempts, including the first request, before a 429 is returned to the caller
    /// </summary>
    public const int MaxRetryAttempts = 5;

    /// <summary>
    /// Delay in seconds for the first retry. Later retries double this value.
    /// </summary>
    public const int BaseDelaySeconds = 1;

    /// <summary>
    /// Upper bound for the computed backoff and for an honored Retry-After value
    /// </summary>
    public const int MaxDelaySeconds = 32;

    /// <summary>
    /// How long a partition keeps its attempt count after the last rejection
    /// </summary>
    public const int AttemptResetMinutes = 2;
}
