using System.Security.AccessControl;
using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class AgentStartSpecTests
{
    private const string ServicePath = @"C:\Program Files\EagleEye\Service\EagleEye.Service.exe";

    private readonly AgentStartSpec _spec = AgentStartSpec.Create(ServicePath, @"C:\Windows", "C:");

    [Fact]
    public void Create_AbsoluteApplicationNameFixedArgumentInstallFolder()
    {
        Assert.Equal(
            (ServicePath, "\"C:\\Program Files\\EagleEye\\Service\\EagleEye.Service.exe\" --session-agent", @"C:\Program Files\EagleEye\Service"),
            (_spec.ApplicationName, _spec.CommandLine, _spec.CurrentDirectory));
    }

    [Fact]
    public void Create_MinimalEnvironment()
    {
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["DOTNET_EnableDiagnostics"] = "0",
                ["PATH"] = @"C:\Windows\System32",
                ["SystemDrive"] = "C:",
                ["SystemRoot"] = @"C:\Windows",
                ["TEMP"] = @"C:\Windows\Temp",
                ["TMP"] = @"C:\Windows\Temp",
                ["windir"] = @"C:\Windows",
            },
            _spec.Environment.ToDictionary(e => e.Key, e => e.Value));
    }

    [Fact]
    public void Create_NoCodeLoadingVariables()
    {
        Assert.DoesNotContain(_spec.Environment, e =>
            (e.Key.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) && e.Key != "DOTNET_EnableDiagnostics")
            || e.Key.StartsWith("COR_", StringComparison.OrdinalIgnoreCase)
            || e.Key.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnvironmentBlock_SortedNulSeparatedDoubleNulTerminated()
    {
        Assert.Equal(
            "DOTNET_EnableDiagnostics=0\0PATH=C:\\Windows\\System32\0SystemDrive=C:\0SystemRoot=C:\\Windows\0" +
            "TEMP=C:\\Windows\\Temp\0TMP=C:\\Windows\\Temp\0windir=C:\\Windows\0\0",
            _spec.EnvironmentBlock());
    }

    [Theory]
    [InlineData(nameof(AgentStartSpec.ProcessSecurityDescriptor), 0x101001)]
    [InlineData(nameof(AgentStartSpec.ThreadSecurityDescriptor), 0x100801)]
    public void SecurityDescriptor_ProtectedSystemFullAdministratorsLimitedNobodyElse(string property, int adminMask)
    {
        var sddl = (string)typeof(AgentStartSpec).GetProperty(property)!.GetValue(_spec)!;
        var descriptor = new RawSecurityDescriptor(sddl);
        var aces = descriptor.DiscretionaryAcl!.Cast<CommonAce>().ToList();

        Assert.True((descriptor.ControlFlags & ControlFlags.DiscretionaryAclProtected) != 0);
        Assert.Equal("S-1-5-18", descriptor.Owner!.Value);
        Assert.Equal(
            [("S-1-5-18", unchecked((int)0x10000000)), ("S-1-5-32-544", adminMask)],
            aces.Select(a => (a.SecurityIdentifier.Value, a.AccessMask)));
        Assert.All(aces, a => Assert.Equal((AceQualifier.AccessAllowed, AceFlags.None), (a.AceQualifier, a.AceFlags)));
    }

    [Theory]
    [InlineData(@"EagleEye.Service.exe")]
    [InlineData(@"C:\Program Files\Eagle""Eye\EagleEye.Service.exe")]
    public void Create_RelativeOrQuotedPath_Throws(string path)
    {
        Assert.Throws<ArgumentException>(() => AgentStartSpec.Create(path, @"C:\Windows", "C:"));
    }

    [Theory]
    [InlineData("", @"C:\Windows", "C:")]
    [InlineData(ServicePath, " ", "C:")]
    [InlineData(ServicePath, @"C:\Windows", "")]
    public void Create_MissingValues_Throw(string path, string systemRoot, string drive)
    {
        Assert.ThrowsAny<ArgumentException>(() => AgentStartSpec.Create(path, systemRoot, drive));
    }
}
