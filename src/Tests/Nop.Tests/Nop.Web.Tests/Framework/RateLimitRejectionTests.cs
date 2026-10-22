using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Http;
using Nop.Web.Framework.Infrastructure;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Framework;

[TestFixture]
public class RateLimitRejectionTests
{
    [SetUp]
    public void SetUp()
    {
        RateLimitAttemptTracker.Clear();
    }

    [Test]
    public async Task OnRejectedAsync_ApiRequest_ReturnsJson429WithRetryAfter()
    {
        var httpContext = CreateHttpContext(
            path: "/api/orders",
            host: "example.test",
            accept: "application/json");

        await RateLimitRejection.OnRejectedAsync(
            new OnRejectedContext { HttpContext = httpContext, Lease = null },
            CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        httpContext.Response.Headers.RetryAfter.ToString().Should().Be("1");
        httpContext.Response.ContentType.Should().Contain("application/json");

        var body = await ReadBodyAsync(httpContext);
        body.Should().Contain(RateLimitDefaults.ErrorCode);
        body.Should().Contain("retryAfterSeconds");
    }

    [Test]
    public async Task OnRejectedAsync_SecondCall_IncreasesRetryAfter()
    {
        var first = CreateHttpContext("/api/orders", "example.test", "application/json");
        var second = CreateHttpContext("/api/orders", "example.test", "application/json");

        await RateLimitRejection.OnRejectedAsync(
            new OnRejectedContext { HttpContext = first, Lease = null },
            CancellationToken.None);
        await RateLimitRejection.OnRejectedAsync(
            new OnRejectedContext { HttpContext = second, Lease = null },
            CancellationToken.None);

        second.Response.Headers.RetryAfter.ToString().Should().Be("2");
    }

    [Test]
    public async Task OnRejectedAsync_HtmlRequest_ReturnsPlainText()
    {
        var httpContext = CreateHttpContext(
            path: "/cart",
            host: "example.test",
            accept: "text/html");

        await RateLimitRejection.OnRejectedAsync(
            new OnRejectedContext { HttpContext = httpContext, Lease = null },
            CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        httpContext.Response.ContentType.Should().Contain("text/plain");

        var body = await ReadBodyAsync(httpContext);
        body.Should().Contain("Too many requests");
    }

    private static DefaultHttpContext CreateHttpContext(string path, string host, string accept)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = path;
        httpContext.Request.Host = new HostString(host);
        httpContext.Request.Headers.Accept = accept;

        return httpContext;
    }

    private static async Task<string> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
