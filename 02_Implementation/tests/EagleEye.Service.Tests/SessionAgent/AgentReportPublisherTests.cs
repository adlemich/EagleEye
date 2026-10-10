using EagleEye.Service.SessionAgent;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.Service.Tests.SessionAgent;

public sealed class AgentReportPublisherTests
{
    private static readonly IReadOnlyList<AgentApp> One = [new AgentApp(5, AgentAppKind.Window)];
    private static readonly IReadOnlyList<AgentApp> Two = [new AgentApp(5, AgentAppKind.Window), new AgentApp(6, AgentAppKind.Window)];

    private readonly FakeTimeProvider _time = new();
    private readonly AgentReportPublisher _publisher;

    public AgentReportPublisherTests()
    {
        _publisher = new AgentReportPublisher(_time);
    }

    [Fact]
    public void Next_FirstScan_Emits()
    {
        Assert.Equal("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"Window"}]}""", _publisher.Next(One, false));
    }

    [Fact]
    public void Next_UnchangedBeforeHeartbeat_EmitsNothing()
    {
        _publisher.Next(One, false);
        _time.Advance(TimeSpan.FromSeconds(4.9));

        Assert.Null(_publisher.Next([new AgentApp(5, AgentAppKind.Window)], false));
    }

    [Fact]
    public void Next_UnchangedAtHeartbeat_EmitsWithNextSequence()
    {
        _publisher.Next(One, false);
        _time.Advance(AgentReportPublisher.HeartbeatInterval);

        Assert.StartsWith("""{"seq":2,""", _publisher.Next(One, false), StringComparison.Ordinal);
    }

    [Fact]
    public void Next_ChangedSet_EmitsAtOnce()
    {
        _publisher.Next(One, false);

        Assert.StartsWith("""{"seq":2,""", _publisher.Next(Two, false), StringComparison.Ordinal);
    }

    [Fact]
    public void Next_TruncationChanged_EmitsAtOnce()
    {
        _publisher.Next(One, false);

        Assert.Contains("\"truncated\":true", _publisher.Next(One, true), StringComparison.Ordinal);
    }

    [Fact]
    public void Next_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _publisher.Next(null!, false));
    }
}
