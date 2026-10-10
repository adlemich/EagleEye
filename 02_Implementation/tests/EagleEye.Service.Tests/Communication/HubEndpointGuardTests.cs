using EagleEye.Service.Communication;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class HubEndpointGuardTests
{
    private static readonly EndpointPorts StandardPorts = new(5080, 5443);

    [Theory]
    [InlineData("/hubs/tray", 15080, true)]
    [InlineData("/hubs/parent", 15443, true)]
    [InlineData("/hubs/tray", 5080, false)]
    [InlineData("/hubs/parent", 5443, false)]
    public async Task InvokeAsync_ShiftedPorts_UsesConfiguredPorts(string path, int localPort, bool allowed)
    {
        var (nextCalled, _) = await InvokeAsync(path, localPort, new EndpointPorts(15080, 15443));

        Assert.Equal(allowed, nextCalled);
    }

    [Theory]
    [InlineData("/hubs/tray", 5080)]
    [InlineData("/hubs/tray/negotiate", 5080)]
    [InlineData("/hubs/parent", 5443)]
    [InlineData("/HUBS/PARENT/negotiate", 5443)]
    [InlineData("/", 5443)]
    [InlineData("/", 5080)]
    [InlineData("/hubs/trayx", 5443)]
    public async Task InvokeAsync_AllowedRequest_CallsNext(string path, int localPort)
    {
        var (nextCalled, statusCode) = await InvokeAsync(path, localPort);

        Assert.Equal((true, StatusCodes.Status200OK), (nextCalled, statusCode));
    }

    [Theory]
    [InlineData("/hubs/tray", 5443)]
    [InlineData("/hubs/tray/negotiate", 5443)]
    [InlineData("/Hubs/Tray", 5443)]
    [InlineData("/hubs/parent", 5080)]
    [InlineData("/hubs/parent/negotiate", 5080)]
    [InlineData("/hubs/tray", 8080)]
    public async Task InvokeAsync_HubOnWrongPort_Returns404(string path, int localPort)
    {
        var (nextCalled, statusCode) = await InvokeAsync(path, localPort);

        Assert.Equal((false, StatusCodes.Status404NotFound), (nextCalled, statusCode));
    }

    [Fact]
    public async Task InvokeAsync_NullContext_ThrowsArgumentNullException()
    {
        var guard = new HubEndpointGuard(_ => Task.CompletedTask, StandardPorts);

        await Assert.ThrowsAsync<ArgumentNullException>(() => guard.InvokeAsync(null!));
    }

    private static async Task<(bool NextCalled, int StatusCode)> InvokeAsync(string path, int localPort, EndpointPorts? ports = null)
    {
        var nextCalled = false;
        var guard = new HubEndpointGuard(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, ports ?? StandardPorts);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.LocalPort = localPort;

        await guard.InvokeAsync(context);

        return (nextCalled, context.Response.StatusCode);
    }
}
