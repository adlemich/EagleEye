using EagleEye.Service.SessionAgent;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.Service.Tests.SessionAgent;

public sealed class AgentProtocolTests
{
    private static readonly AgentReport Report = new(
        17, false, [new AgentApp(5, AgentAppKind.Window), new AgentApp(8, AgentAppKind.FileExplorer),
            new AgentApp(9, AgentAppKind.ExplorerWindow), new AgentApp(12, AgentAppKind.StoreApp, 640)]);

    [Fact]
    public void Serialize_WritesCompactLine()
    {
        Assert.Equal(
            """{"seq":17,"truncated":false,"apps":[{"pid":5,"kind":"Window"},{"pid":8,"kind":"FileExplorer"},{"pid":9,"kind":"ExplorerWindow"},{"pid":12,"kind":"StoreApp","host":640}]}""",
            AgentProtocol.Serialize(Report));
    }

    [Fact]
    public void TryParse_RoundTrip_GivesSameReport()
    {
        Assert.True(AgentProtocol.TryParse(AgentProtocol.Serialize(Report), out var parsed));
        Assert.Equal((Report.Seq, Report.Truncated), (parsed.Seq, parsed.Truncated));
        Assert.Equal(Report.Apps, parsed.Apps);
    }

    [Fact]
    public void TryParse_EmptyListAndTruncated_IsValid()
    {
        Assert.True(AgentProtocol.TryParse("""{"seq":0,"truncated":true,"apps":[]}""", out var parsed));
        Assert.Equal((0L, true, 0), (parsed.Seq, parsed.Truncated, parsed.Apps.Count));
    }

    [Fact]
    public void TryParse_MaxApps_IsValid()
    {
        var apps = Enumerable.Range(1, AgentProtocol.MaxApps).Select(i => new AgentApp(i, AgentAppKind.Window)).ToList();

        Assert.True(AgentProtocol.TryParse(AgentProtocol.Serialize(new AgentReport(1, false, apps)), out _));
    }

    [Fact]
    public void TryParse_TooManyApps_IsRejected()
    {
        var apps = Enumerable.Range(1, AgentProtocol.MaxApps + 1).Select(i => new AgentApp(i, AgentAppKind.Window)).ToList();

        Assert.False(AgentProtocol.TryParse(AgentProtocol.Serialize(new AgentReport(1, false, apps)), out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"Window"}]""")]
    [InlineData("""{"seq":1,"truncated":false}""")]
    [InlineData("""{"truncated":false,"apps":[]}""")]
    [InlineData("""{"seq":1,"apps":[]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":null}""")]
    [InlineData("""{"seq":-1,"truncated":false,"apps":[]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[],"extra":1}""")]
    [InlineData("""{"seq":1,"seq":2,"truncated":false,"apps":[]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"kind":"Window"}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":"5","kind":"Window"}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":0,"kind":"Window"}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":-3,"kind":"Window"}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"window"}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"Process"}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":0}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"Window","host":3}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"FileExplorer","host":3}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"ExplorerWindow","host":3}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"StoreApp"}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"StoreApp","host":0}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"Window","x":{"y":{"z":[1]}}}]}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[[[[[]]]]]}""")]
    [InlineData("""{"seq":99999999999999999999,"truncated":false,"apps":[]}""")]
    public void TryParse_Invalid_IsRejected(string? line)
    {
        Assert.False(AgentProtocol.TryParse(line, out var report));
        Assert.Null(report);
    }

    [Fact]
    public void Serialize_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentProtocol.Serialize(null!));
    }
}
