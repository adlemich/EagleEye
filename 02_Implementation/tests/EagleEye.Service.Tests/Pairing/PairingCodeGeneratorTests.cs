using EagleEye.Service.Pairing;
using Xunit;

namespace EagleEye.Service.Tests.Pairing;

public sealed class PairingCodeGeneratorTests
{
    [Fact]
    public void Generate_Always_ReturnsSixAsciiDigits()
    {
        var generator = new PairingCodeGenerator();

        var codes = Enumerable.Range(0, 1000).Select(_ => generator.Generate()).ToList();

        Assert.All(codes, code => Assert.Matches("^[0-9]{6}$", code));
    }

    [Fact]
    public void Generate_ManyCodes_KeepsLeadingZeros()
    {
        var generator = new PairingCodeGenerator();

        // With 20 000 draws, P(no code < 100000) = 0.9^20000, i.e. practically zero.
        var codes = Enumerable.Range(0, 20_000).Select(_ => generator.Generate());

        Assert.Contains(codes, code => code.StartsWith('0'));
    }
}
