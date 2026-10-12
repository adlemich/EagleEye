using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static EagleEye.Service.Tests.Enforcement.EnforcementTestData;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class EnforcementLogTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly TestLogger<EnforcementLog> _logger = new();
    private readonly EnforcementLog _log;

    public EnforcementLogTests()
    {
        _log = new EnforcementLog(_logger, new InstanceLogLimiter(_time));
    }

    [Fact]
    public void Detected_LogsAllAc28Fields()
    {
        Assert.True(_log.Detected(Start()));

        Assert.Equal(
            @"Blocked start: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), process 5120, break time 20:00–23:59 (Mo Tu We Th Fr Sa Su); closing the app.",
            _logger.Messages(LogLevel.Information).Single());
    }

    [Fact]
    public void Detected_BudgetBlockedStart_ReasonRuleAndUsedTime()
    {
        var request = Start() with { Entry = null, Reason = CloseReasons.BudgetUsedUp, Rule = new AppRuleAtClose(45, BreakTimeDays.Monday | BreakTimeDays.Tuesday, 2705) };

        Assert.True(_log.Detected(request));

        Assert.Equal(
            @"Blocked start: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), process 5120, budget used up (rule 0:45, Mo Tu, used 0:45:05); closing the app.",
            _logger.Messages(LogLevel.Information).Single());
    }

    [Fact]
    public void Detected_RuleUnknown_SaysSo()
    {
        _log.Detected(Start() with { Entry = null, Reason = CloseReasons.NotAllowedToday });

        Assert.Contains("not allowed today (rule unknown)", _logger.Messages(LogLevel.Information).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void RunningApp_ClosingAndClosed_NotLimited()
    {
        var request = Start() with { Entry = null, Kind = CloseKinds.RunningApp, Reason = CloseReasons.BudgetUsedUp, AfterGrace = true };
        for (var i = 0; i < InstanceLogLimiter.MaxEntriesPerHour + 1; i++)
        {
            Assert.True(_log.Detected(request));
        }

        _log.Ended(request with { AfterGrace = false, Entry = Entry, Reason = CloseReasons.BreakTime }, "closed gracefully after 0.3 s", "shown", true);

        var messages = _logger.Messages(LogLevel.Information);
        Assert.Equal(@"Closing app: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), budget used up, after grace period.", messages[0]);
        Assert.Equal(
            @"App closed: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), break time 20:00–23:59 (Mo Tu We Th Fr Sa Su): closed gracefully after 0.3 s; message shown.",
            messages[^1]);
    }

    [Fact]
    public void WarningAndGraceTemplates()
    {
        _log.AppWarning("kid1", "Editor", "notepad.exe", WarningLevel.Early, 2, AppEndKind.Budget, "shown");
        _log.AppWarning("kid1", "Editor", "notepad.exe", WarningLevel.Final, 1, AppEndKind.Midnight, "shown");
        _log.AppWarning("kid1", "Editor", "notepad.exe", WarningLevel.Final, 1, AppEndKind.NotAllowedToday, "shown");
        _log.AppWarning("kid1", "Editor", "notepad.exe", WarningLevel.Final, 1, AppEndKind.Break, "shown");
        _log.BreakWarning("kid1", Entry, new DateTime(2026, 10, 12, 20, 0, 0), WarningLevel.Final, 1, "shown (replaced the open message)");
        _log.AppGrace("kid1", "Editor", "notepad.exe", AppEndKind.Budget, 1, new DateTime(2026, 10, 12, 18, 31, 5), "shown");
        _log.BreakGrace("kid1", Entry, new DateTime(2026, 10, 12, 20, 0, 0), 2, 1, new DateTime(2026, 10, 12, 19, 59, 35), "not shown (tray client not connected)");

        Assert.Equal(
            [
                "Warning: account kid1, Editor (notepad.exe), early warning, 2 min left (budget); message shown.",
                "Warning: account kid1, Editor (notepad.exe), final warning, 1 min left (midnight); message shown.",
                "Warning: account kid1, Editor (notepad.exe), final warning, 1 min left (not allowed today); message shown.",
                "Warning: account kid1, Editor (notepad.exe), final warning, 1 min left (break time); message shown.",
                "Warning: account kid1, break time 20:00–23:59 (Mo Tu We Th Fr Sa Su) begins at 20:00, final warning, 1 min left; message shown (replaced the open message).",
                "Grace period: account kid1, Editor (notepad.exe): the rules changed (budget); closes in 1 min at 18:31:05; message shown.",
                "Grace period: account kid1, break time 20:00–23:59 (Mo Tu We Th Fr Sa Su) begins at 20:00; 2 app(s) close in 1 min at 19:59:35; message not shown (tray client not connected).",
            ],
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public void AppRecordFailed_Warning()
    {
        var failure = new InvalidOperationException("db");

        _log.AppRecordFailed(Start(), failure);

        Assert.True(_logger.Has(LogLevel.Warning, failure));
        Assert.Throws<ArgumentNullException>(() => _log.AppRecordFailed(null!, failure));
        Assert.Throws<ArgumentNullException>(() => _log.BreakWarning("kid1", null!, DateTime.Now, WarningLevel.Early, 1, "shown"));
        Assert.Throws<ArgumentNullException>(() => _log.BreakGrace("kid1", null!, DateTime.Now, 1, 1, DateTime.Now, "shown"));
    }

    [Fact]
    public void Ended_LogsOutcomeAndMessageState()
    {
        _log.Ended(Start(), "closed gracefully after 0.5 s", "shown", detectionLogged: true);

        Assert.Equal(
            @"Blocked start ended: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), break time 20:00–23:59 (Mo Tu We Th Fr Sa Su): closed gracefully after 0.5 s; message shown.",
            _logger.Messages(LogLevel.Information).Single());
    }

    [Fact]
    public void Ended_DetectionSuppressed_NothingLogged()
    {
        _log.Ended(Start(), "x", "shown", detectionLogged: false);

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Limiter_After30PerHour_SuppressedThenOneSummary()
    {
        var results = Enumerable.Range(0, 33).Select(_ => _log.Detected(Start())).ToList();
        _log.Flush();
        var beforeHour = _logger.Messages(LogLevel.Information).Count;
        _time.Advance(InstanceLogLimiter.Window);
        _log.Flush();

        Assert.Equal((30, 3, 30), (results.Count(r => r), results.Count(r => !r), beforeHour));
        Assert.Equal(
            @"3 further blocked starts of account kid1, Editor (C:\Windows\System32\notepad.exe) were not logged in the last hour.",
            _logger.Messages(LogLevel.Information).Last());
    }

    [Fact]
    public void ErrorsAndWarnings()
    {
        var failure = new InvalidOperationException("db");

        _log.NotEnded(Start(), 2);
        _log.TerminateFailed(Start(), 5120, 5);
        _log.HistoryFailed(Start(), failure);

        Assert.Equal(
            @"The blocked app Editor (C:\Windows\System32\notepad.exe) of account kid1 could not be ended: 2 process(es) still running after 3 rounds.",
            _logger.Messages(LogLevel.Error).Single());
        Assert.Contains(@"Process 5120 of the blocked app Editor (C:\Windows\System32\notepad.exe) could not be terminated (Win32 error 5).", _logger.Messages(LogLevel.Warning));
        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _log.Detected(null!));
        Assert.Throws<ArgumentNullException>(() => _log.Ended(null!, "o", "s", true));
        Assert.Throws<ArgumentNullException>(() => _log.NotEnded(null!, 1));
        Assert.Throws<ArgumentNullException>(() => _log.TerminateFailed(null!, 1, 1));
        Assert.Throws<ArgumentNullException>(() => _log.HistoryFailed(null!, new InvalidOperationException()));
    }
}
