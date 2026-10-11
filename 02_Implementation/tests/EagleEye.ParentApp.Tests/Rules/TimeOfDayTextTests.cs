using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.Rules;

public sealed class TimeOfDayTextTests
{
    [Theory]
    [InlineData("20:00", 1200)]
    [InlineData("09:00", 540)]
    [InlineData("7:30", 450)]
    [InlineData("07:30", 450)]
    [InlineData("730", 450)]
    [InlineData("0730", 450)]
    [InlineData("7", 420)]
    [InlineData("07", 420)]
    [InlineData("20.00", 1200)]
    [InlineData(" 23:59 ", 1439)]
    [InlineData("0", 0)]
    [InlineData("00:00", 0)]
    [InlineData("2359", 1439)]
    public void TryParse_Accepted(string text, int expected)
    {
        Assert.True(TimeOfDayText.TryParse(text, out var minute));
        Assert.Equal(expected, minute);
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("7:5")]
    [InlineData("7:300")]
    [InlineData(":30")]
    [InlineData("123:00")]
    [InlineData("12345")]
    [InlineData("2400")]
    [InlineData("1260")]
    [InlineData("-1:00")]
    [InlineData("1a:00")]
    [InlineData("12:3a")]
    [InlineData("١٢:٠٠")]
    public void TryParse_Rejected(string? text)
    {
        Assert.False(TimeOfDayText.TryParse(text, out _));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(450, "07:30")]
    [InlineData(1439, "23:59")]
    public void Format_HhMm(int minute, string expected)
    {
        Assert.Equal(expected, TimeOfDayText.Format(minute));
    }
}
