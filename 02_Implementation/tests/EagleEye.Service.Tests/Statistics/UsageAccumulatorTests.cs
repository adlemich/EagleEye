using EagleEye.Service.Statistics;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class UsageAccumulatorTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Path = @"C:\Windows\notepad.exe";
    private static readonly DateOnly Today = new(2026, 10, 8);

    private readonly UsageAccumulator _usage = new();

    [Fact]
    public void Emit_WholeSecondsOnly_RemainderCarried()
    {
        _usage.Add(Kid, Path, Today, TimeSpan.FromSeconds(4.6));
        var first = _usage.Emit(Today);
        _usage.Add(Kid, Path, Today, TimeSpan.FromSeconds(4.6));

        Assert.Equal([new UsageCredit(Kid, Path, Today, 4)], first);
        Assert.Equal([new UsageCredit(Kid, Path, Today, 5)], _usage.Emit(Today));
    }

    [Fact]
    public void Emit_NothingNew_Empty()
    {
        _usage.Add(Kid, Path, Today, TimeSpan.FromSeconds(3));
        _usage.Emit(Today);

        Assert.Empty(_usage.Emit(Today));
    }

    [Fact]
    public void Add_KeysIgnoreCaseOfSidAndPath()
    {
        _usage.Add(Kid, Path, Today, TimeSpan.FromSeconds(0.6));
        _usage.Add(Kid.ToLowerInvariant(), Path.ToUpperInvariant(), Today, TimeSpan.FromSeconds(0.6));

        Assert.Equal(1, _usage.Emit(Today).Single().Seconds);
    }

    [Fact]
    public void Add_DifferentAccountPathOrDay_Separate()
    {
        _usage.Add(Kid, Path, Today, TimeSpan.FromSeconds(1));
        _usage.Add("S-1-5-21-1-2-3-1004", Path, Today, TimeSpan.FromSeconds(1));
        _usage.Add(Kid, @"C:\x\game.exe", Today, TimeSpan.FromSeconds(1));
        _usage.Add(Kid, Path, Today.AddDays(-1), TimeSpan.FromSeconds(1));

        Assert.Equal(4, _usage.Emit(Today.AddDays(-1)).Count);
    }

    [Fact]
    public void Emit_ForgetsOldDaysAfterEmitting()
    {
        _usage.Add(Kid, Path, Today.AddDays(-2), TimeSpan.FromSeconds(1.5));
        _usage.Emit(Today.AddDays(-1));
        _usage.Add(Kid, Path, Today.AddDays(-2), TimeSpan.FromSeconds(0.6));

        // The forgotten 0.5 s remainder is not combined with the new 0.6 s.
        Assert.Empty(_usage.Emit(Today.AddDays(-1)));
    }
}
