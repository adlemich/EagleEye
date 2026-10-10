using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class AppUsageRowViewModelTests
{
    [Fact]
    public void Update_NotifiesNameSecondsAndText()
    {
        var row = new AppUsageRowViewModel(1, "Notepad", 59);
        var changes = new List<string?>();
        row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        row.Update("Editor", 120);
        row.Update("Editor", 120);

        Assert.Equal(["DisplayName", "Seconds", "UsageText"], changes);
        Assert.Equal((1L, "Editor", 120L, "00:02"), (row.AppId, row.DisplayName, row.Seconds, row.UsageText));
    }

    [Fact]
    public void Constructor_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AppUsageRowViewModel(1, null!, 0));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(59, "00:00")]
    [InlineData(60, "00:01")]
    [InlineData(3_599, "00:59")]
    [InlineData(3_600, "01:00")]
    [InlineData(86_400, "24:00")]
    [InlineData(-5, "00:00")]
    public void UsageDuration_RoundsDown(long seconds, string expected)
    {
        Assert.Equal(expected, UsageDuration.ToHhMm(seconds));
    }
}
