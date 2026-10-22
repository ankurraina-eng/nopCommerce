using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Domain.Logging;
using Nop.Data;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Web.Framework.Infrastructure;

/// <summary>
/// Writes rate-limit system log entries without interfering with request handling
/// </summary>
public static partial class RateLimitLog
{
    /// <summary>
    /// Write a warning when the database is installed and warning logs are enabled
    /// </summary>
    /// <param name="serviceProvider">Request or root service provider</param>
    /// <param name="message">Log message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task WriteAsync(IServiceProvider serviceProvider, string message)
    {
        if (serviceProvider is null || string.IsNullOrEmpty(message))
            return;

        try
        {
            if (!DataSettingsManager.IsDatabaseInstalled())
                return;

            var logger = serviceProvider.GetService<ILogger>();
            if (logger is null)
                return;

            if (logger.IsEnabled(LogLevel.Warning))
                await logger.WarningAsync(message);
        }
        catch
        {
            // Logging must never break rate limiting
        }
    }

    /// <summary>
    /// Create a scope and write a warning when the database is installed and warning logs are enabled
    /// </summary>
    /// <param name="scopeFactory">Scope factory</param>
    /// <param name="message">Log message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task WriteAsync(IServiceScopeFactory scopeFactory, string message)
    {
        if (scopeFactory is null || string.IsNullOrEmpty(message))
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            await WriteAsync(scope.ServiceProvider, message);
        }
        catch
        {
            // Logging must never break rate limiting
        }
    }
}
