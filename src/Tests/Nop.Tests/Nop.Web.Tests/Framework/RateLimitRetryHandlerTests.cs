using System.Net;
using AwesomeAssertions;
using Nop.Core.Http;
using Nop.Web.Framework.Infrastructure;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Framework;

[TestFixture]
public class RateLimitRetryHandlerTests
{
    [Test]
    public async Task SendAsync_SuccessfulResponse_DoesNotLogOrRetry()
    {
        var logs = new List<string>();
        var inner = new StubHandler(HttpStatusCode.OK);
        using var handler = new RateLimitRetryHandler(
            message =>
            {
                logs.Add(message);
                return Task.CompletedTask;
            },
            (_, _) => TimeSpan.Zero)
        {
            InnerHandler = inner
        };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://example.test/ok");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.SendCount.Should().Be(1);
        logs.Should().BeEmpty();
    }

    [Test]
    public async Task SendAsync_RetriesOnceOn429ThenSucceeds()
    {
        var logs = new List<string>();
        var inner = new StubHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        using var handler = new RateLimitRetryHandler(
            message =>
            {
                logs.Add(message);
                return Task.CompletedTask;
            },
            (_, _) => TimeSpan.Zero)
        {
            InnerHandler = inner
        };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://example.test/retry");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.SendCount.Should().Be(2);
        logs.Should().HaveCount(1);
        logs[0].Should().Contain(RateLimitDefaults.MessageMarker);
        logs[0].Should().Contain("Retrying");
    }

    [Test]
    public async Task SendAsync_ExhaustsRetriesOnPersistent429()
    {
        var logs = new List<string>();
        var statuses = Enumerable.Repeat(HttpStatusCode.TooManyRequests, RateLimitDefaults.MaxRetryAttempts).ToArray();
        var inner = new StubHandler(statuses);
        using var handler = new RateLimitRetryHandler(
            message =>
            {
                logs.Add(message);
                return Task.CompletedTask;
            },
            (_, _) => TimeSpan.Zero)
        {
            InnerHandler = inner
        };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://example.test/limited");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        inner.SendCount.Should().Be(RateLimitDefaults.MaxRetryAttempts);
        logs.Should().HaveCount(RateLimitDefaults.MaxRetryAttempts);
        logs[^1].Should().Contain("Rejected");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;

        public StubHandler(params HttpStatusCode[] statuses)
        {
            _statuses = new Queue<HttpStatusCode>(statuses);
        }

        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
