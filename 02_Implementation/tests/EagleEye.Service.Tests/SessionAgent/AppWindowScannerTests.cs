using EagleEye.Service.SessionAgent;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.Service.Tests.SessionAgent;

public sealed class AppWindowScannerTests
{
    [Fact]
    public void Scan_MergesDuplicatesSortsAndSkipsNonApps()
    {
        WindowInfo[] windows =
        [
            new(1, 30, "A", true, 10, 10, false, 0, 0, null, "b.exe"),
            new(2, 10, "A", true, 10, 10, false, 0, 0, null, "a.exe"),
            new(3, 30, "B", true, 10, 10, false, 0, 0, null, "b.exe"),
            new(4, 20, "A", false, 10, 10, false, 0, 0, null, "c.exe"),
            new(5, 640, AppWindowRule.StoreFrameClass, true, 10, 10, false, 0, 0, 812, "ApplicationFrameHost.exe"),
            new(6, 640, AppWindowRule.StoreFrameClass, true, 10, 10, false, 0, 0, 812, "ApplicationFrameHost.exe"),
        ];

        Assert.Equal(
            [new AgentApp(10, AgentAppKind.Window), new AgentApp(30, AgentAppKind.Window), new AgentApp(812, AgentAppKind.StoreApp, 640)],
            AppWindowScanner.Scan(windows));
    }

    [Fact]
    public void Scan_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AppWindowScanner.Scan(null!));
    }
}
