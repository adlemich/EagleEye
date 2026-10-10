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

        var serviceConnection = new ServiceConnection(ServiceBaseUrl());
        using (var trayContext = new TrayApplicationContext(serviceConnection))
        {
            _ = Task.Run(() => serviceConnection.ConnectAsync(CancellationToken.None));
            Application.Run(trayContext);
        }

        // Main must stay synchronous for [STAThread]. A bounded wait on a thread-pool task at
        // process shutdown cannot deadlock the (already finished) UI message loop.
        Task.Run(async () => await serviceConnection.DisposeAsync()).Wait(ShutdownTimeout);
    }

    private static string ServiceBaseUrl()
    {
#if DEBUG
        // Debug builds only (US-005 D-5): the port offset of the Debug console service (EAGLEEYE_DEV_PORT_OFFSET), so a
        // DEV smoke check never talks to the service installed on the same PC. Release builds do not contain it.
        if (int.TryParse(Environment.GetEnvironmentVariable("EAGLEEYE_DEV_PORT_OFFSET"), out var offset) && offset is > 0 and <= 50_000)
        {
            return $"http://localhost:{ServiceDefaults.ServicePort + offset}";
        }
#endif
        return ServiceDefaults.LocalBaseUrl;
    }
}
