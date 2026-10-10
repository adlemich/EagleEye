using EagleEye.Service;
using EagleEye.Service.Certificates;
using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Diagnostics;
using EagleEye.Service.Pairing;
using EagleEye.Service.Rules;
using EagleEye.Service.SessionAgent;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Data;
using EagleEye.Shared.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.EventLog;

// Agent mode (ADR-011): the service executable started by the service inside a user session. It must not
// touch the log folder, the database or the web host, so this is the very first statement.
if (args.Contains(SessionAgentHost.Argument))
{
    return await SessionAgentHost.RunAsync(args);
}

var paths = ServicePaths.Resolve();
var ports = EndpointPorts.Resolve();
paths.EnsureDirectories();

// The log folder is readable by SYSTEM and Administrators only (US-003 AC-14). The ACL is applied on
// every start; if that fails, no log file is written in this run (never into a folder standard users
// could read). The console-mode override folder of DEV is not protected (it would lock DEV out).
Exception? logProtectionError = null;
if (!paths.IsOverridden)
{
    try
    {
        LogDirectoryProtector.Protect(paths.LogDirectory);
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException
        or System.Security.Principal.IdentityNotMappedException)
    {
        logProtectionError = ex;
    }
}

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

        // Service log file %ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log (ADR-002 note, FR-SVC-100, FR-SVC-103).
        if (logProtectionError is null)
        {
            var options = new RollingFileOptions { Directory = paths.LogDirectory, FilePrefix = paths.LogFilePrefix };
            logging.Services.AddSingleton<ILoggerProvider>(_ => new RollingFileLoggerProvider(options, TimeProvider.System));
            logging.AddFilter<RollingFileLoggerProvider>("EagleEye", LogLevel.Information);
            logging.AddFilter<RollingFileLoggerProvider>("Microsoft", LogLevel.Warning);
            logging.AddFilter<RollingFileLoggerProvider>("System", LogLevel.Warning);
        }
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseKestrel(kestrel =>
        {
            // Two endpoints (ADR-008 §1): tray clients over loopback HTTP, parent apps over TLS on all interfaces.
            kestrel.ListenLocalhost(ports.TrayPort);
            var certificate = kestrel.ApplicationServices.GetRequiredService<ICertificateManager>().GetOrCreate();
            kestrel.ListenAnyIP(ports.ParentPort, listen => listen.UseHttps(certificate));
        });

        webBuilder.ConfigureServices(services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(ports);
            services.AddSingleton<IServicePaths>(paths);
            services.AddSingleton<IVersionProvider>(
                new AssemblyVersionProvider(typeof(AssemblyVersionProvider).Assembly));

            services.AddSingleton<ISelfSignedCertificateFactory, SelfSignedCertificateFactory>();
            services.AddSingleton<ICertificateManager, CertificateManager>();

            services.AddSingleton(_ => new ServiceDatabase(SqliteDatabase.BuildConnectionString(paths.DatabasePath)));
            services.AddSingleton<IPairedDeviceRepository, PairedDeviceRepository>();
            services.AddSingleton<IAccountSelectionRepository, AccountSelectionRepository>();

            services.AddSingleton<IPairingCodeGenerator, PairingCodeGenerator>();
            services.AddSingleton<IPairingTokenService, PairingTokenService>();
            services.AddSingleton<IPairingCodeEventLog, PairingCodeEventLog>();
            services.AddSingleton<ITrayConnectionTracker, TrayConnectionTracker>();
            services.AddSingleton<IParentConnectionRegistry, ParentConnectionRegistry>();
            services.AddSingleton<IPairingCodeNotifier, PairingCodeNotifier>();
            services.AddSingleton<IPairingManager, PairingManager>();

            // State area "UserAccounts" (US-003, ADR-010).
            services.AddSingleton<ILocalAccountSource, NetApiLocalAccountSource>();
            services.AddSingleton<IUserAccountsBroadcaster, UserAccountsBroadcaster>();
            services.AddSingleton<IUserAccountService, UserAccountService>();
            services.AddHostedService<AccountInventoryMonitor>();

            // State area "AccountRules" (US-005, ADR-010): break times and display texts.
            services.AddSingleton<IBreakTimeRepository, BreakTimeRepository>();
            services.AddSingleton<IAccountRulesBroadcaster, AccountRulesBroadcaster>();
            services.AddSingleton<BreakTimeService>();
            services.AddSingleton<IBreakTimeService>(sp => sp.GetRequiredService<BreakTimeService>());
            services.AddSingleton<IAccountDataPurger>(sp => sp.GetRequiredService<BreakTimeService>());

            // App observation and usage accounting (US-004, ADR-011, ADR-012).
            services.AddUsageRecording();

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

if (logProtectionError is not null)
{
    host.Services.GetRequiredService<ILogger<ServicePaths>>().LogWarning(
        logProtectionError,
        "The log folder {LogDirectory} could not be restricted to SYSTEM and Administrators; no log file is written.",
        paths.LogDirectory);
}

// A damaged database must stop the service with a clear log entry (coding guidelines §8.4).
// The account inventory is read before Kestrel accepts connections (US-003 plan, Step 2.5); then instances left
// open by a crash are closed and old usage is purged (US-004 plan, Decision 3).
try
{
    await host.Services.GetRequiredService<ServiceDatabase>().InitializeAsync();
    await host.Services.GetRequiredService<IUserAccountService>().InitializeAsync();
    await host.Services.GetRequiredService<IUsageService>().InitializeAsync();
    await host.Services.GetRequiredService<IBreakTimeService>().InitializeAsync();
}
catch (Exception ex) when (ex is SqliteException or InvalidDataException)
{
    host.Services.GetRequiredService<ILogger<ServiceDatabase>>()
        .LogCritical(ex, "The service database {DatabasePath} could not be opened. The service stops.", paths.DatabasePath);
    return 1;
}

await host.RunAsync();
return 0;
