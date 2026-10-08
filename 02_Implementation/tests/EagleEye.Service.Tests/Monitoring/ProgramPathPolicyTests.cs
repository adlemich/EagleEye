using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class ProgramPathPolicyTests
{
    private static readonly ProgramPathRoots Roots = new(@"C:\Windows", @"C:\Program Files", @"C:\Program Files (x86)\", @"C:\Program Files\EagleEye");

    private readonly ProgramPathPolicy _policy = new(Roots, letter => letter switch
    {
        'C' or 'D' => DriveKind.Fixed,
        'E' => DriveKind.Removable,
        'Z' => DriveKind.Network,
        _ => DriveKind.Other,
    });

    [Theory]
    [InlineData(@"C:\Windows\explorer.exe", PathClass.LocalTrusted)]
    [InlineData(@"c:\windows\system32\notepad.exe", PathClass.LocalTrusted)]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator_1_x64__8wekyb3d8bbwe\CalculatorApp.exe", PathClass.LocalTrusted)]
    [InlineData(@"C:\Program Files (x86)\Steam\steam.exe", PathClass.LocalTrusted)]
    [InlineData(@"\\?\C:\Windows\explorer.exe", PathClass.LocalTrusted)]
    [InlineData(@"C:\Users\kid\Desktop\game.exe", PathClass.LocalUntrusted)]
    [InlineData(@"C:\WindowsEvil\game.exe", PathClass.LocalUntrusted)]
    [InlineData(@"C:\Program Files Evil\game.exe", PathClass.LocalUntrusted)]
    [InlineData(@"D:\Games\game.exe", PathClass.LocalUntrusted)]
    [InlineData(@"E:\game.exe", PathClass.Removable)]
    [InlineData(@"F:\game.exe", PathClass.Removable)]
    [InlineData(@"Z:\share\game.exe", PathClass.Remote)]
    [InlineData(@"\\server\share\game.exe", PathClass.Remote)]
    [InlineData(@"\\?\UNC\server\share\game.exe", PathClass.Remote)]
    [InlineData(@"\\?\unc\server\share\game.exe", PathClass.Remote)]
    [InlineData(@"\\.\PhysicalDrive0", PathClass.Device)]
    [InlineData(@"\??\C:\game.exe", PathClass.Device)]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume3\game.exe", PathClass.Device)]
    [InlineData(@"\\?\Volume{12345678-1234-1234-1234-123456789012}\game.exe", PathClass.Device)]
    [InlineData(null, PathClass.Invalid)]
    [InlineData("", PathClass.Invalid)]
    [InlineData("game.exe", PathClass.Invalid)]
    [InlineData(@"C:game.exe", PathClass.Invalid)]
    [InlineData(@"1:\game.exe", PathClass.Invalid)]
    [InlineData(@"C:\Windows\..\Users\kid\game.exe", PathClass.Invalid)]
    [InlineData(@"C:\Windows\.\explorer.exe", PathClass.Invalid)]
    [InlineData(@"C:\Windows\\explorer.exe", PathClass.Invalid)]
    [InlineData(@"C:\Users\kid\game.exe:stream", PathClass.Invalid)]
    [InlineData(@"C:/Windows/explorer.exe", PathClass.Invalid)]
    [InlineData("C:\\Windows\\explorer.exe\0", PathClass.Invalid)]
    public void Classify(string? path, PathClass expected)
    {
        Assert.Equal(expected, _policy.Classify(path));
    }

    [Fact]
    public void Classify_TooLong_IsInvalid()
    {
        Assert.Equal(PathClass.Invalid, _policy.Classify(@"C:\" + new string('a', ProgramPathPolicy.MaxPathLength)));
    }

    [Theory]
    [InlineData(@"\\?\C:\Users\kid\game.exe", @"C:\Users\kid\game.exe")]
    [InlineData(@"C:\Users\kid\game.exe", @"C:\Users\kid\game.exe")]
    [InlineData(@"\\server\share\game.exe", null)]
    [InlineData(null, null)]
    public void NormalizeLocal(string? path, string? expected)
    {
        Assert.Equal(expected, _policy.NormalizeLocal(path));
    }

    [Theory]
    [InlineData(@"C:\Program Files\EagleEye\TrayClient\EagleEye.TrayClient.exe", true)]
    [InlineData(@"\\?\C:\Program Files\EagleEye\Service\EagleEye.Service.exe", true)]
    [InlineData(@"C:\Users\kid\Desktop\EagleEye.TrayClient.exe", false)]
    [InlineData(@"C:\Program Files\EagleEyeX\EagleEye.TrayClient.exe", false)]
    [InlineData(@"\\server\EagleEye\EagleEye.TrayClient.exe", false)]
    public void IsEagleEyeProgram_ByInstallPathNotName(string path, bool expected)
    {
        Assert.Equal(expected, _policy.IsEagleEyeProgram(path));
    }

    [Theory]
    [InlineData(@"C:\Windows\explorer.exe", true)]
    [InlineData(@"C:\WINDOWS\EXPLORER.EXE", true)]
    [InlineData(@"C:\Users\kid\explorer.exe", false)]
    [InlineData(@"C:\Windows\System32\explorer.exe", false)]
    [InlineData(null, false)]
    public void IsSystemExplorer(string? path, bool expected)
    {
        Assert.Equal(expected, _policy.IsSystemExplorer(path));
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\ApplicationFrameHost.exe", true)]
    [InlineData(@"C:\Users\kid\ApplicationFrameHost.exe", false)]
    public void IsSystemFrameHost(string path, bool expected)
    {
        Assert.Equal(expected, _policy.IsSystemFrameHost(path));
    }

    [Theory]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator_1_x64__8wekyb3d8bbwe", true)]
    [InlineData(@"C:\Program Files\WindowsAppsX\x", false)]
    [InlineData(@"D:\WindowsApps\x", false)]
    [InlineData(null, false)]
    public void IsInWindowsApps(string? path, bool expected)
    {
        Assert.Equal(expected, _policy.IsInWindowsApps(path));
    }

    [Theory]
    [InlineData("S-1-5-18", true)]
    [InlineData("S-1-5-32-544", true)]
    [InlineData(ProgramPathPolicy.TrustedInstallerSid, true)]
    [InlineData("S-1-5-21-1-2-3-1003", false)]
    [InlineData("S-1-5-32-545", false)]
    [InlineData(null, false)]
    public void IsTrustedOwner(string? sid, bool expected)
    {
        Assert.Equal(expected, ProgramPathPolicy.IsTrustedOwner(sid));
    }

    [Theory]
    [InlineData(DriveType.Fixed, DriveKind.Fixed)]
    [InlineData(DriveType.Removable, DriveKind.Removable)]
    [InlineData(DriveType.Network, DriveKind.Network)]
    [InlineData(DriveType.CDRom, DriveKind.Other)]
    [InlineData(DriveType.Ram, DriveKind.Other)]
    [InlineData(DriveType.NoRootDirectory, DriveKind.Other)]
    public void ToDriveKind(DriveType type, DriveKind expected)
    {
        Assert.Equal(expected, ProgramPathPolicy.ToDriveKind(type));
    }
}
