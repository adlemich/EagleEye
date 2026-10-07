using EagleEye.Service.Communication;
using EagleEye.Service.Pairing;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

/// <summary>The US-003 methods of <see cref="ParentHub"/>: <c>GetUserAccounts</c>, <c>SetParentalControl</c>.</summary>
public sealed class ParentHubUserAccountsTests
{
    private const string Sid = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string DeviceName = "Dad's laptop";

    private static readonly Guid RequestId = Guid.Parse("7f3c0000-0000-0000-0000-000000000001");

    private readonly Mock<IUserAccountService> _userAccounts = new();
    private readonly Mock<IUsageService> _usage = new();
    private readonly TestLogger<ParentHub> _logger = new();
    private readonly ParentHub _hub;

    public ParentHubUserAccountsTests()
    {
        var context = HubContextFactory.Create("connection-1");
        ParentConnectionState.SetPaired(context.Object, "device-1", DeviceName);
        _hub = new ParentHub(Mock.Of<IPairingManager>(), Mock.Of<IParentConnectionRegistry>(), _userAccounts.Object, _usage.Object, _logger)
        {
            Context = context.Object,
        };
    }

    [Fact]
    public async Task GetUserAccounts_ReturnsSnapshotOfService()
    {
        var snapshot = new UserAccountListDto(3, null, []);
        _userAccounts.Setup(u => u.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        Assert.Same(snapshot, await _hub.GetUserAccounts());
    }

    [Theory]
    [MemberData(nameof(ServiceFailures))]
    public async Task GetUserAccounts_ServiceFails_LogsAndThrowsSafeHubException(Exception failure)
    {
        _userAccounts.Setup(u => u.GetSnapshotAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.GetUserAccounts());

        Assert.Equal((ParentHub.AccountsUnavailableMessage, true), (ex.Message, _logger.Has(LogLevel.Error, failure)));
    }

    [Fact]
    public async Task GetUserAccounts_HubException_IsPassedThrough()
    {
        var original = new HubException("as is");
        _userAccounts.Setup(u => u.GetSnapshotAsync(It.IsAny<CancellationToken>())).ThrowsAsync(original);

        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.GetUserAccounts()));
    }

    [Fact]
    public async Task SetParentalControl_Valid_DelegatesWithDeviceNameFromConnection()
    {
        var ack = new StateWriteAckDto(13);
        _userAccounts.Setup(u => u.SetParentalControlAsync(RequestId, Sid, true, DeviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ack);

        Assert.Same(ack, await _hub.SetParentalControl(RequestId, Sid, true));
    }

    [Fact]
    public async Task SetParentalControl_EmptyRequestId_ThrowsInvalidRequest()
    {
        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SetParentalControl(Guid.Empty, Sid, true));

        Assert.Equal(ParentHub.InvalidRequestMessage, ex.Message);
        _userAccounts.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("max")]
    [InlineData("S-1-5-21-x")]
    [InlineData("C:\\Users\\max")]
    public async Task SetParentalControl_InvalidSid_ThrowsInvalidRequest(string? sid)
    {
        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SetParentalControl(RequestId, sid!, true));

        Assert.Equal(ParentHub.InvalidRequestMessage, ex.Message);
        _userAccounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SetParentalControl_UnknownAccount_ThrowsUnknownAccount()
    {
        _userAccounts.Setup(u => u.SetParentalControlAsync(RequestId, Sid, false, DeviceName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnknownAccountException());

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SetParentalControl(RequestId, Sid, false));

        Assert.Equal(ParentHub.UnknownAccountMessage, ex.Message);
    }

    [Theory]
    [MemberData(nameof(ServiceFailures))]
    public async Task SetParentalControl_ServiceFails_LogsAndThrowsSaveFailed(Exception failure)
    {
        _userAccounts.Setup(u => u.SetParentalControlAsync(RequestId, Sid, true, DeviceName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SetParentalControl(RequestId, Sid, true));

        Assert.Equal((ParentHub.SaveFailedMessage, true), (ex.Message, _logger.Has(LogLevel.Error, failure)));
    }

    [Fact]
    public async Task SetParentalControl_HubException_IsPassedThrough()
    {
        var original = new HubException("as is");
        _userAccounts.Setup(u => u.SetParentalControlAsync(RequestId, Sid, true, DeviceName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(original);

        Assert.Same(original, await Assert.ThrowsAsync<HubException>(() => _hub.SetParentalControl(RequestId, Sid, true)));
    }

    [Theory]
    [InlineData("S-1-5-18", true)]
    [InlineData(Sid, true)]
    [InlineData("S-1", false)]
    [InlineData(null, false)]
    public void IsValidSid_ReturnsWhetherSyntacticallyValid(string? sid, bool expected)
    {
        Assert.Equal(expected, ParentHub.IsValidSid(sid));
    }

    public static TheoryData<Exception> ServiceFailures => new()
    {
        new AccountInventoryUnavailableException(),
        new InvalidOperationException("C:\\secret\\path"),
    };
}
