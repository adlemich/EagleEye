using EagleEye.ParentApp.Core.Communication;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ConnectionStateTests
{
    [Theory]
    [InlineData(ConnectionStatus.NotPaired, PairingStatus.NotPaired)]
    [InlineData(ConnectionStatus.PairingConnecting, PairingStatus.InProgress)]
    [InlineData(ConnectionStatus.AwaitingCode, PairingStatus.InProgress)]
    [InlineData(ConnectionStatus.PairedConnecting, PairingStatus.Paired)]
    [InlineData(ConnectionStatus.PairedConnected, PairingStatus.Paired)]
    [InlineData(ConnectionStatus.PairedDisconnected, PairingStatus.Paired)]
    public void PairingStatus_DerivedFromStatus(ConnectionStatus status, PairingStatus expected)
    {
        var state = new ConnectionState(status, "kid-pc", null, ConnectionMessage.None);

        Assert.Equal(expected, state.PairingStatus);
    }

    [Fact]
    public void Initial_IsNotPairedWithoutHostOrMessage()
    {
        Assert.Equal(new ConnectionState(ConnectionStatus.NotPaired, null, null, ConnectionMessage.None), ConnectionState.Initial);
    }
}
