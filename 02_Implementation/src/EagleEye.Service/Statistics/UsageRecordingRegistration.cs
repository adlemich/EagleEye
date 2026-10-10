using EagleEye.Service.Data;
using EagleEye.Service.Monitoring;
using EagleEye.Service.UserAccounts;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace EagleEye.Service.Statistics;

/// <summary>Dependency injection of app observation and usage accounting (US-004, ADR-011, ADR-012). Verified manually.</summary>
public static class UsageRecordingRegistration
{
    /// <summary>Registers sessions, session agents, process inspection, naming, accounting and the usage state owner.</summary>
    public static IServiceCollection AddUsageRecording(this IServiceCollection services)
    {
        services.AddSingleton<UsageEventQueue>();
        services.AddSingleton<UsageTracker>();
        services.AddSingleton<IUsageRepository, UsageRepository>();
        services.AddSingleton<IUsageBroadcaster, UsageBroadcaster>();
        services.AddSingleton<UsageService>();
        services.AddSingleton<IUsageService>(sp => sp.GetRequiredService<UsageService>());
        services.AddSingleton<IAccountDataPurger>(sp => sp.GetRequiredService<UsageService>());

        // UserAccountService → purger (UsageService) → UserAccountService: Lazy breaks the construction cycle.
        services.AddSingleton(sp => new Lazy<IAccountDataPurger>(sp.GetRequiredService<IAccountDataPurger>));

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var roots = new ProgramPathRoots(
            systemRoot,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!); // The service folder has a parent.
        services.AddSingleton(new ProgramPathPolicy(roots, letter => ProgramPathPolicy.ToDriveKind(new DriveInfo(letter.ToString()).DriveType)));
        services.AddSingleton<ISessionSource, WtsSessionSource>();
        services.AddSingleton<IProcessInspector, Win32ProcessInspector>();
        services.AddSingleton<IAppMetadataSource, Win32AppMetadataSource>();
        services.AddSingleton<AppNameResolver>();
        services.AddSingleton<IAgentReportSink, AgentReportProcessor>();
        services.AddSingleton<ISessionAgentSupervisor, SessionAgentSupervisor>();
        AddLauncher(services, systemRoot);
        services.AddHostedService<UsageAccountingLoop>();

        if (WindowsServiceHelpers.IsWindowsService())
        {
            // Session and power notifications of the SCM (ADR-012 §2, §3); replaces the default lifetime.
            services.AddSingleton<IHostLifetime, EagleEyeServiceLifetime>();
        }

        return services;
    }

    private static void AddLauncher(IServiceCollection services, string systemRoot)
    {
#if DEBUG
        if (DevSettings.WatchSid() is { } watchedSid)
        {
            // Debug only (T-13): the agent runs as a child process in DEV's session; DEV's account counts as standard.
            services.AddSingleton<IAgentLauncher, DevSessionAgentLauncher>();
            services.AddSingleton<ILocalAccountSource>(_ => new DevLocalAccountSource(new NetApiLocalAccountSource(), watchedSid));
            return;
        }
#endif
        var spec = AgentStartSpec.Create(
            Environment.ProcessPath!, // The service always has a process path.
            systemRoot,
            Path.GetPathRoot(systemRoot)!.TrimEnd('\\'));
        services.AddSingleton(spec);
        services.AddSingleton<IAgentLauncher, SessionAgentLauncher>();
    }
}
