using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.Tests.Statistics;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

/// <summary>US-010 Decision 2. 2026-10-12 is a Monday; Berlin is UTC+2 that day.</summary>
public sealed class OverlayLineBuilderTests
{
    private static readonly WarningSettings Settings = new(5, 1);
    private static readonly TimeZoneInfo Zone = TestZones.Berlin;
    private static readonly BreakTimeEntryDto Evening = new(3, true, 1200, 1439, BreakTimeDays.All);

    [Fact]
    public void SubjectLine_Budget_OwnTimeRoundedUp_CountingOnlyInUse()
    {
        var subject = Subject(null, new LimitRule(10, BreakTimeDays.All), 300);

        Assert.Equal(new OverlayLineDto(OverlayLineKind.App, "Editor", 300, OverlayLevel.Early, true), OverlayLineBuilder.SubjectLine(subject, null, true, Settings, Local(10, 0), Zone));
        Assert.False(OverlayLineBuilder.SubjectLine(subject, null, false, Settings, Local(10, 0), Zone)!.Counting);
    }

    [Fact]
    public void SubjectLine_GroupIgnoresANearerBreak()
    {
        var subject = Subject(7, new LimitRule(30, BreakTimeDays.All), 0);

        var line = OverlayLineBuilder.SubjectLine(subject, null, true, Settings, Local(19, 57), Zone);

        Assert.Equal(new OverlayLineDto(OverlayLineKind.Group, "Games", 1800, OverlayLevel.Normal, true), line);
    }

    [Fact]
    public void SubjectLine_BeyondTheHorizon_Null()
    {
        Assert.Null(OverlayLineBuilder.SubjectLine(Subject(null, new LimitRule(61, BreakTimeDays.All), 0), null, true, Settings, Local(10, 0), Zone));
    }

    [Fact]
    public void SubjectLine_NotAllowedToday_ZeroExpired_CountingAlsoNotInUse()
    {
        var line = OverlayLineBuilder.SubjectLine(Subject(null, new LimitRule(10, BreakTimeDays.Sunday), 0), null, false, Settings, Local(10, 0), Zone);

        Assert.Equal(new OverlayLineDto(OverlayLineKind.App, "Editor", 0, OverlayLevel.Expired, true), line);
    }

    [Fact]
    public void SubjectLine_AppGrace_GraceLeftRoundedUp_Counting()
    {
        var line = OverlayLineBuilder.SubjectLine(Subject(null, new LimitRule(0, BreakTimeDays.None), 0), 59.2, false, Settings, Local(10, 0), Zone);

        Assert.Equal(new OverlayLineDto(OverlayLineKind.App, "Editor", 60, OverlayLevel.Final, true), line);
    }

    [Fact]
    public void BreakLine_NextBegin_InEffect_Grace_None()
    {
        Assert.Equal(new OverlayLineDto(OverlayLineKind.Break, null, 1800, OverlayLevel.Normal, true), OverlayLineBuilder.BreakLine([Evening], null, Settings, Local(19, 30), Zone));
        Assert.Equal(new OverlayLineDto(OverlayLineKind.Break, null, 0, OverlayLevel.Expired, true), OverlayLineBuilder.BreakLine([Evening], null, Settings, Local(20, 30), Zone));
        Assert.Equal(new OverlayLineDto(OverlayLineKind.Break, null, 45, OverlayLevel.Final, true), OverlayLineBuilder.BreakLine([Evening], 44.5, Settings, Local(20, 0), Zone));
        Assert.Null(OverlayLineBuilder.BreakLine([Evening], null, Settings, Local(18, 59), Zone));
        Assert.Null(OverlayLineBuilder.BreakLine([], null, Settings, Local(18, 59), Zone));
    }

    [Theory]
    [InlineData(-3.0, 0)]
    [InlineData(0.0, 0)]
    [InlineData(0.1, 1)]
    [InlineData(59.0, 59)]
    public void Seconds_CeilingNeverNegative(double seconds, int expected)
    {
        Assert.Equal(expected, OverlayLineBuilder.Seconds(seconds));
    }

    [Fact]
    public void Guards_Throw()
    {
        var subject = Subject(null, LimitRule.Default, 0);
        Assert.Throws<ArgumentNullException>(() => OverlayLineBuilder.SubjectLine(null!, null, true, Settings, Local(10, 0), Zone));
        Assert.Throws<ArgumentNullException>(() => OverlayLineBuilder.SubjectLine(subject, null, true, null!, Local(10, 0), Zone));
        Assert.Throws<ArgumentNullException>(() => OverlayLineBuilder.BreakLine(null!, null, Settings, Local(10, 0), Zone));
        Assert.Throws<ArgumentNullException>(() => OverlayLineBuilder.BreakLine([], null, null!, Local(10, 0), Zone));
        Assert.Throws<ArgumentNullException>(() => OverlayLineBuilder.BreakLine([], null, Settings, Local(10, 0), null!));
    }

    private static LimitSubject Subject(long? groupId, LimitRule rule, long used) => groupId is null
        ? new LimitSubject(LimitSubjectKind.App, @"app|C:\APPS\NOTEPAD.EXE", "Editor", null, rule, used, [@"C:\Apps\notepad.exe"])
        : new LimitSubject(LimitSubjectKind.Group, $"group|{groupId}", "Games", groupId, rule, used, [@"C:\Apps\notepad.exe"]);

    private static DateTimeOffset Local(int hour, int minute) =>
        new DateTimeOffset(2026, 10, 12, hour, minute, 0, TimeSpan.FromHours(2)).ToUniversalTime();
}
