using AwesomeAssertions;
using Nop.Core.Http;
using NUnit.Framework;

namespace Nop.Tests.Nop.Core.Tests.Http;

[TestFixture]
public class RateLimitBackoffTests
{
    [SetUp]
    public void SetUp()
    {
        RateLimitAttemptTracker.Clear();
    }

    [Test]
    public void GetDelaySeconds_UsesExponentialBackoffCappedAtMax()
    {
        RateLimitBackoff.GetDelaySeconds(1).Should().Be(1);
        RateLimitBackoff.GetDelaySeconds(2).Should().Be(2);
        RateLimitBackoff.GetDelaySeconds(3).Should().Be(4);
        RateLimitBackoff.GetDelaySeconds(4).Should().Be(8);
        RateLimitBackoff.GetDelaySeconds(5).Should().Be(16);
        RateLimitBackoff.GetDelaySeconds(6).Should().Be(32);
    }

    [Test]
    public void GetDelaySeconds_TreatsZeroOrNegativeAsFirstAttempt()
    {
        RateLimitBackoff.GetDelaySeconds(0).Should().Be(1);
        RateLimitBackoff.GetDelaySeconds(-3).Should().Be(1);
    }

    [Test]
    public void GetDelay_PrefersRetryAfterHeaderOverExponential()
    {
        RateLimitBackoff.GetDelay(1, "3").Should().Be(TimeSpan.FromSeconds(3));
    }

    [Test]
    public void GetDelay_CapsRetryAfterAtMaxDelaySeconds()
    {
        RateLimitBackoff.GetDelay(1, "100").Should().Be(TimeSpan.FromSeconds(RateLimitDefaults.MaxDelaySeconds));
    }

    [Test]
    public void GetDelay_FallsBackToExponentialWhenHeaderUnparseable()
    {
        RateLimitBackoff.GetDelay(3, "not-a-delay").Should().Be(TimeSpan.FromSeconds(4));
    }

    [Test]
    public void BuildLogMessage_StartsWithMarkerAndDiffersByWillRetry()
    {
        var retrying = RateLimitBackoff.BuildLogMessage("GET", "/api/orders", 2, 1, willRetry: true);
        var rejected = RateLimitBackoff.BuildLogMessage("GET", "/api/orders", 2, 1, willRetry: false);

        retrying.Should().StartWith(RateLimitDefaults.MessageMarker);
        rejected.Should().StartWith(RateLimitDefaults.MessageMarker);
        retrying.Should().Contain("Retrying");
        rejected.Should().Contain("Rejected");
        retrying.Should().NotBe(rejected);
    }

    [Test]
    public void AttemptTracker_IncrementsAndResets()
    {
        var key = "example.test";

        RateLimitAttemptTracker.Increment(key).Should().Be(1);
        RateLimitAttemptTracker.Increment(key).Should().Be(2);

        RateLimitAttemptTracker.Reset(key);
        RateLimitAttemptTracker.Increment(key).Should().Be(1);
    }
}
