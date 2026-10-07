using System.ServiceProcess;
using EagleEye.Service.Statistics;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Windows service lifetime that also receives session-change and power notifications from the Service Control
/// Manager and forwards them to the accounting loop at once (ADR-012 §2 and §3). Registered only when the process
/// runs as a Windows service. Thin, verified manually.
/// </summary>
public sealed class EagleEyeServiceLifetime : WindowsServiceLifetime
{
    private readonly UsageEventQueue _queue;

    /// <summary>Creates the lifetime.</summary>
    public EagleEyeServiceLifetime(
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor,
        UsageEventQueue queue)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        CanHandleSessionChangeEvent = true;
        CanHandlePowerEvent = true;
    }

    /// <inheritdoc />
    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        SessionChangeKind? kind = changeDescription.Reason switch
        {
            SessionChangeReason.ConsoleConnect => SessionChangeKind.ConsoleConnect,
            SessionChangeReason.ConsoleDisconnect => SessionChangeKind.ConsoleDisconnect,
            SessionChangeReason.RemoteConnect => SessionChangeKind.RemoteConnect,
            SessionChangeReason.RemoteDisconnect => SessionChangeKind.RemoteDisconnect,
            SessionChangeReason.SessionLogon => SessionChangeKind.Logon,
            SessionChangeReason.SessionLogoff => SessionChangeKind.Logoff,
            SessionChangeReason.SessionLock => SessionChangeKind.Lock,
            SessionChangeReason.SessionUnlock => SessionChangeKind.Unlock,
            _ => null,
        };
        if (kind is { } value)
        {
            _queue.Enqueue(new SessionChanged(changeDescription.SessionId, value));
        }

        base.OnSessionChange(changeDescription);
    }

    /// <inheritdoc />
    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
        switch (powerStatus)
        {
            case PowerBroadcastStatus.Suspend:
                _queue.Enqueue(new PowerSuspended());
                break;
            case PowerBroadcastStatus.ResumeSuspend:
            case PowerBroadcastStatus.ResumeAutomatic:
                _queue.Enqueue(new PowerResumed());
                break;
        }

        return base.OnPowerEvent(powerStatus);
    }
}
