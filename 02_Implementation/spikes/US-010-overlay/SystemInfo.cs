using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace OverlaySpike;

/// <summary>Facts for the START lines. Registry is only read.</summary>
internal static class SystemInfo
{
    public static string AppVersion() =>
        typeof(SystemInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    public static string WindowsVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var build = key?.GetValue("CurrentBuild") as string ?? "?";
            var family = int.TryParse(build, out var b) && b >= 22000 ? "Windows 11" : "Windows 10";
            return $"{family} {key?.GetValue("EditionID")} {key?.GetValue("DisplayVersion")} build {build}.{key?.GetValue("UBR")} ({Environment.OSVersion.VersionString})";
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return Environment.OSVersion.VersionString;
        }
    }

    public static string UserAndSession() =>
        $"user={Environment.UserDomainName}\\{Environment.UserName}, session={Process.GetCurrentProcess().SessionId}, " +
        $"elevated={Environment.IsPrivilegedProcess}, processors={Environment.ProcessorCount}";

    public static IEnumerable<string> Monitors()
    {
        foreach (var screen in Screen.AllScreens)
        {
            var center = new Native.POINT { X = screen.Bounds.X + (screen.Bounds.Width / 2), Y = screen.Bounds.Y + (screen.Bounds.Height / 2) };
            var monitor = Native.MonitorFromPoint(center, Native.MONITOR_DEFAULTTONEAREST);
            var b = screen.Bounds;
            yield return $"monitor {screen.DeviceName}: bounds=({b.X},{b.Y} {b.Width}x{b.Height}) dpi={Native.MonitorDpi(monitor)} " +
                $"({Native.MonitorDpi(monitor) * 100 / 96}%) refresh={Native.RefreshRate(screen.DeviceName)}Hz primary={screen.Primary}";
        }
    }

    public static string Adapters() => "display adapters: " + string.Join(" | ", Native.DisplayAdapters());
}
