using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.Settings;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Rules;

/// <summary>US-006 models; the shared replica and write logic is covered by <see cref="AccountRulesModelTests"/>.</summary>
public sealed class AppRulesModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";

    private readonly FakeTimeProvider _time = new();
    private readonly ParentHubGateway _gateway;
    private readonly Mock<IParentHubClient> _client = new();
    private readonly AppRulesModel _model;

    public AppRulesModelTests()
    {
        _gateway = new ParentHubGateway(_time);
        _client.Setup(c => c.GetAppRulesAsync(Kid, It.IsAny<CancellationToken>())).ReturnsAsync(Apps(Kid, 5));
        _model = new AppRulesModel(_gateway, _time);
    }

    [Fact]
    public async Task Select_Connected_FetchesTheAppRules()
    {
        await ConnectAndSelectAsync();

        Assert.Equal((AccountsLoadState.Ready, 5L), (_model.LoadState, _model.Snapshot!.Revision));
    }

    [Fact]
    public async Task Broadcast_OfTheSelectedAccount_AppliedOthersIgnored()
    {
        await ConnectAndSelectAsync();

        _client.Raise(c => c.AppRulesChanged += null, Apps(Kid2, 9));
        _client.Raise(c => c.AppRulesChanged += null, Apps(Kid, 7));

        Assert.Equal(7, _model.Snapshot!.Revision);
    }

    [Fact]
    public async Task Writes_DelegatedAndConfirmedByTheAcknowledgedRevision()
    {
        await ConnectAndSelectAsync();
        _client.Setup(c => c.SetAppBudgetAsync(It.IsAny<Guid>(), Kid, 3, AppBudgetPart.Hours, 2, It.IsAny<CancellationToken>()))
            .Callback(() => _client.Raise(c => c.AppRulesChanged += null, Apps(Kid, 6)))
            .ReturnsAsync(new StateWriteAckDto(6));
        _client.Setup(c => c.SetAppDayAsync(It.IsAny<Guid>(), Kid, 3, DayOfWeek.Friday, true, It.IsAny<CancellationToken>()))
            .Callback(() => _client.Raise(c => c.AppRulesChanged += null, Apps(Kid, 7)))
            .ReturnsAsync(new StateWriteAckDto(7));
        _client.Setup(c => c.SetBudgetTextAsync(It.IsAny<Guid>(), Kid, "Ende", It.IsAny<CancellationToken>()))
            .Callback(() => _client.Raise(c => c.AppRulesChanged += null, Apps(Kid, 8)))
            .ReturnsAsync(new StateWriteAckDto(8));

        Assert.True(await _model.SetBudgetAsync(3, AppBudgetPart.Hours, 2));
        Assert.True(await _model.SetDayAsync(3, DayOfWeek.Friday, true));
        Assert.True(await _model.SetBudgetTextAsync("Ende"));
    }

    [Fact]
    public async Task Write_Rejected_False()
    {
        await ConnectAndSelectAsync();
        _client.Setup(c => c.SetAppDayAsync(It.IsAny<Guid>(), Kid, 3, DayOfWeek.Friday, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HubException("The app no longer exists."));

        Assert.False(await _model.SetDayAsync(3, DayOfWeek.Friday, true));
    }

    [Fact]
    public async Task SetBudgetText_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _model.SetBudgetTextAsync(null!));
    }

    [Fact]
    public async Task WarningSettings_FetchedOnConnect_BroadcastAppliedAndWritesSent()
    {
        _client.Setup(c => c.GetWarningSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new WarningSettingsDto(2, null, 5, null, 1, null));
        _client.Setup(c => c.SetWarningMinutesAsync(It.IsAny<Guid>(), WarningLevel.Early, 3, It.IsAny<CancellationToken>()))
            .Callback(() => _client.Raise(c => c.WarningSettingsChanged += null, new WarningSettingsDto(4, null, 3, null, 1, null)))
            .ReturnsAsync(new StateWriteAckDto(4));
        _client.Setup(c => c.SetWarningTextAsync(It.IsAny<Guid>(), WarningLevel.Final, "Schnell", It.IsAny<CancellationToken>()))
            .Callback(() => _client.Raise(c => c.WarningSettingsChanged += null, new WarningSettingsDto(5, null, 3, null, 1, "Schnell")))
            .ReturnsAsync(new StateWriteAckDto(5));
        var model = new WarningSettingsModel(_gateway, _time);
        Assert.Equal(AccountsLoadState.NotAvailable, model.LoadState);

        _gateway.SetConnected(_client.Object);
        await model.LastFetch;
        Assert.Equal((AccountsLoadState.Ready, 2L), (model.LoadState, model.Snapshot!.Revision));

        _client.Raise(c => c.WarningSettingsChanged += null, new WarningSettingsDto(3, null, 6, null, 1, null));
        Assert.Equal(6, model.Snapshot!.EarlyMinutes);

        Assert.True(await model.SetMinutesAsync(WarningLevel.Early, 3));
        Assert.True(await model.SetTextAsync(WarningLevel.Final, "Schnell"));
        Assert.Equal("Schnell", model.Snapshot!.FinalText);
        await Assert.ThrowsAsync<ArgumentNullException>(() => model.SetTextAsync(WarningLevel.Final, null!));
    }

    [Fact]
    public async Task WarningSettings_CreatedWhileConnected_FetchesAtOnce()
    {
        _client.Setup(c => c.GetWarningSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new WarningSettingsDto(2, null, 5, null, 1, null));
        _gateway.SetConnected(_client.Object);

        var model = new WarningSettingsModel(_gateway, _time);
        await model.LastFetch;

        Assert.Equal(AccountsLoadState.Ready, model.LoadState);
    }

    private async Task ConnectAndSelectAsync()
    {
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(Kid);
        await _model.LastFetch;
    }

    private static AppRulesDto Apps(string sid, long revision) => new(revision, null, sid, [new AppRuleDto(3, "Editor", "p", 0, BreakTimeDays.None)], null);
}
