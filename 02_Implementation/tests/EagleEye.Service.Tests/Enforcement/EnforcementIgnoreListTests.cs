using EagleEye.Service.Enforcement;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class EnforcementIgnoreListTests
{
    private readonly EnforcementIgnoreList _list = new(@"C:\Windows");

    [Theory]
    [InlineData(@"C:\Windows\explorer.exe")]
    [InlineData(@"c:\windows\EXPLORER.EXE")]
    [InlineData(@"C:\Windows\System32\ApplicationFrameHost.exe")]
    [InlineData(@"C:\Windows\System32\dwm.exe")]
    [InlineData(@"C:\Windows\System32\conhost.exe")]
    [InlineData(@"C:\Windows\System32\svchost.exe")]
    [InlineData(@"C:\Windows\System32\SecurityHealthSystray.exe")]
    [InlineData(@"C:\Windows\SystemApps\ShellExperienceHost_cw5n1h2txyewy\ShellExperienceHost.exe")]
    [InlineData(@"C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe")]
    [InlineData(@"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe")]
    [InlineData(@"C:\Windows\SystemApps\SxS\MicrosoftWindows.61869836.InpApp_cw5n1h2txyewy\TextInputHost.exe")]
    [InlineData(@"C:\Windows\SystemApps\Microsoft.LockApp_cw5n1h2txyewy\LockApp.exe")]
    public void IsIgnored_ListedSystemPaths_True(string path)
    {
        Assert.True(_list.IsIgnored(path));
    }

    [Theory]
    [InlineData(@"C:\Users\kid1\Desktop\explorer.exe")]
    [InlineData(@"C:\Windows\System32\explorer.exe")]
    [InlineData(@"C:\Games\dwm.exe")]
    [InlineData(@"C:\Users\kid1\SystemApps\LockApp.exe")]
    [InlineData(@"C:\Windows\SystemApps\Foo\Game.exe")]
    [InlineData(@"C:\Windows\SystemApps\..\..\Users\kid1\LockApp.exe")]
    [InlineData(@"C:\Windows\System32\Taskmgr.exe")]
    [InlineData(@"C:\Windows\ImmersiveControlPanel\SystemSettings.exe")]
    [InlineData(@"C:\Windows\System32\osk.exe")]
    [InlineData(@"C:\Windows\System32\notepad.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void IsIgnored_OtherPrograms_False(string? path)
    {
        Assert.False(_list.IsIgnored(path));
    }

    [Fact]
    public void Constructor_EmptyRoot_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new EnforcementIgnoreList(" "));
    }
}
