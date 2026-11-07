using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class WarningLedgerTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private readonly WarningLedger _ledger = new();

    [Fact]
    public void Early_ThenFinal_EachOnce()
    {
        var key = WarningLedger.AppKey("S-1", @"C:\a.exe", Today);

        Assert.False(_ledger.IsShown(key, WarningLevel.Early));
        _ledger.MarkShown(key, WarningLevel.Early);
        Assert.Equal((true, false), (_ledger.IsShown(key, WarningLevel.Early), _ledger.IsShown(key, WarningLevel.Final)));
        _ledger.MarkShown(key, WarningLevel.Final);
        _ledger.MarkShown(key, WarningLevel.Early);
        Assert.True(_ledger.IsShown(key, WarningLevel.Final));
    }

    [Fact]
    public void FinalMarksEarly()
    {
        var key = WarningLedger.BreakKey(2, new DateTime(2026, 10, 12, 20, 0, 0));

        _ledger.MarkShown(key, WarningLevel.Final);

        Assert.True(_ledger.IsShown(key, WarningLevel.Early));
    }

    [Fact]
    public void Keys_PerAppCaseInsensitivePerDayPerSessionAndBreak()
    {
        Assert.Equal(WarningLedger.AppKey("s-1", @"c:\A.exe", Today), WarningLedger.AppKey("S-1", @"C:\a.EXE", Today));
        Assert.NotEqual(WarningLedger.AppKey("S-1", "a", Today), WarningLedger.AppKey("S-1", "a", Today.AddDays(1)));
        Assert.NotEqual(WarningLedger.BreakKey(2, new DateTime(2026, 10, 12, 20, 0, 0)), WarningLedger.BreakKey(3, new DateTime(2026, 10, 12, 20, 0, 0)));
        Assert.NotEqual(WarningLedger.BreakKey(2, new DateTime(2026, 10, 12, 20, 0, 0)), WarningLedger.BreakKey(2, new DateTime(2026, 10, 12, 21, 0, 0)));
    }

    [Fact]
    public void Prune_NewDay_ShownAgain()
    {
        var yesterday = WarningLedger.AppKey("S-1", "a", Today.AddDays(-1));
        var today = WarningLedger.AppKey("S-1", "a", Today);
        _ledger.MarkShown(yesterday, WarningLevel.Final);
        _ledger.MarkShown(today, WarningLevel.Final);

        _ledger.Prune(Today);

        Assert.Equal((false, true), (_ledger.IsShown(yesterday, WarningLevel.Early), _ledger.IsShown(today, WarningLevel.Early)));
    }

    [Fact]
    public void AppKey_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => WarningLedger.AppKey(null!, "a", Today));
        Assert.Throws<ArgumentNullException>(() => WarningLedger.AppKey("S-1", null!, Today));
    }
}

public sealed class GracePolicyTests
{
    [Theory]
    [InlineData(0, double.PositiveInfinity, true)]
    [InlineData(20, 120, true)]
    [InlineData(59, 120, true)]
    [InlineData(60, 120, false)]
    [InlineData(30, 20, false)]
    [InlineData(30, 30, false)]
    public void Starts_LessThanTheFinalMinutesAndEarlier(double newLeft, double oldLeft, bool expected)
    {
        Assert.Equal(expected, GracePolicy.Starts(newLeft, oldLeft, 1));
    }

    [Fact]
    public void Threshold_TheFullFinalMinutes()
    {
        Assert.Equal(180, GracePolicy.Threshold(3));
    }
}

public sealed class KidMessageFactoryTests
{

    [Fact]
    public void BreakTime_ParentTextAndTimes()
    {
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakTime, "Pause", null, 1200, 1439), KidMessageFactory.BreakTime("Pause", new BreakTimeEntryDto(1, true, 1200, 1439, BreakTimeDays.All)));
    }

    [Theory]
    [InlineData(CloseReasons.BudgetUsedUp, KidMessageReason.AppBudgetUsedUp)]
    [InlineData(CloseReasons.NotAllowedToday, KidMessageReason.AppNotAllowedToday)]
    public void AppEnd_ReasonAndBudgetText(string reason, KidMessageReason expected)
    {
        Assert.Equal(new KidMessageDto(expected, null, "Editor", null, null), KidMessageFactory.AppEnd(reason, null, "Editor"));
    }


    [Theory]
    [InlineData(0, 1)]
    [InlineData(0.5, 1)]
    [InlineData(60, 1)]
    [InlineData(60.1, 2)]
    [InlineData(300, 5)]
    public void Minutes_CeilingAtLeastOne(double seconds, int expected)
    {
        Assert.Equal(expected, KidMessageFactory.Minutes(seconds));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.BreakTime(null, null!));
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.AppEnd(CloseReasons.BudgetUsedUp, null, null!));

    }
}
