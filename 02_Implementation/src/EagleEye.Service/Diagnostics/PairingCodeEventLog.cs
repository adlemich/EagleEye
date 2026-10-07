using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using EagleEye.Shared.Constants;

namespace EagleEye.Service.Diagnostics;

/// <summary>
/// Writes the pairing code to the Application log (event ID 1000, source <c>EagleEye</c>). This is
/// the only place a pairing code is written to a log (FR-SVC-092, ADR-008 §6), and it is written
/// directly, not through <c>ILogger</c>. The text is bilingual, because the service runs as SYSTEM
/// and has no user language. Thin wrapper over the Event Log API, verified manually.
/// </summary>
public sealed class PairingCodeEventLog(ILogger<PairingCodeEventLog> logger) : IPairingCodeEventLog
{
    /// <summary>Event Log source of the service.</summary>
    public const string SourceName = "EagleEye";

    /// <summary>Event ID of the pairing-code entry.</summary>
    public const int PairingCodeEventId = 1000;

    private const string LogName = "Application";
    private const string MessageFormat =
        "EagleEye-Kopplungscode / pairing code: {0} — gültig {1} Minuten / valid for {1} minutes.";

    /// <inheritdoc />
    public void Write(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var message = string.Format(
            CultureInfo.InvariantCulture, MessageFormat, code, (int)PairingRules.CodeLifetime.TotalMinutes);
        try
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, LogName);
            }

            EventLog.WriteEntry(SourceName, message, EventLogEntryType.Information, PairingCodeEventId);
            logger.LogInformation("Pairing code *** written to the Event Log (no tray client connected).");
        }
        catch (Exception ex) when (ex is SecurityException or InvalidOperationException or Win32Exception or ArgumentException)
        {
            // Happens only without admin rights (console mode); the service runs as SYSTEM.
            logger.LogError(ex, "The pairing code could not be written to the Event Log.");
        }
    }
}
