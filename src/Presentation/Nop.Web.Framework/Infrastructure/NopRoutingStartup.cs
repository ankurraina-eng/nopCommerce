using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Http;
using Nop.Core.Infrastructure;

namespace Nop.Web.Framework.Infrastructure;

/// <summary>
/// Represents object for the configuring routing on application startup
/// </summary>
public partial class NopRoutingStartup : INopStartup
{
    /// <summary>
    /// Add and configure any of the middleware
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <param name="configuration">Configuration of the application</param>
    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    /// <param name="application">Builder for configuring an application's request pipeline</param>
    public virtual void Configure(IApplicationBuilder application)
    {
        //add the RoutingMiddleware
        application.UseRouting();

        var commonConfig = Singleton<AppSettings>.Instance.Get<CommonConfig>();
        if (commonConfig.PermitLimit > 0)
        {
            application.UseRateLimiter();

            // Capture the partition key before later middleware authenticates the request.
            // The rate limiter uses the same pre-auth key, so a successful request must reset that key.
            application.Use(async (context, next) =>
            {
                var partitionKey = RateLimitAttemptTracker.GetPartitionKey(
                    context.User.Identity?.Name,
                    context.Request.Headers.Host.ToString());

                await next();

                if (context.Response.StatusCode != StatusCodes.Status429TooManyRequests)
                    RateLimitAttemptTracker.Reset(partitionKey);
            });
        }
    }

    /// <summary>
    /// Gets order of this startup configuration implementation
    /// </summary>
    public int Order => 400; // Routing should be loaded before authentication
}