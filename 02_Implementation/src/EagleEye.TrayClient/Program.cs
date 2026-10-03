using EagleEye.Shared.Constants;
using EagleEye.TrayClient.Communication;
using EagleEye.TrayClient.UI;

namespace EagleEye.TrayClient;

internal static class Program
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(3);

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        var serviceConnection = new ServiceConnection(ServiceDefaults.LocalBaseUrl);
        using (var trayContext = new TrayApplicationContext(serviceConnection))
        {
            _ = Task.Run(() => serviceConnection.ConnectAsync(CancellationToken.None));
            Application.Run(trayContext);
        }

        // Main must stay synchronous for [STAThread]. A bounded wait on a thread-pool task at
        // process shutdown cannot deadlock the (already finished) UI message loop.
        Task.Run(async () => await serviceConnection.DisposeAsync()).Wait(ShutdownTimeout);
    }
}
