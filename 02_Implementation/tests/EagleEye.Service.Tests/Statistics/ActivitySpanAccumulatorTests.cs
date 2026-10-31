using EagleEye.Service.Statistics;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class ActivitySpanAccumulatorTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Notepad = @"C:\Windows\notepad.exe";
    private static readonly DateOnly Day = new(2026, 10, 31);
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);

    private readonly ActivitySpanAccumulator _spans = new();

    [Fact]
    public void Add_WithinOneSecond_ExtendsTheOpenSpan()
    {
        _spans.Add(Kid, Notepad, Day, T0, T0.AddSeconds(5));
        _spans.Add(Kid.ToLowerInvariant(), Notepad.ToUpperInvariant(), Day, T0.AddSeconds(6), T0.AddSeconds(10));

        Assert.Equal([new SpanUpdate(Kid, Notepad, Day, T0, T0.AddSeconds(10))], _spans.Take(Day));
    }

    [Fact]
    public void Add_Gap_StartsANewSpan()
    {
        _spans.Add(Kid, Notepad, Day, T0, T0.AddSeconds(5));
        _spans.Take(Day);

        _spans.Add(Kid, Notepad, Day, T0.AddSeconds(7), T0.AddSeconds(12));

        Assert.Equal(T0.AddSeconds(7), _spans.Take(Day).Single().StartUtc);
    }

    [Fact]
    public void Take_GapWithinOneTake_ReturnsBothSpans()
    {
        _spans.Add(Kid, Notepad, Day, T0, T0.AddSeconds(2));
        _spans.Add(Kid, Notepad, Day, T0.AddSeconds(2), T0.AddSeconds(3));
        _spans.Add(Kid, Notepad, Day, T0.AddSeconds(8), T0.AddSeconds(10));

        Assert.Equal([(T0, T0.AddSeconds(3)), (T0.AddSeconds(8), T0.AddSeconds(10))], _spans.Take(Day).Select(s => (s.StartUtc, s.EndUtc)));
    }

    [Fact]
    public void Add_ClockJumpBack_StartsANewSpan()
    {
        _spans.Add(Kid, Notepad, Day, T0, T0.AddMinutes(5));

        _spans.Add(Kid, Notepad, Day, T0.AddMinutes(-10), T0.AddMinutes(-9));

        Assert.Equal([T0, T0.AddMinutes(-10)], _spans.Take(Day).Select(s => s.StartUtc));
    }

    [Fact]
    public void Add_OtherDay_StartsANewSpan()
    {
        _spans.Add(Kid, Notepad, Day, T0, T0.AddSeconds(5));

        _spans.Add(Kid, Notepad, Day.AddDays(1), T0.AddSeconds(5), T0.AddSeconds(8));

        Assert.Equal([Day, Day.AddDays(1)], _spans.Take(Day).Select(s => s.Day));
    }

    [Fact]
    public void Add_EarlierEnd_KeepsTheLaterEnd()
    {
        _spans.Add(Kid, Notepad, Day, T0, T0.AddSeconds(5));

        _spans.Add(Kid, Notepad, Day, T0.AddSeconds(4), T0.AddSeconds(4));

        Assert.Equal(T0.AddSeconds(5), _spans.Take(Day).Single().EndUtc);
    }

    [Fact]
    public void Take_OnlyChangedSpans_AndForgetsOldDays()
    {
        _spans.Add(Kid, Notepad, Day, T0, T0.AddSeconds(5));
        _spans.Add(Kid, "paint.exe", Day, T0, T0.AddSeconds(5));
        _spans.Take(Day);

        _spans.Add(Kid, Notepad, Day, T0.AddSeconds(5), T0.AddSeconds(10));
        Assert.Equal(Notepad, _spans.Take(Day.AddDays(1)).Single().ProgramPath);

        // The open span of the old day was forgotten: the next part starts a new span.
        _spans.Add(Kid, Notepad, Day, T0.AddSeconds(10), T0.AddSeconds(15));
        Assert.Equal(T0.AddSeconds(10), _spans.Take(Day).Single().StartUtc);
    }
}
