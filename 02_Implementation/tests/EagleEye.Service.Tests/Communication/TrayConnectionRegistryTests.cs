using EagleEye.Service.Communication;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class TrayConnectionRegistryTests
{
    private readonly TrayConnectionRegistry _registry = new();

    [Fact]
    public void NewestOf_Empty_Null()
    {
        Assert.Null(_registry.NewestOf(2));
    }

    [Fact]
    public void NewestOf_TwoConnectionsInTheSession_TheNewest()
    {
        _registry.Register("a", new TrayClientIdentity(2, null, 1));
        _registry.Register("b", new TrayClientIdentity(2, null, 2));
        _registry.Register("c", new TrayClientIdentity(3, null, 3));

        Assert.Equal(("b", "c"), (_registry.NewestOf(2), _registry.NewestOf(3)));
    }

    [Fact]
    public void Unregister_FallsBackToTheOlderConnection()
    {
        _registry.Register("a", new TrayClientIdentity(2, null, 1));
        _registry.Register("b", new TrayClientIdentity(2, null, 2));

        _registry.Unregister("b");
        _registry.Unregister("unknown");

        Assert.Equal("a", _registry.NewestOf(2));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.ThrowsAny<ArgumentException>(() => _registry.Register(" ", new TrayClientIdentity(2, null, 1)));
        Assert.Throws<ArgumentNullException>(() => _registry.Register("a", null!));
        Assert.Throws<ArgumentNullException>(() => _registry.Unregister(null!));
    }
}
