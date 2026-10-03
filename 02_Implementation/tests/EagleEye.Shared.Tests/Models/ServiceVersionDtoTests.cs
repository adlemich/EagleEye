using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class ServiceVersionDtoTests
{
    [Fact]
    public void Constructor_WithVersion_SetsVersion()
    {
        var dto = new ServiceVersionDto("EagleEye_v0.1");

        Assert.Equal("EagleEye_v0.1", dto.Version);
    }

    [Fact]
    public void Equals_SameVersion_ReturnsTrue()
    {
        var first = new ServiceVersionDto("EagleEye_v0.1");
        var second = new ServiceVersionDto("EagleEye_v0.1");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equals_DifferentVersion_ReturnsFalse()
    {
        var first = new ServiceVersionDto("EagleEye_v0.1");
        var second = new ServiceVersionDto("EagleEye_v0.2");

        Assert.NotEqual(first, second);
    }
}
