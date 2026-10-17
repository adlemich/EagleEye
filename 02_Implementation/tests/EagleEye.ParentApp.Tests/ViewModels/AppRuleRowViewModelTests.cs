using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.Settings;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class AppRuleRowViewModelTests
{
    private static readonly AppRuleDto Editor = new(1, "Editor", "p", 45, BreakTimeDays.Monday);

    private readonly Host _host = new();

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new AppRuleRowViewModel(null!, _host));
        Assert.Throws<ArgumentNullException>(() => new AppRuleRowViewModel(Editor, null!));
        Assert.Throws<ArgumentNullException>(() => new AppRuleRowViewModel(Editor, _host).Update(null!));
    }

    [Fact]
    public void Fields_NullBecomesEmpty_SameValueNotDirty()
    {
        var row = new AppRuleRowViewModel(Editor, _host);
        row.HoursText = "0";
        row.MinutesText = "45";
        Assert.False(row.HasUncommittedText);

        row.HoursText = null!;
        Assert.True(row.HasUncommittedText);
        row.MinutesText = null!;
        Assert.Equal((string.Empty, string.Empty), (row.HoursText, row.MinutesText));
    }

    [Fact]
    public void TypedHours_KeptAgainstSnapshots_MinutesUpdated()
    {
        var row = new AppRuleRowViewModel(Editor, _host);
        row.HoursText = "7";

        row.Update(Editor with { BudgetMinutes = 130 });

        Assert.Equal(("7", "10"), (row.HoursText, row.MinutesText));
    }

    [Fact]
    public async Task TwoPendingWritesOfOneField_ValueKeptUntilBothEnded()
    {
        var first = new TaskCompletionSource<bool>();
        var second = new TaskCompletionSource<bool>();
        var calls = new Queue<TaskCompletionSource<bool>>([first, second]);
        _host.Model.Setup(m => m.SetDayAsync(1, DayOfWeek.Monday, It.IsAny<bool>())).Returns(() => calls.Dequeue().Task);
        var row = new AppRuleRowViewModel(Editor, _host);

        row.Monday = false;
        var firstWrite = row.LastWrite;
        row.Monday = true;
        first.SetResult(true);
        await firstWrite;
        row.Update(Editor with { Days = BreakTimeDays.None });
        Assert.True(row.Monday);

        second.SetResult(true);
        await row.LastWrite;
        Assert.False(row.Monday);
    }

    private sealed class Host : IAppRuleRowHost
    {
        public Mock<IAppRulesModel> Model { get; } = new();

        public void Post(Action action) => action();

        public Task<bool> SendAsync(Func<IAppRulesModel, Task<bool>> write) => write(Model.Object);

        public void ShowProblem(AppBudgetProblem problem)
        {
        }
    }
}

public sealed class WarningRowViewModelTests
{
    private static readonly WarningSettingsDto Settings = new(2, null, 5, "Bald", 1, null);

    private readonly Host _host = new();

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new WarningRowViewModel(WarningLevel.Early, null!, _host));
        Assert.Throws<ArgumentNullException>(() => new WarningRowViewModel(WarningLevel.Early, Settings, null!));
        Assert.Throws<ArgumentNullException>(() => new WarningRowViewModel(WarningLevel.Early, Settings, _host).Update(null!));
    }

    [Fact]
    public void Fields_NullBecomesEmpty_SameValueIgnored()
    {
        var row = new WarningRowViewModel(WarningLevel.Early, Settings, _host);
        row.MinutesText = "5";
        row.Text = "Bald";
        row.MinutesText = null!;
        row.Text = null!;

        Assert.Equal((string.Empty, string.Empty), (row.MinutesText, row.Text));
    }

    [Fact]
    public void TypedMinutes_KeptAgainstSnapshots_TextUpdated()
    {
        var row = new WarningRowViewModel(WarningLevel.Early, Settings, _host);
        row.MinutesText = "9";

        row.Update(Settings with { EarlyMinutes = 7, EarlyText = "Neu" });

        Assert.Equal(("9", "Neu"), (row.MinutesText, row.Text));
    }

    private sealed class Host : IWarningRowHost
    {
        public void Post(Action action) => action();

        public Task<bool> SendAsync(Func<IWarningSettingsModel, Task<bool>> write) => Task.FromResult(true);

        public void ShowProblem(WarningEditProblem problem)
        {
        }
    }
}
