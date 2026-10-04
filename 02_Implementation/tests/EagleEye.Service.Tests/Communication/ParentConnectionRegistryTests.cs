using EagleEye.Service.Communication;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class ParentConnectionRegistryTests
{
    private readonly ParentConnectionRegistry _registry = new();

    [Fact]
    public void Register_TwoConnections_CountsBoth()
    {
        _registry.Register("device-1", HubContextFactory.Create("c1").Object);
        _registry.Register("device-1", HubContextFactory.Create("c2").Object);

        Assert.Equal(2, _registry.CountFor("device-1"));
    }

    [Fact]
    public void Unregister_OneOfTwo_KeepsTheOther()
    {
        _registry.Register("device-1", HubContextFactory.Create("c1").Object);
        _registry.Register("device-1", HubContextFactory.Create("c2").Object);

        _registry.Unregister("device-1", "c1");

        Assert.Equal(1, _registry.CountFor("device-1"));
    }

    [Fact]
    public void Unregister_LastConnection_RemovesDevice()
    {
        _registry.Register("device-1", HubContextFactory.Create("c1").Object);

        _registry.Unregister("device-1", "c1");

        Assert.Equal(0, _registry.CountFor("device-1"));
    }

    [Fact]
    public void Unregister_UnknownDeviceOrConnection_DoesNothing()
    {
        _registry.Register("device-1", HubContextFactory.Create("c1").Object);

        _registry.Unregister("device-2", "c1");
        _registry.Unregister("device-1", "c9");

        Assert.Equal(1, _registry.CountFor("device-1"));
    }

    [Fact]
    public void AbortAll_ExceptCaller_AbortsOnlyOthers()
    {
        var caller = HubContextFactory.Create("c1");
        var other = HubContextFactory.Create("c2");
        _registry.Register("device-1", caller.Object);
        _registry.Register("device-1", other.Object);

        _registry.AbortAll("device-1", "c1");

        caller.Verify(c => c.Abort(), Times.Never);
        other.Verify(c => c.Abort(), Times.Once);
        Assert.Equal(1, _registry.CountFor("device-1"));
    }

    [Fact]
    public void AbortAll_WithoutException_AbortsAllAndRemovesDevice()
    {
        var first = HubContextFactory.Create("c1");
        _registry.Register("device-1", first.Object);

        _registry.AbortAll("device-1", exceptConnectionId: null);

        first.Verify(c => c.Abort(), Times.Once);
        Assert.Equal(0, _registry.CountFor("device-1"));
    }

    [Fact]
    public void AbortAll_OtherDevice_IsNotAffected()
    {
        var otherDevice = HubContextFactory.Create("c2");
        _registry.Register("device-1", HubContextFactory.Create("c1").Object);
        _registry.Register("device-2", otherDevice.Object);

        _registry.AbortAll("device-1", null);

        otherDevice.Verify(c => c.Abort(), Times.Never);
    }

    [Fact]
    public void AbortAll_UnknownDevice_DoesNothing()
    {
        var exception = Record.Exception(() => _registry.AbortAll("device-9", null));

        Assert.Null(exception);
    }

    [Fact]
    public void Register_NullConnection_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _registry.Register("device-1", (HubCallerContext)null!));
    }
}
