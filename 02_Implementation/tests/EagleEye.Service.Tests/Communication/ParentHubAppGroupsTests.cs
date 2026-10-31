using EagleEye.Service.Communication;
using EagleEye.Service.Groups;
using EagleEye.Service.Pairing;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

/// <summary>The US-008 methods of <see cref="ParentHub"/> (state areas "AppGroups" and "AppGroupRules").</summary>
public sealed class ParentHubAppGroupsTests
{
    private const string Sid = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string DeviceName = "Dad's MacBook";
    private const string Path = @"C:\Games\launcher.exe";

    private static readonly Guid RequestId = Guid.Parse("7f3c0000-0000-0000-0000-000000000008");
    private static readonly StateWriteAckDto Ack = new(11);

    private readonly Mock<IAppGroupService> _groups = new();
    private readonly TestLogger<ParentHub> _logger = new();
    private readonly ParentHub _hub;

    public ParentHubAppGroupsTests()
    {
        var context = HubContextFactory.Create("connection-1");
        ParentConnectionState.SetPaired(context.Object, "device-1", DeviceName);
        _hub = new ParentHub(
            Mock.Of<IPairingManager>(), Mock.Of<IParentConnectionRegistry>(), Mock.Of<IUserAccountService>(), Mock.Of<IUsageService>(),
            Mock.Of<IBreakTimeService>(), Mock.Of<IAppRuleService>(), Mock.Of<IWarningSettingsService>(), _groups.Object, _logger)
        {
            Context = context.Object,
        };
    }

    public static TheoryData<Func<ParentHub, Task>> Writes() => new()
    {
        hub => hub.CreateAppGroup(RequestId, "Games"),
        hub => hub.RenameAppGroup(RequestId, 1, "Gaming"),
        hub => hub.DeleteAppGroup(RequestId, 1),
        hub => hub.MoveAppToGroup(RequestId, Path, 1),
        hub => hub.SetAppGroupBudget(RequestId, Sid, 1, AppBudgetPart.Hours, 1),
        hub => hub.SetAppGroupDay(RequestId, Sid, 1, DayOfWeek.Saturday, true),
    };

    // ---------- Queries ----------

    [Fact]
    public async Task GetAppGroups_ReturnsSnapshot_FailureSafeText_HubExceptionPassedThrough()
    {
        var dto = new AppGroupsDto(3, null, [], []);
        _groups.Setup(g => g.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Assert.Same(dto, await _hub.GetAppGroups());

        var failure = new InvalidOperationException("db");
        _groups.Setup(g => g.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        Assert.Equal(ParentHub.RulesUnavailableMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.GetAppGroups())).Message);
        Assert.True(_logger.Has(LogLevel.Error, failure));

        var original = new HubException("as is");
        _groups.Setup(g => g.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(original);
        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.GetAppGroups()));
    }

    [Fact]
    public async Task GetAppGroupRules_ReturnsSnapshot()
    {
        var dto = new AppGroupRulesDto(4, null, Sid, []);
        _groups.Setup(g => g.GetRulesAsync(Sid, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        Assert.Same(dto, await _hub.GetAppGroupRules(Sid));
    }

    [Fact]
    public async Task GetAppGroupRules_InvalidSidUnknownAccountFailureOrHubException()
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.GetAppGroupRules("x"))).Message);

        _groups.Setup(g => g.GetRulesAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(new UnknownAccountException());
        Assert.Equal(ParentHub.UnknownAccountMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.GetAppGroupRules(Sid))).Message);

        var failure = new InvalidOperationException("db");
        _groups.Setup(g => g.GetRulesAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        Assert.Equal(ParentHub.RulesUnavailableMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.GetAppGroupRules(Sid))).Message);
        Assert.True(_logger.Has(LogLevel.Error, failure));

        var original = new HubException("as is");
        _groups.Setup(g => g.GetRulesAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(original);
        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.GetAppGroupRules(Sid)));
    }

    // ---------- Writes: delegation with the device name ----------

    [Fact]
    public async Task Writes_DelegateWithTheDeviceName()
    {
        _groups.Setup(g => g.CreateAsync(RequestId, "Games", DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);
        _groups.Setup(g => g.RenameAsync(RequestId, 1, "Gaming", DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);
        _groups.Setup(g => g.DeleteAsync(RequestId, 1, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);
        _groups.Setup(g => g.MoveAsync(RequestId, Path, null, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);
        _groups.Setup(g => g.SetBudgetAsync(RequestId, Sid, 1, AppBudgetPart.Minutes, 59, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);
        _groups.Setup(g => g.SetDayAsync(RequestId, Sid, 1, DayOfWeek.Sunday, false, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.CreateAppGroup(RequestId, "Games"));
        Assert.Same(Ack, await _hub.RenameAppGroup(RequestId, 1, "Gaming"));
        Assert.Same(Ack, await _hub.DeleteAppGroup(RequestId, 1));
        Assert.Same(Ack, await _hub.MoveAppToGroup(RequestId, Path, null));
        Assert.Same(Ack, await _hub.SetAppGroupBudget(RequestId, Sid, 1, AppBudgetPart.Minutes, 59));
        Assert.Same(Ack, await _hub.SetAppGroupDay(RequestId, Sid, 1, DayOfWeek.Sunday, false));
    }

    // ---------- Writes: invalid input ----------

    [Fact]
    public async Task Writes_InvalidInput_InvalidRequest()
    {
        Func<Task>[] calls =
        [
            () => _hub.CreateAppGroup(RequestId, "   "),
            () => _hub.CreateAppGroup(RequestId, new string('x', 41)),
            () => _hub.CreateAppGroup(Guid.Empty, "Games"),
            () => _hub.RenameAppGroup(RequestId, 1, null!),
            () => _hub.DeleteAppGroup(Guid.Empty, 1),
            () => _hub.MoveAppToGroup(RequestId, " ", 1),
            () => _hub.SetAppGroupBudget(RequestId, Sid, 1, AppBudgetPart.Hours, 24),
            () => _hub.SetAppGroupBudget(RequestId, "x", 1, AppBudgetPart.Hours, 1),
            () => _hub.SetAppGroupDay(RequestId, Sid, 1, (DayOfWeek)9, true),
        ];

        foreach (var call in calls)
        {
            Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(call)).Message);
        }

        _groups.VerifyNoOtherCalls();
    }

    // ---------- Writes: safe error texts ----------

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task Writes_ServiceExceptions_MappedToSafeTexts(Func<ParentHub, Task> write)
    {
        var cases = new (Exception Failure, string Message)[]
        {
            (new GroupNotFoundException(), ParentHub.GroupNotFoundMessage),
            (new DuplicateGroupNameException(), ParentHub.DuplicateGroupNameMessage),
            (new AppNotFoundException(), ParentHub.AppNotFoundMessage),
            (new UnknownAccountException(), ParentHub.UnknownAccountMessage),
            (new IOException("disk full"), ParentHub.SaveFailedMessage),
        };

        foreach (var (failure, message) in cases)
        {
            SetupAllWritesToThrow(failure);
            Assert.Equal(message, (await Assert.ThrowsAsync<HubException>(() => write(_hub))).Message);
        }
    }

    private void SetupAllWritesToThrow(Exception failure)
    {
        _groups.Setup(g => g.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _groups.Setup(g => g.RenameAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _groups.Setup(g => g.DeleteAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _groups.Setup(g => g.MoveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _groups.Setup(g => g.SetBudgetAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        _groups.Setup(g => g.SetDayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<DayOfWeek>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
    }
}
