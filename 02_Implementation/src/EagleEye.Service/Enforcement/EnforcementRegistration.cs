using EagleEye.Service.Data;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>Dependency injection of the break-time enforcement (US-005, ADR-013). Verified manually.</summary>
public static class EnforcementRegistration
{
    /// <summary>Registers gate, runner, process access, history, time-change monitor and the facade for the loop.</summary>
    public static IServiceCollection AddAppEnforcement(this IServiceCollection services)
    {
        services.AddSingleton<IBlockedStartRepository, BlockedStartRepository>();
        services.AddSingleton<ITimeChangeFindingRepository, TimeChangeFindingRepository>();
        services.AddSingleton(new EnforcementIgnoreList(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        services.AddSingleton<IProcessTable, Win32ProcessTable>();
        services.AddSingleton<IProcessTerminator, Win32ProcessTerminator>();
        services.AddSingleton<OpenAppsView>();
        services.AddSingleton<IAppCloser, AppCloser>();
        services.AddSingleton(sp => new EnforcementLog(
            sp.GetRequiredService<ILogger<EnforcementLog>>(), new InstanceLogLimiter(sp.GetRequiredService<TimeProvider>())));
        services.AddSingleton<ICloseSequenceRunner, CloseSequenceRunner>();
        services.AddSingleton<AppStartGate>();
        services.AddSingleton<TimeChangeMonitor>();
        services.AddSingleton<EnforcementHistory>();
        services.AddSingleton<IAccountDataPurger>(sp => sp.GetRequiredService<EnforcementHistory>());
        services.AddSingleton<IAppEnforcement, AppEnforcement>();
        return services;
    }
}
