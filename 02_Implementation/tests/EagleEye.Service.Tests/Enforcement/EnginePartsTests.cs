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
    private static readonly WarningSettings Settings = new(5, "frueh", 2, null);

    [Fact]
    public void BreakTime_ParentTextAndTimes()
    {
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakTime, "Pause", null, null, 1200, 1439), KidMessageFactory.BreakTime("Pause", new BreakTimeEntryDto(1, true, 1200, 1439, BreakTimeDays.All)));
    }

    [Theory]
    [InlineData(CloseReasons.BudgetUsedUp, KidMessageReason.AppBudgetUsedUp)]
    [InlineData(CloseReasons.NotAllowedToday, KidMessageReason.AppNotAllowedToday)]
    public void AppEnd_ReasonAndBudgetText(string reason, KidMessageReason expected)
    {
        Assert.Equal(new KidMessageDto(expected, null, "Editor", null, null, null), KidMessageFactory.AppEnd(reason, null, "Editor"));
    }

    [Fact]
    public void Warnings_TextOfTheLevelAndMinutesRoundedUp()
    {
        Assert.Equal(new KidMessageDto(KidMessageReason.AppEarlyWarning, "frueh", "Editor", 3, null, null), KidMessageFactory.AppWarning(WarningLevel.Early, Settings, "Editor", 121));
        Assert.Equal(new KidMessageDto(KidMessageReason.AppFinalWarning, null, "Editor", 1, null, null), KidMessageFactory.AppWarning(WarningLevel.Final, Settings, "Editor", 20));
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakEarlyWarning, "frueh", null, 5, 1200, null), KidMessageFactory.BreakWarning(WarningLevel.Early, Settings, 1200, 300));
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakFinalWarning, null, null, 2, 1200, null), KidMessageFactory.BreakWarning(WarningLevel.Final, Settings, 1200, 61));
    }

    [Fact]
    public void Graces_FinalTextAndTheFullFinalMinutes()
    {
        Assert.Equal(new KidMessageDto(KidMessageReason.AppGracePeriod, null, "Editor", 2, null, null), KidMessageFactory.AppGrace(Settings, "Editor"));
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakGracePeriod, null, null, 2, 1200, null), KidMessageFactory.BreakGrace(Settings, 1200));
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
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.AppWarning(WarningLevel.Early, null!, "a", 1));
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.AppWarning(WarningLevel.Early, Settings, null!, 1));
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.BreakWarning(WarningLevel.Early, null!, 1, 1));
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.AppGrace(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.AppGrace(Settings, null!));
        Assert.Throws<ArgumentNullException>(() => KidMessageFactory.BreakGrace(null!, 1));
    }
}

public sealed class MessageOutboxTests
{
    private readonly Mock<IKidMessenger> _messenger = new();
    private readonly List<(string Who, string State)> _logs = [];

    public MessageOutboxTests()
    {
        _messenger.Setup(m => m.ShowAsync(It.IsAny<int>(), It.IsAny<KidMessageDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(KidMessageStates.Shown);
    }

    [Theory]
    [InlineData(KidMessageReason.BreakGracePeriod, 6)]
    [InlineData(KidMessageReason.AppGracePeriod, 5)]
    [InlineData(KidMessageReason.BreakFinalWarning, 4)]
    [InlineData(KidMessageReason.AppFinalWarning, 3)]
    [InlineData(KidMessageReason.BreakEarlyWarning, 2)]
    [InlineData(KidMessageReason.AppEarlyWarning, 1)]
    public void PriorityOf_GraceFinalEarly_BreakBeforeApp(KidMessageReason reason, int expected)
    {
        Assert.Equal(expected, MessageOutbox.PriorityOf(reason));
    }

    [Fact]
    public void Send_MostUrgentSent_OthersReplacedInTheTick()
    {
        var outbox = new MessageOutbox();
        var early = Message(KidMessageReason.AppEarlyWarning);
        var final = Message(KidMessageReason.AppFinalWarning);
        var final2 = Message(KidMessageReason.AppFinalWarning);
        outbox.Add(early, s => _logs.Add(("early", s)));
        outbox.Add(final, s => _logs.Add(("final", s)));
        outbox.Add(final2, s => _logs.Add(("final2", s)));

        outbox.Send(_messenger.Object, 2);

        _messenger.Verify(m => m.ShowAsync(2, final, CancellationToken.None), Times.Once);
        Assert.Equal(
            [("early", KidMessageStates.ReplacedInTick), ("final2", KidMessageStates.ReplacedInTick), ("final", KidMessageStates.Shown)],
            _logs);
    }

    [Fact]
    public void Send_Empty_NothingSent()
    {
        new MessageOutbox().Send(_messenger.Object, 2);

        _messenger.VerifyNoOtherCalls();
    }

    private static KidMessageDto Message(KidMessageReason reason) => new(reason, null, "a", 1, null, null);
}
