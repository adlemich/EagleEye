using EagleEye.Service.Communication;
using EagleEye.Service.Pairing;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class PairingAuthorizationHubFilterTests
{
    private readonly PairingAuthorizationHubFilter _filter = new();

    [Theory]
    [InlineData(nameof(ParentHub.RemovePairedDevice))]
    [InlineData(nameof(ParentHub.GetUserAccounts))]
    [InlineData(nameof(ParentHub.SetParentalControl))]
    public async Task InvokeMethodAsync_UnpairedProtectedMethod_ThrowsNotPaired(string methodName)
    {
        var context = CreateInvocation(methodName, paired: false);

        var ex = await Assert.ThrowsAsync<HubException>(() => _filter.InvokeMethodAsync(context, Next).AsTask());

        Assert.Equal(PairingAuthorizationHubFilter.NotPairedMessage, ex.Message);
    }

    [Theory]
    [InlineData(nameof(ParentHub.GetPairingStatus))]
    [InlineData(nameof(ParentHub.StartPairing))]
    [InlineData(nameof(ParentHub.SubmitPairingCode))]
    public async Task InvokeMethodAsync_UnpairedAllowUnpairedMethod_CallsNext(string methodName)
    {
        var context = CreateInvocation(methodName, paired: false);

        var result = await _filter.InvokeMethodAsync(context, Next);

        Assert.Equal("next", result);
    }

    [Theory]
    [InlineData(nameof(ParentHub.StartPairing))]
    [InlineData(nameof(ParentHub.SubmitPairingCode))]
    public async Task InvokeMethodAsync_PairedPairingMethod_ThrowsAlreadyPaired(string methodName)
    {
        var context = CreateInvocation(methodName, paired: true);

        var ex = await Assert.ThrowsAsync<HubException>(() => _filter.InvokeMethodAsync(context, Next).AsTask());

        Assert.Equal(PairingAuthorizationHubFilter.AlreadyPairedMessage, ex.Message);
    }

    [Theory]
    [InlineData(nameof(ParentHub.RemovePairedDevice))]
    [InlineData(nameof(ParentHub.GetPairingStatus))]
    [InlineData(nameof(ParentHub.GetUserAccounts))]
    [InlineData(nameof(ParentHub.SetParentalControl))]
    public async Task InvokeMethodAsync_PairedOtherMethod_CallsNext(string methodName)
    {
        var context = CreateInvocation(methodName, paired: true);

        var result = await _filter.InvokeMethodAsync(context, Next);

        Assert.Equal("next", result);
    }

    [Fact]
    public async Task InvokeMethodAsync_NullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _filter.InvokeMethodAsync(null!, Next).AsTask());
    }

    [Fact]
    public async Task InvokeMethodAsync_NullNext_ThrowsArgumentNullException()
    {
        var context = CreateInvocation(nameof(ParentHub.GetPairingStatus), paired: false);

        await Assert.ThrowsAsync<ArgumentNullException>(() => _filter.InvokeMethodAsync(context, null!).AsTask());
    }

    private static ValueTask<object?> Next(HubInvocationContext context)
    {
        return ValueTask.FromResult<object?>("next");
    }

    private static HubInvocationContext CreateInvocation(string methodName, bool paired)
    {
        var caller = HubContextFactory.Create("connection-1");
        if (paired)
        {
            ParentConnectionState.SetPaired(caller.Object, "device-1", "Dad's laptop");
        }

        var hub = new ParentHub(
            Mock.Of<IPairingManager>(), Mock.Of<IParentConnectionRegistry>(), Mock.Of<IUserAccountService>(),
            Mock.Of<IUsageService>(), Mock.Of<ILogger<ParentHub>>());
        var method = typeof(ParentHub).GetMethod(methodName)
            ?? throw new InvalidOperationException($"Method {methodName} not found.");
        return new HubInvocationContext(caller.Object, Mock.Of<IServiceProvider>(), hub, method, []);
    }
}
