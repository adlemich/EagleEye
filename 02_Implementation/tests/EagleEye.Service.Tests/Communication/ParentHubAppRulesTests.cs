using EagleEye.Service.Communication;
using EagleEye.Service.Pairing;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

/// <summary>The US-006 methods of <see cref="ParentHub"/> (state areas "AppRules" and "WarningSettings").</summary>
public sealed class ParentHubAppRulesTests
{
    private const string Sid = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string DeviceName = "Dad's laptop";

    private static readonly Guid RequestId = Guid.Parse("7f3c0000-0000-0000-0000-000000000006");
    private static readonly StateWriteAckDto Ack = new(9);

    private readonly Mock<IAppRuleService> _apps = new();
    private readonly Mock<IWarningSettingsService> _warnings = new();
    private readonly TestLogger<ParentHub> _logger = new();
    private readonly ParentHub _hub;

    public ParentHubAppRulesTests()
    {
        var context = HubContextFactory.Create("connection-1");
        ParentConnectionState.SetPaired(context.Object, "device-1", DeviceName);
        _hub = new ParentHub(
            Mock.Of<IPairingManager>(), Mock.Of<IParentConnectionRegistry>(), Mock.Of<IUserAccountService>(), Mock.Of<IUsageService>(),
            Mock.Of<IBreakTimeService>(), _apps.Object, _warnings.Object, _logger)
        {
            Context = context.Object,
        };
    }

    public static TheoryData<Func<ParentHub, Task>> AppWrites() => new()
    {
        hub => hub.SetAppBudget(RequestId, Sid, 3, AppBudgetPart.Hours, 2),
        hub => hub.SetAppDay(RequestId, Sid, 3, DayOfWeek.Sunday, true),
        hub => hub.SetBudgetText(RequestId, Sid, "Ende"),
    };

    public static TheoryData<Func<ParentHub, Task>> WarningWrites() => new()
    {
        hub => hub.SetWarningMinutes(RequestId, WarningLevel.Early, 3),
        hub => hub.SetWarningText(RequestId, WarningLevel.Final, "Schnell"),
    };

    // ---------- Queries ----------

    [Fact]
    public async Task GetAppRules_ReturnsServiceSnapshot()
    {
        var dto = new AppRulesDto(4, null, Sid, [], null);
        _apps.Setup(a => a.GetAsync(Sid, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        Assert.Same(dto, await _hub.GetAppRules(Sid));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-sid")]
    public async Task GetAppRules_InvalidSid_InvalidRequest(string? sid)
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.GetAppRules(sid!))).Message);
    }

    [Fact]
    public async Task GetAppRules_UnknownAccount_UnknownAccountMessage()
    {
        _apps.Setup(a => a.GetAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(new UnknownAccountException());

        Assert.Equal(ParentHub.UnknownAccountMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.GetAppRules(Sid))).Message);
    }

    [Fact]
    public async Task GetAppRules_Failure_LoggedAndSafeText()
    {
        var failure = new InvalidOperationException("db");
        _apps.Setup(a => a.GetAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.GetAppRules(Sid));

        Assert.Equal((ParentHub.RulesUnavailableMessage, true), (ex.Message, _logger.Has(LogLevel.Error, failure)));
    }

    [Fact]
    public async Task GetAppRules_HubException_PassedThrough()
    {
        var original = new HubException("as is");
        _apps.Setup(a => a.GetAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(original);

        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.GetAppRules(Sid)));
    }

    [Fact]
    public async Task GetWarningSettings_ReturnsSnapshot()
    {
        var dto = new WarningSettingsDto(2, null, 5, null, 1, null);
        _warnings.Setup(w => w.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        Assert.Same(dto, await _hub.GetWarningSettings());
    }

    [Fact]
    public async Task GetWarningSettings_Failure_LoggedAndSafeText()
    {
        var failure = new InvalidOperationException("db");
        _warnings.Setup(w => w.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.GetWarningSettings());

        Assert.Equal((ParentHub.RulesUnavailableMessage, true), (ex.Message, _logger.Has(LogLevel.Error, failure)));
    }

    [Fact]
    public async Task GetWarningSettings_HubException_PassedThrough()
    {
        var original = new HubException("as is");
        _warnings.Setup(w => w.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(original);

        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.GetWarningSettings()));
    }

    // ---------- Writes: delegation with the device name ----------

    [Fact]
    public async Task SetAppBudget_Delegates()
    {
        _apps.Setup(a => a.SetBudgetAsync(RequestId, Sid, 3, AppBudgetPart.Minutes, 59, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetAppBudget(RequestId, Sid, 3, AppBudgetPart.Minutes, 59));
    }

    [Fact]
    public async Task SetAppDay_Delegates()
    {
        _apps.Setup(a => a.SetDayAsync(RequestId, Sid, 3, DayOfWeek.Monday, false, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetAppDay(RequestId, Sid, 3, DayOfWeek.Monday, false));
    }

    [Fact]
    public async Task SetBudgetText_Delegates()
    {
        _apps.Setup(a => a.SetBudgetTextAsync(RequestId, Sid, "Ende \U0001F60A", DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetBudgetText(RequestId, Sid, "Ende \U0001F60A"));
    }

    [Fact]
    public async Task SetWarningMinutes_Delegates()
    {
        _warnings.Setup(w => w.SetMinutesAsync(RequestId, WarningLevel.Final, 2, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetWarningMinutes(RequestId, WarningLevel.Final, 2));
    }

    [Fact]
    public async Task SetWarningText_Delegates()
    {
        _warnings.Setup(w => w.SetTextAsync(RequestId, WarningLevel.Early, "Bald", DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetWarningText(RequestId, WarningLevel.Early, "Bald"));
    }

    // ---------- Writes: invalid input ----------

    [Theory]
    [InlineData(AppBudgetPart.Hours, 24)]
    [InlineData(AppBudgetPart.Minutes, 60)]
    [InlineData(AppBudgetPart.Hours, -1)]
    [InlineData((AppBudgetPart)4, 1)]
    public async Task SetAppBudget_InvalidValue_InvalidRequest(AppBudgetPart part, int value)
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetAppBudget(RequestId, Sid, 3, part, value))).Message);
    }

    [Fact]
    public async Task AppWrites_InvalidSidOrRequestOrDayOrText_InvalidRequest()
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetAppDay(RequestId, "x", 3, DayOfWeek.Monday, true))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetAppDay(Guid.Empty, Sid, 3, DayOfWeek.Monday, true))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetAppDay(RequestId, Sid, 3, (DayOfWeek)9, true))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetBudgetText(RequestId, Sid, null!))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetBudgetText(RequestId, Sid, new string('a', 501)))).Message);
    }

    [Theory]
    [InlineData(WarningLevel.Early, 0)]
    [InlineData(WarningLevel.Final, 60)]
    [InlineData((WarningLevel)3, 5)]
    public async Task SetWarningMinutes_Invalid_InvalidRequest(WarningLevel level, int minutes)
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetWarningMinutes(RequestId, level, minutes))).Message);
    }

    [Fact]
    public async Task WarningWrites_InvalidRequestOrText_InvalidRequest()
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetWarningMinutes(Guid.Empty, WarningLevel.Early, 3))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetWarningText(RequestId, WarningLevel.Early, "a" + '\uD83D'))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetWarningText(RequestId, (WarningLevel)3, "x"))).Message);
    }

    // ---------- Writes: exception mapping ----------

    [Theory]
    [MemberData(nameof(AppWrites))]
    public async Task AppWrite_AppNotFound_SafeText(Func<ParentHub, Task> write)
    {
        SetupAllWritesToThrow(new AppNotFoundException());

        Assert.Equal(ParentHub.AppNotFoundMessage, (await Assert.ThrowsAsync<HubException>(() => write(_hub))).Message);
    }

    [Theory]
    [MemberData(nameof(AppWrites))]
    [MemberData(nameof(WarningWrites))]
    public async Task Write_StorageFails_LoggedAndSaveFailedMessage(Func<ParentHub, Task> write)
    {
        var failure = new SqliteException("disk full", 13);
        SetupAllWritesToThrow(failure);

        var ex = await Assert.ThrowsAsync<HubException>(() => write(_hub));

        Assert.Equal((ParentHub.SaveFailedMessage, true), (ex.Message, _logger.Has(LogLevel.Error, failure)));
    }

    [Fact]
    public async Task SetWarningMinutes_FinalNotLess_SafeText()
    {
        SetupAllWritesToThrow(new RuleValidationException(RuleViolation.FinalNotLessThanEarly));

        Assert.Equal(ParentHub.FinalNotLessMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetWarningMinutes(RequestId, WarningLevel.Final, 5))).Message);
    }

    [Fact]
    public async Task AppWrite_UnknownAccount_SafeText()
    {
        SetupAllWritesToThrow(new UnknownAccountException());

        Assert.Equal(ParentHub.UnknownAccountMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetAppDay(RequestId, Sid, 3, DayOfWeek.Monday, true))).Message);
    }

    private void SetupAllWritesToThrow(Exception failure)
    {
        _apps.Setup(a => a.SetBudgetAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _apps.Setup(a => a.SetDayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<DayOfWeek>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _apps.Setup(a => a.SetBudgetTextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _warnings.Setup(w => w.SetMinutesAsync(It.IsAny<Guid>(), It.IsAny<WarningLevel>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _warnings.Setup(w => w.SetTextAsync(It.IsAny<Guid>(), It.IsAny<WarningLevel>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
    }
}
