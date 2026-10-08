using EagleEye.Service.SessionAgent;
using EagleEye.Service.UserAccounts;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests;

/// <summary>The Debug-only switches (tests run in Debug; Release builds do not contain them, ADR-011 T-13).</summary>
[Collection(EnvironmentCollection.Name)]
public sealed class DevSettingsTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData(" S-1-5-21-1-2-3-1001 ", "S-1-5-21-1-2-3-1001")]
    public void WatchSid(string? value, string? expected)
    {
        Assert.Equal(expected, WithVariable(DevSettings.WatchSidVariable, value, DevSettings.WatchSid));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("10000", 10000)]
    [InlineData("50000", 50000)]
    [InlineData("50001", 0)]
    [InlineData("-5", 0)]
    [InlineData("abc", 0)]
    public void PortOffset(string? value, int expected)
    {
        Assert.Equal(expected, WithVariable(DevSettings.PortOffsetVariable, value, DevSettings.PortOffset));
    }

    [Theory]
    [InlineData(null, 5080, 5443)]
    [InlineData("10000", 15080, 15443)]
    public void EndpointPorts_Resolve(string? offset, int tray, int parent)
    {
        Assert.Equal(new EndpointPorts(tray, parent), WithVariable(DevSettings.PortOffsetVariable, offset, EndpointPorts.Resolve));
    }

    [Fact]
    public void DevLocalAccountSource_WatchedAccountIsStandardOthersUnchanged()
    {
        var inner = new Mock<ILocalAccountSource>();
        inner.Setup(s => s.GetAccounts()).Returns(
        [
            new LocalAccountInfo("S-1-5-21-1-2-3-1001", "Admin", null, false, true),
            new LocalAccountInfo("S-1-5-21-1-2-3-1002", "papa", null, false, true),
        ]);

        var accounts = new DevLocalAccountSource(inner.Object, "s-1-5-21-1-2-3-1001").GetAccounts();

        Assert.Equal([false, true], accounts.Select(a => a.IsAdmin));
    }

    [Fact]
    public void WindowInfo_RecordKeepsAllFields()
    {
        var window = new WindowInfo(7, 1, "C", true, 2, 3, false, 4, 5, 6, "p.exe");

        Assert.Equal((7, 1, "C", true, 2, 3, false, 4L, 5, (int?)6, "p.exe"),
            (window.Handle, window.ProcessId, window.ClassName, window.IsVisible, window.Width, window.Height, window.HasOwner,
             window.ExStyle, window.Cloaked, window.HostedProcessId, window.ProcessName));
    }

    private static T WithVariable<T>(string name, string? value, Func<T> action)
    {
        var original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }
}

/// <summary>Tests that change process environment variables do not run in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentCollection
{
    public const string Name = "Environment variables";
}
