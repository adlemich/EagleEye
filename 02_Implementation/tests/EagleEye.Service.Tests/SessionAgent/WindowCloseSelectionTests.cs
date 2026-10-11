using EagleEye.Service.SessionAgent;
using Xunit;

namespace EagleEye.Service.Tests.SessionAgent;

public sealed class WindowCloseSelectionTests
{
    private static readonly WindowInfo Notepad = new(1, 100, "Notepad", true, 800, 600, false, 0, 0, null, "notepad.exe");

    [Fact]
    public void Select_AllAppWindowsOfTheTarget()
    {
        WindowInfo[] windows = [Notepad, Notepad with { Handle = 2 }, Notepad with { Handle = 3, ProcessId = 200 }];

        Assert.Equal([(100, (nint)1), (100, (nint)2)], WindowCloseSelection.Select(new HashSet<int> { 100 }, windows));
    }

    [Fact]
    public void Select_WindowsTheAppsRuleIgnores_NotSelected()
    {
        WindowInfo[] windows =
        [
            Notepad with { IsVisible = false },
            Notepad with { HasOwner = true },
            Notepad with { ExStyle = AppWindowRule.ToolWindowStyle },
            Notepad with { ClassName = "Shell_TrayWnd" },
        ];

        Assert.Empty(WindowCloseSelection.Select(new HashSet<int> { 100 }, windows));
    }

    [Fact]
    public void Select_StoreApp_TheFrameWindowOfTheHostedProcess()
    {
        var frame = new WindowInfo(7, 640, AppWindowRule.StoreFrameClass, true, 800, 600, false, 0, 0, 812, "ApplicationFrameHost.exe");

        Assert.Equal([(812, (nint)7)], WindowCloseSelection.Select(new HashSet<int> { 812 }, [frame]));
        Assert.Empty(WindowCloseSelection.Select(new HashSet<int> { 640 }, [frame]));
    }

    [Fact]
    public void Select_FileExplorerWindow_NeverSelected()
    {
        var explorer = new WindowInfo(9, 300, AppWindowRule.FileExplorerClass, true, 800, 600, false, 0, 0, null, "explorer.exe");
        var renamed = explorer with { Handle = 10, ClassName = "GameWindow" };

        Assert.Equal([(300, (nint)10)], WindowCloseSelection.Select(new HashSet<int> { 300 }, [explorer, renamed]));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => WindowCloseSelection.Select(null!, []));
        Assert.Throws<ArgumentNullException>(() => WindowCloseSelection.Select(new HashSet<int>(), null!));
    }
}
