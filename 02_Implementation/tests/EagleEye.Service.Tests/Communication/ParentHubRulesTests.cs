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

/// <summary>The US-005 methods of <see cref="ParentHub"/> (state area "AccountRules").</summary>
public sealed class ParentHubRulesTests
{
    private const string Sid = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string DeviceName = "Dad's laptop";

    private static readonly Guid RequestId = Guid.Parse("7f3c0000-0000-0000-0000-000000000005");
    private static readonly StateWriteAckDto Ack = new(9);

    private readonly Mock<IBreakTimeService> _rules = new();
    private readonly TestLogger<ParentHub> _logger = new();
    private readonly ParentHub _hub;

    public ParentHubRulesTests()
    {
        var context = HubContextFactory.Create("connection-1");
        ParentConnectionState.SetPaired(context.Object, "device-1", DeviceName);
        _hub = new ParentHub(
            Mock.Of<IPairingManager>(), Mock.Of<IParentConnectionRegistry>(), Mock.Of<IUserAccountService>(), Mock.Of<IUsageService>(), _rules.Object, _logger)
        {
            Context = context.Object,
        };
    }

    public static TheoryData<Func<ParentHub, Task>> Writes() => new()
    {
        hub => hub.AddBreakTimeEntry(RequestId, Sid),
        hub => hub.DeleteBreakTimeEntry(RequestId, Sid, 3),
        hub => hub.SetBreakTimeEntryActive(RequestId, Sid, 3, true),
        hub => hub.SetBreakTimeEntryTime(RequestId, Sid, 3, BreakTimeBoundary.End, 1439),
        hub => hub.SetBreakTimeEntryDay(RequestId, Sid, 3, DayOfWeek.Sunday, false),
        hub => hub.SetDisplayText(RequestId, Sid, "Hallo\r\n\U0001F60A"),
    };

    // ---------- GetAccountRules ----------

    [Fact]
    public async Task GetAccountRules_ReturnsServiceSnapshot()
    {
        var dto = new AccountRulesDto(4, null, Sid, [], "x", false);
        _rules.Setup(r => r.GetAsync(Sid, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        Assert.Same(dto, await _hub.GetAccountRules(Sid));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-sid")]
    public async Task GetAccountRules_InvalidSid_InvalidRequest(string? sid)
    {
        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.GetAccountRules(sid!));

        Assert.Equal(ParentHub.InvalidRequestMessage, ex.Message);
    }

    [Fact]
    public async Task GetAccountRules_UnknownAccount_UnknownAccountMessage()
    {
        _rules.Setup(r => r.GetAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(new UnknownAccountException());

        Assert.Equal(ParentHub.UnknownAccountMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.GetAccountRules(Sid))).Message);
    }

    [Fact]
    public async Task GetAccountRules_Failure_LoggedAndSafeText()
    {
        var failure = new AccountInventoryUnavailableException();
        _rules.Setup(r => r.GetAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.GetAccountRules(Sid));

        Assert.Equal((ParentHub.RulesUnavailableMessage, true), (ex.Message, _logger.Has(LogLevel.Error, failure)));
    }

    [Fact]
    public async Task GetAccountRules_HubException_PassedThrough()
    {
        var original = new HubException("as is");
        _rules.Setup(r => r.GetAsync(Sid, It.IsAny<CancellationToken>())).ThrowsAsync(original);

        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.GetAccountRules(Sid)));
    }

    // ---------- Writes: delegation ----------

    [Fact]
    public async Task AddBreakTimeEntry_Delegates()
    {
        _rules.Setup(r => r.AddEntryAsync(RequestId, Sid, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.AddBreakTimeEntry(RequestId, Sid));
    }

    [Fact]
    public async Task DeleteBreakTimeEntry_Delegates()
    {
        _rules.Setup(r => r.DeleteEntryAsync(RequestId, Sid, 3, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.DeleteBreakTimeEntry(RequestId, Sid, 3));
    }

    [Fact]
    public async Task SetBreakTimeEntryActive_Delegates()
    {
        _rules.Setup(r => r.SetActiveAsync(RequestId, Sid, 3, true, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetBreakTimeEntryActive(RequestId, Sid, 3, true));
    }

    [Fact]
    public async Task SetBreakTimeEntryTime_Delegates()
    {
        _rules.Setup(r => r.SetTimeAsync(RequestId, Sid, 3, BreakTimeBoundary.Start, 0, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetBreakTimeEntryTime(RequestId, Sid, 3, BreakTimeBoundary.Start, 0));
    }

    [Fact]
    public async Task SetBreakTimeEntryDay_Delegates()
    {
        _rules.Setup(r => r.SetDayAsync(RequestId, Sid, 3, DayOfWeek.Sunday, true, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetBreakTimeEntryDay(RequestId, Sid, 3, DayOfWeek.Sunday, true));
    }

    [Fact]
    public async Task SetDisplayText_Delegates()
    {
        _rules.Setup(r => r.SetDisplayTextAsync(RequestId, Sid, "Hallo \U0001F60A", DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetDisplayText(RequestId, Sid, "Hallo \U0001F60A"));
    }

    // ---------- Writes: invalid input ----------

    [Fact]
    public async Task Write_EmptyRequestId_InvalidRequest()
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.AddBreakTimeEntry(Guid.Empty, Sid))).Message);
    }

    [Fact]
    public async Task Write_InvalidSid_InvalidRequest()
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.AddBreakTimeEntry(RequestId, "x"))).Message);
    }

    [Theory]
    [InlineData(BreakTimeBoundary.Start, -1)]
    [InlineData(BreakTimeBoundary.End, 1440)]
    [InlineData((BreakTimeBoundary)7, 60)]
    public async Task SetBreakTimeEntryTime_InvalidArguments_InvalidRequest(BreakTimeBoundary boundary, int minute)
    {
        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SetBreakTimeEntryTime(RequestId, Sid, 3, boundary, minute));

        Assert.Equal(ParentHub.InvalidRequestMessage, ex.Message);
    }

    [Fact]
    public async Task SetBreakTimeEntryDay_UnknownDay_InvalidRequest()
    {
        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SetBreakTimeEntryDay(RequestId, Sid, 3, (DayOfWeek)9, true));

        Assert.Equal(ParentHub.InvalidRequestMessage, ex.Message);
    }

    [Fact]
    public async Task SetDisplayText_InvalidTexts_InvalidRequest()
    {
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetDisplayText(RequestId, Sid, null!))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetDisplayText(RequestId, Sid, new string('a', 501)))).Message);
        Assert.Equal(ParentHub.InvalidRequestMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.SetDisplayText(RequestId, Sid, "a" + '\uD83D'))).Message);
    }

    [Fact]
    public async Task SetDisplayText_CrLfNormalizedBeforeTheLengthCheck_Accepted()
    {
        var text = new string('a', 499) + "\r\n";
        _rules.Setup(r => r.SetDisplayTextAsync(RequestId, Sid, text, DeviceName, It.IsAny<CancellationToken>())).ReturnsAsync(Ack);

        Assert.Same(Ack, await _hub.SetDisplayText(RequestId, Sid, text));
    }

    // ---------- Writes: exception mapping ----------

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task Write_UnknownAccount_UnknownAccountMessage(Func<ParentHub, Task> write)
    {
        SetupAllWritesToThrow(new UnknownAccountException());

        Assert.Equal(ParentHub.UnknownAccountMessage, (await Assert.ThrowsAsync<HubException>(() => write(_hub))).Message);
    }

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task Write_StorageFails_LoggedAndSaveFailedMessage(Func<ParentHub, Task> write)
    {
        var failure = new SqliteException("disk full", 13);
        SetupAllWritesToThrow(failure);

        var ex = await Assert.ThrowsAsync<HubException>(() => write(_hub));

        Assert.Equal((ParentHub.SaveFailedMessage, true), (ex.Message, _logger.Has(LogLevel.Error, failure)));
    }

    [Fact]
    public async Task Write_EntryNotFound_EntryNotFoundMessage()
    {
        SetupAllWritesToThrow(new EntryNotFoundException());

        Assert.Equal(ParentHub.EntryNotFoundMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.DeleteBreakTimeEntry(RequestId, Sid, 3))).Message);
    }

    [Fact]
    public async Task Write_TooManyEntries_TooManyEntriesMessage()
    {
        SetupAllWritesToThrow(new TooManyEntriesException());

        Assert.Equal(ParentHub.TooManyEntriesMessage, (await Assert.ThrowsAsync<HubException>(() => _hub.AddBreakTimeEntry(RequestId, Sid))).Message);
    }

    [Theory]
    [InlineData(RuleViolation.EndNotAfterStart, ParentHub.EndNotAfterStartMessage)]
    [InlineData(RuleViolation.NoDaySelected, ParentHub.NoDaySelectedMessage)]
    public async Task Write_RuleViolation_SafeText(RuleViolation violation, string expected)
    {
        SetupAllWritesToThrow(new RuleValidationException(violation));

        Assert.Equal(expected, (await Assert.ThrowsAsync<HubException>(() => _hub.SetBreakTimeEntryDay(RequestId, Sid, 3, DayOfWeek.Monday, false))).Message);
    }

    [Fact]
    public async Task Write_HubException_PassedThrough()
    {
        var original = new HubException("as is");
        SetupAllWritesToThrow(original);

        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.AddBreakTimeEntry(RequestId, Sid)));
    }

    private void SetupAllWritesToThrow(Exception failure)
    {
        _rules.Setup(r => r.AddEntryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _rules.Setup(r => r.DeleteEntryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _rules.Setup(r => r.SetActiveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _rules.Setup(r => r.SetTimeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<BreakTimeBoundary>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _rules.Setup(r => r.SetDayAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<DayOfWeek>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        _rules.Setup(r => r.SetDisplayTextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
    }
}
