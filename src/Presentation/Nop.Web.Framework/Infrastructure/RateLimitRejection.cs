using System.Globalization;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Nop.Core.Http;

namespace Nop.Web.Framework.Infrastructure;

/// <summary>
/// ASP.NET rate limiter <see cref="RateLimiterOptions.OnRejected"/> callback
/// </summary>
public static partial class RateLimitRejection
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Handle a rejected request: set 429, Retry-After, body, and system log
    /// </summary>
    /// <param name="context">Rejection context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var key = RateLimitAttemptTracker.GetPartitionKey(
            httpContext.User.Identity?.Name,
            httpContext.Request.Headers.Host.ToString());
        var attempt = RateLimitAttemptTracker.Increment(key);
        var delaySeconds = RateLimitBackoff.GetDelaySeconds(attempt);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers.RetryAfter = delaySeconds.ToString(CultureInfo.InvariantCulture);

        var method = httpContext.Request.Method;
        var target = httpContext.Request.Path + httpContext.Request.QueryString;
        await RateLimitLog.WriteAsync(
            httpContext.RequestServices,
            RateLimitBackoff.BuildLogMessage(method, target, delaySeconds, attempt, willRetry: false));

        var message = $"Too many requests. Retry after {delaySeconds} seconds.";

        if (IsApiRequest(httpContext.Request))
        {
            httpContext.Response.ContentType = MediaTypeNames.Application.Json;
            var body = new
            {
                status = StatusCodes.Status429TooManyRequests,
                error = RateLimitDefaults.ErrorCode,
                message,
                retryAfterSeconds = delaySeconds,
                attempt
            };
            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(body, _jsonOptions),
                cancellationToken);
        }
        else
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Plain;
            await httpContext.Response.WriteAsync(message, cancellationToken);
        }
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        if (request.Path.Value?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;

        var accept = request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
