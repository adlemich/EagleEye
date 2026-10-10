using EagleEye.Service.Data;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>Dependency injection of the break-time enforcement (US-005, ADR-013). Verified manually.</summary>
public static class EnforcementRegistration
{
    /// <summary>Registers gate, runner, process access, history, time-change monitor and the facade for the loop.</summary>
    public static IServiceCollection AddBreakTimeEnforcement(this IServiceCollection services)
    {
        services.AddSingleton<IBlockedStartRepository, BlockedStartRepository>();
        services.AddSingleton<ITimeChangeFindingRepository, TimeChangeFindingRepository>();
        services.AddSingleton(new EnforcementIgnoreList(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        services.AddSingleton<IProcessTable, Win32ProcessTable>();
        services.AddSingleton<IProcessTerminator, Win32ProcessTerminator>();
        services.AddSingleton<OpenAppsView>();
        services.AddSingleton<IBlockedAppCloser, BlockedAppCloser>();
        services.AddSingleton(sp => new BlockedStartLog(
            sp.GetRequiredService<ILogger<BlockedStartLog>>(), new InstanceLogLimiter(sp.GetRequiredService<TimeProvider>())));
        services.AddSingleton<IBlockedStartRunner, BlockedStartRunner>();
        services.AddSingleton<BreakTimeGate>();
        services.AddSingleton<TimeChangeMonitor>();
        services.AddSingleton<EnforcementHistory>();
        services.AddSingleton<IAccountDataPurger>(sp => sp.GetRequiredService<EnforcementHistory>());
        services.AddSingleton<IBreakTimeEnforcement, BreakTimeEnforcement>();
        return services;
    }
}
