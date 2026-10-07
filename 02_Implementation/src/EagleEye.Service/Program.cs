using EagleEye.Service;
using EagleEye.Service.Certificates;
using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Diagnostics;
using EagleEye.Service.Pairing;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.EventLog;

var paths = ServicePaths.Resolve();
paths.EnsureDirectories();

// Runs as a Windows service when started by the SCM, and as a console app otherwise
// (development mode). UseWindowsService() also sets the content root to the install folder.
var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "EagleEyeService")
    .ConfigureLogging(logging =>
    {
        // Operational events of EagleEye at Information to the Event Log (source "EagleEye").
        // ASP.NET Core request logging stays at Warning, so request lines are never logged (ADR-008 §5).
        logging.Services.Configure<EventLogSettings>(settings => settings.SourceName = PairingCodeEventLog.SourceName);
        logging.AddFilter<EventLogLoggerProvider>("EagleEye", LogLevel.Information);
        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseKestrel(kestrel =>
        {
            // Two endpoints (ADR-008 §1): tray clients over loopback HTTP, parent apps over TLS on all interfaces.
            kestrel.ListenLocalhost(ServiceDefaults.ServicePort);
            var certificate = kestrel.ApplicationServices.GetRequiredService<ICertificateManager>().GetOrCreate();
            kestrel.ListenAnyIP(ServiceDefaults.ParentPort, listen => listen.UseHttps(certificate));
        });

        webBuilder.ConfigureServices(services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IServicePaths>(paths);
            services.AddSingleton<IVersionProvider>(
                new AssemblyVersionProvider(typeof(AssemblyVersionProvider).Assembly));

            services.AddSingleton<ISelfSignedCertificateFactory, SelfSignedCertificateFactory>();
            services.AddSingleton<ICertificateManager, CertificateManager>();

            services.AddSingleton(_ => new ServiceDatabase(SqliteDatabase.BuildConnectionString(paths.DatabasePath)));
            services.AddSingleton<IPairedDeviceRepository, PairedDeviceRepository>();

            services.AddSingleton<IPairingCodeGenerator, PairingCodeGenerator>();
            services.AddSingleton<IPairingTokenService, PairingTokenService>();
            services.AddSingleton<IPairingCodeEventLog, PairingCodeEventLog>();
            services.AddSingleton<ITrayConnectionTracker, TrayConnectionTracker>();
            services.AddSingleton<IParentConnectionRegistry, ParentConnectionRegistry>();
            services.AddSingleton<IPairingCodeNotifier, PairingCodeNotifier>();
            services.AddSingleton<IPairingManager, PairingManager>();

            services.AddSignalR()
                .AddHubOptions<ParentHub>(options => options.AddFilter<PairingAuthorizationHubFilter>());
        });

        webBuilder.Configure(app =>
        {
            app.UseMiddleware<HubEndpointGuard>();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<TrayHub>(HubRoutes.Tray);
                endpoints.MapHub<ParentHub>(HubRoutes.Parent);
            });
        });
    })
    .Build();

// A damaged database must stop the service with a clear log entry (coding guidelines §8.4).
try
{
    await host.Services.GetRequiredService<ServiceDatabase>().InitializeAsync();
}
catch (Exception ex) when (ex is SqliteException or InvalidDataException)
{
    host.Services.GetRequiredService<ILogger<ServiceDatabase>>()
        .LogCritical(ex, "The service database {DatabasePath} could not be opened. The service stops.", paths.DatabasePath);
    return 1;
}

await host.RunAsync();
return 0;
