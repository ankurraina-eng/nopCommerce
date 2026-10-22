using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Http;

namespace Nop.Web.Framework.Infrastructure;

/// <summary>
/// Retries outbound HTTP calls that receive HTTP 429 with exponential backoff
/// </summary>
public partial class RateLimitRetryHandler : DelegatingHandler
{
    #region Fields

    protected readonly Func<string, Task> _logAsync;
    protected readonly Func<int, string, TimeSpan> _delaySelector;

    #endregion

    #region Ctor

    /// <summary>
    /// Create a handler with explicit logging and delay selection (tests and custom wiring)
    /// </summary>
    /// <param name="logAsync">Log writer; null becomes a no-op</param>
    /// <param name="delaySelector">Delay for a 1-based attempt and Retry-After header</param>
    public RateLimitRetryHandler(Func<string, Task> logAsync, Func<int, string, TimeSpan> delaySelector = null)
    {
        _logAsync = logAsync ?? (_ => Task.CompletedTask);
        _delaySelector = delaySelector ?? ((attempt, header) => RateLimitBackoff.GetDelay(attempt, header));
    }

    /// <summary>
    /// Create a handler that logs through <see cref="RateLimitLog"/>
    /// </summary>
    /// <param name="scopeFactory">Scope factory used for logging</param>
    public RateLimitRetryHandler(IServiceScopeFactory scopeFactory)
        : this(message => RateLimitLog.WriteAsync(scopeFactory, message))
    {
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Buffer the request body once so each attempt can resend the same content
    /// </summary>
    /// <param name="request">Original request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Buffered content bytes, or null when there is no body</returns>
    protected virtual async Task<byte[]> BufferContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
            return null;

        return await request.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Build a new request message for one send attempt
    /// </summary>
    /// <param name="request">Original request</param>
    /// <param name="contentBytes">Buffered body</param>
    /// <returns>Cloned request</returns>
    protected virtual HttpRequestMessage CreateRequest(HttpRequestMessage request, byte[] contentBytes)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (contentBytes is not null)
        {
            clone.Content = new ByteArrayContent(contentBytes);
            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                        continue;

                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object>(option.Key), option.Value);

        return clone;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Send the request, retrying on HTTP 429 up to <see cref="RateLimitDefaults.MaxRetryAttempts"/>
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var contentBytes = await BufferContentAsync(request, cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            var attemptRequest = CreateRequest(request, contentBytes);
            var response = await base.SendAsync(attemptRequest, cancellationToken);

            if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                return response;

            var retryAfterHeader = response.Headers.TryGetValues("Retry-After", out var values)
                ? values.FirstOrDefault()
                : null;
            var delay = _delaySelector(attempt, retryAfterHeader);
            var delaySeconds = (int)Math.Round(delay.TotalSeconds);
            var target = request.RequestUri?.ToString() ?? string.Empty;
            var willRetry = attempt < RateLimitDefaults.MaxRetryAttempts;

            await _logAsync(RateLimitBackoff.BuildLogMessage(
                request.Method.Method,
                target,
                delaySeconds,
                attempt,
                willRetry));

            if (!willRetry)
                return response;

            response.Dispose();
            attemptRequest.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    #endregion
}
