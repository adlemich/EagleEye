using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Models;

public sealed class PairingDtoTests
{
    [Fact]
    public void PairingStatusDto_Constructor_SetsProperties()
    {
        var dto = new PairingStatusDto(true, "device-1", "Dad's laptop");

        Assert.Equal((true, "device-1", "Dad's laptop"), (dto.IsPaired, dto.DeviceId, dto.DeviceName));
    }

    [Fact]
    public void PairingStatusDto_Equals_SameValues_ReturnsTrue()
    {
        Assert.Equal(new PairingStatusDto(false, null, null), new PairingStatusDto(false, null, null));
    }

    [Fact]
    public void PairingStatusDto_Equals_DifferentValues_ReturnsFalse()
    {
        Assert.NotEqual(new PairingStatusDto(true, "a", "n"), new PairingStatusDto(true, "b", "n"));
    }

    [Fact]
    public void PairingResultDto_Constructor_SetsProperties()
    {
        var dto = new PairingResultDto(PairingOutcome.Success, "device-1", "token");

        Assert.Equal((PairingOutcome.Success, "device-1", "token"), (dto.Outcome, dto.DeviceId, dto.Token));
    }

    [Fact]
    public void PairingResultDto_Equals_SameValues_ReturnsTrue()
    {
        Assert.Equal(
            new PairingResultDto(PairingOutcome.WrongCode, null, null),
            new PairingResultDto(PairingOutcome.WrongCode, null, null));
    }

    [Fact]
    public void PairingResultDto_Equals_DifferentOutcome_ReturnsFalse()
    {
        Assert.NotEqual(
            new PairingResultDto(PairingOutcome.WrongCode, null, null),
            new PairingResultDto(PairingOutcome.CodeExpired, null, null));
    }
}
