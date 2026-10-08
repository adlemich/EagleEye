using EagleEye.Service.SessionAgent;
using Xunit;

namespace EagleEye.Service.Tests.SessionAgent;

public sealed class AppWindowRuleTests
{
    private static readonly WindowInfo Normal = new(1, 100, "Notepad", true, 800, 600, false, 0, 0, null, "notepad.exe");

    [Fact]
    public void Classify_VisibleNormalWindow_IsWindowOfItsProcess()
    {
        Assert.Equal(new AgentApp(100, AgentAppKind.Window), AppWindowRule.Classify(Normal));
    }

    [Fact]
    public void Classify_Minimised_StillCounts()
    {
        // A minimised window stays visible (IsWindowVisible) with a non-empty rectangle (US-004 AC-12).
        Assert.NotNull(AppWindowRule.Classify(Normal with { Width = 160, Height = 28 }));
    }

    [Theory]
    [InlineData(false, 800, 600)]
    [InlineData(true, 0, 600)]
    [InlineData(true, 800, 0)]
    [InlineData(true, -5, 600)]
    public void Classify_InvisibleOrEmpty_IsNone(bool visible, int width, int height)
    {
        Assert.Null(AppWindowRule.Classify(Normal with { IsVisible = visible, Width = width, Height = height }));
    }

    [Fact]
    public void Classify_CloakedByShell_Counts()
    {
        Assert.NotNull(AppWindowRule.Classify(Normal with { Cloaked = AppWindowRule.CloakedByShell }));
    }

    [Theory]
    [InlineData(1)] // DWM_CLOAKED_APP
    [InlineData(4)] // DWM_CLOAKED_INHERITED
    [InlineData(3)] // app and shell
    public void Classify_CloakedOtherwise_IsNone(int cloaked)
    {
        Assert.Null(AppWindowRule.Classify(Normal with { Cloaked = cloaked }));
    }

    [Fact]
    public void Classify_OwnedWindow_IsNone()
    {
        Assert.Null(AppWindowRule.Classify(Normal with { HasOwner = true }));
    }

    [Fact]
    public void Classify_ToolWindow_IsNone()
    {
        Assert.Null(AppWindowRule.Classify(Normal with { ExStyle = AppWindowRule.ToolWindowStyle }));
    }

    [Fact]
    public void Classify_OwnedToolWindowWithAppWindowStyle_Counts()
    {
        var window = Normal with { HasOwner = true, ExStyle = AppWindowRule.ToolWindowStyle | AppWindowRule.AppWindowStyle };

        Assert.NotNull(AppWindowRule.Classify(window));
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Windows.UI.Core.CoreWindow")]
    [InlineData("NotifyIconOverflowWindow")]
    [InlineData("MultitaskingViewFrame")]
    public void Classify_ShellClass_IsNone(string className)
    {
        Assert.Null(AppWindowRule.Classify(Normal with { ClassName = className, ProcessName = "explorer.exe" }));
    }

    [Fact]
    public void Classify_ShellClassWithAppWindowStyle_IsStillNone()
    {
        Assert.Null(AppWindowRule.Classify(Normal with { ClassName = "Shell_TrayWnd", ExStyle = AppWindowRule.AppWindowStyle }));
    }

    [Fact]
    public void Classify_StoreFrameWithHostedProcess_IsStoreAppWithFrameAsHost()
    {
        var frame = Normal with { ProcessId = 640, ClassName = AppWindowRule.StoreFrameClass, HostedProcessId = 812, ProcessName = "ApplicationFrameHost.exe" };

        Assert.Equal(new AgentApp(812, AgentAppKind.StoreApp, 640), AppWindowRule.Classify(frame));
    }

    [Fact]
    public void Classify_StoreFrameWithoutHostedProcess_IsNone()
    {
        Assert.Null(AppWindowRule.Classify(Normal with { ClassName = AppWindowRule.StoreFrameClass, HostedProcessId = null }));
    }

    [Fact]
    public void Classify_ExplorerFolderWindow_IsFileExplorer()
    {
        var window = Normal with { ClassName = AppWindowRule.FileExplorerClass, ProcessName = "EXPLORER.EXE" };

        Assert.Equal(new AgentApp(100, AgentAppKind.FileExplorer), AppWindowRule.Classify(window));
    }

    [Fact]
    public void Classify_OtherAppLikeWindowOfExplorerName_IsExplorerWindow()
    {
        var window = Normal with { ClassName = "SomeGameWindow", ProcessName = "explorer.exe" };

        Assert.Equal(new AgentApp(100, AgentAppKind.ExplorerWindow), AppWindowRule.Classify(window));
    }

    [Fact]
    public void Classify_FolderClassInOtherProcess_IsOrdinaryWindow()
    {
        var window = Normal with { ClassName = AppWindowRule.FileExplorerClass, ProcessName = "game.exe" };

        Assert.Equal(new AgentApp(100, AgentAppKind.Window), AppWindowRule.Classify(window));
    }

    [Fact]
    public void Classify_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AppWindowRule.Classify(null!));
    }
}
