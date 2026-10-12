using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Service.Tests.Statistics;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class TodayUsageLedgerTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\System32\notepad.exe";
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly Mock<IUsageRepository> _repository = new();
    private readonly Mock<IAppRuleService> _appRules = new();
    private readonly TestLogger<TodayUsageLedger> _logger = new();
    private readonly TodayUsageLedger _ledger;

    public TodayUsageLedgerTests()
    {
        _time.SetLocalTimeZone(TestZones.Berlin);
        _appRules.SetupGet(a => a.Current).Returns(AppRulesSnapshot.Empty.With(Kid, AccountAppRules.Empty.WithApp(new AppRuleEntry(7, Notepad, "Editor", 60, BreakTimeDays.All))));
        _repository.Setup(r => r.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetUsageAsync(Kid, Today, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DayAppUsage(Today, 7, "Editor", 605), new DayAppUsage(Today, 99, "Unknown", 50)]);
        _ledger = new TodayUsageLedger(_repository.Object, _appRules.Object, _time, _logger);
    }

    [Fact]
    public async Task EnsureLoaded_TodaysUsageByPath_CaseInsensitive()
    {
        await _ledger.EnsureLoadedAsync([Kid], CancellationToken.None);

        Assert.Equal(605, _ledger.UsedSeconds(Kid.ToLowerInvariant(), Notepad.ToUpperInvariant(), Today));
        Assert.Equal(0, _ledger.UsedSeconds(Kid, @"C:\other.exe", Today));
    }

    [Fact]
    public async Task Apply_AddsCreditsOfLoadedAccountsOnly()
    {
        await _ledger.EnsureLoadedAsync([Kid], CancellationToken.None);

        _ledger.Apply([new UsageCredit(Kid, Notepad, Today, 5), new UsageCredit(Kid2, Notepad, Today, 5)]);
        _ledger.Apply([new UsageCredit(Kid, Notepad.ToLowerInvariant(), Today, 4)]);

        Assert.Equal((614L, 0L), (_ledger.UsedSeconds(Kid, Notepad, Today), _ledger.UsedSeconds(Kid2, Notepad, Today)));
    }

    [Fact]
    public async Task Apply_CreditOfAnotherDay_IgnoredOrKeptForTomorrow()
    {
        await _ledger.EnsureLoadedAsync([Kid], CancellationToken.None);

        _ledger.Apply([new UsageCredit(Kid, Notepad, Today.AddDays(-1), 30), new UsageCredit(Kid, Notepad, Today.AddDays(1), 3)]);

        Assert.Equal((605L, 0L, 3L), (_ledger.UsedSeconds(Kid, Notepad, Today), _ledger.UsedSeconds(Kid, Notepad, Today.AddDays(-1)), _ledger.UsedSeconds(Kid, Notepad, Today.AddDays(1))));
    }

    [Fact]
    public async Task NewDay_StartsAtZeroAndForgetsYesterday()
    {
        await _ledger.EnsureLoadedAsync([Kid], CancellationToken.None);
        _time.Advance(TimeSpan.FromDays(1));

        _ledger.Apply([new UsageCredit(Kid, Notepad, Today.AddDays(1), 5)]);

        Assert.Equal((0L, 5L), (_ledger.UsedSeconds(Kid, Notepad, Today), _ledger.UsedSeconds(Kid, Notepad, Today.AddDays(1))));
    }

    [Fact]
    public async Task EnsureLoaded_OnlyOncePerAccount_AccountLeavingControlForgottenAndReloaded()
    {
        await _ledger.EnsureLoadedAsync([Kid], CancellationToken.None);
        await _ledger.EnsureLoadedAsync([Kid], CancellationToken.None);
        _repository.Verify(r => r.GetUsageAsync(Kid, Today, Today, It.IsAny<CancellationToken>()), Times.Once);

        await _ledger.EnsureLoadedAsync([], CancellationToken.None);
        Assert.Equal(0, _ledger.UsedSeconds(Kid, Notepad, Today));

        await _ledger.EnsureLoadedAsync([Kid], CancellationToken.None);
        Assert.Equal(605, _ledger.UsedSeconds(Kid, Notepad, Today));
    }

    [Fact]
    public async Task EnsureLoaded_Fails_ZeroAndWarningLoggedOnce()
    {
        var failure = new InvalidOperationException("db");
        _repository.Setup(r => r.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        await _ledger.EnsureLoadedAsync([Kid, Kid2], CancellationToken.None);
        _ledger.Apply([new UsageCredit(Kid, Notepad, Today, 5)]);

        Assert.Equal(5, _ledger.UsedSeconds(Kid, Notepad, Today));
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task EnsureLoaded_Cancelled_Propagates()
    {
        _repository.Setup(r => r.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _ledger.EnsureLoadedAsync([Kid], CancellationToken.None));
    }

    [Fact]
    public async Task Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _ledger.Apply(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _ledger.EnsureLoadedAsync(null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => _ledger.UsedSeconds(null!, Notepad, Today));
        Assert.Throws<ArgumentNullException>(() => _ledger.UsedSeconds(Kid, null!, Today));
    }
}

public sealed class CloseRegistryTests
{
    private readonly CloseRegistry _registry = new();

    [Fact]
    public void Add_Find_CaseInsensitivePerSession()
    {
        var entry = Entry(completed: false);
        _registry.Add(2, @"C:\A.exe", entry);

        Assert.Same(entry, _registry.Find(2, @"c:\a.EXE"));
        Assert.True(_registry.IsClosing(2, @"C:\A.exe"));
        Assert.Null(_registry.Find(3, @"C:\A.exe"));
        Assert.Null(_registry.Find(2, @"C:\B.exe"));
    }

    [Fact]
    public void Find_Completed_DroppedAndNull()
    {
        _registry.Add(2, "a", Entry(completed: true));

        Assert.Null(_registry.Find(2, "a"));
        Assert.False(_registry.IsClosing(2, "a"));
    }

    [Fact]
    public void RemoveSession_OnlyThatSession()
    {
        _registry.Add(2, "a", Entry(false));
        _registry.Add(3, "a", Entry(false));

        _registry.RemoveSession(2);

        Assert.Equal((false, true), (_registry.IsClosing(2, "a"), _registry.IsClosing(3, "a")));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _registry.Find(1, null!));
        Assert.Throws<ArgumentNullException>(() => _registry.Add(1, null!, Entry(false)));
        Assert.Throws<ArgumentNullException>(() => _registry.Add(1, "a", null!));
    }

    private static CloseEntry Entry(bool completed)
    {
        var handle = new Mock<ICloseSequenceHandle>();
        handle.SetupGet(h => h.IsCompleted).Returns(completed);
        return new CloseEntry(handle.Object, false, []);
    }
}

public sealed class SessionAppsViewTests
{
    [Fact]
    public void Update_Of_Remove()
    {
        var view = new SessionAppsView();
        ObservedApp[] apps = [new("p", "p.exe", "P", [new ObservedProcess(1, 1)])];

        view.Update(2, "S-1", apps);
        view.Update(3, "S-2", []);

        Assert.Equal(new SessionApps(2, "S-1", apps), view.Of(2));
        Assert.Equal(2, view.Sessions.Count);
        view.Remove(2);
        Assert.Null(view.Of(2));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new SessionAppsView().Update(1, null!, []));
        Assert.Throws<ArgumentNullException>(() => new SessionAppsView().Update(1, "S", null!));
    }
}
