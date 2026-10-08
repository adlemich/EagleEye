using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

/// <summary>Debug-only port offset of the parent hub URI (tests run in Debug; Release does not contain it).</summary>
[Collection("Environment variables")]
public sealed class DevPortOffsetTests
{
    [Theory]
    [InlineData(null, "https://kid-pc:5443/hubs/parent")]
    [InlineData("10000", "https://kid-pc:15443/hubs/parent")]
    [InlineData("50001", "https://kid-pc:5443/hubs/parent")]
    [InlineData("x", "https://kid-pc:5443/hubs/parent")]
    public void ToParentHubUri_DebugOffset(string? offset, string expected)
    {
        var original = Environment.GetEnvironmentVariable(HostAddress.DevPortOffsetVariable);
        try
        {
            Environment.SetEnvironmentVariable(HostAddress.DevPortOffsetVariable, offset);
            Assert.True(HostAddress.TryParse("kid-pc", out var host));

            Assert.Equal(new Uri(expected), host.ToParentHubUri());
        }
        finally
        {
            Environment.SetEnvironmentVariable(HostAddress.DevPortOffsetVariable, original);
        }
    }
}
