using EagleEye.Service.Communication;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class HubEndpointGuardTests
{
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
        var guard = new HubEndpointGuard(_ => Task.CompletedTask);

        await Assert.ThrowsAsync<ArgumentNullException>(() => guard.InvokeAsync(null!));
    }

    private static async Task<(bool NextCalled, int StatusCode)> InvokeAsync(string path, int localPort)
    {
        var nextCalled = false;
        var guard = new HubEndpointGuard(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.LocalPort = localPort;

        await guard.InvokeAsync(context);

        return (nextCalled, context.Response.StatusCode);
    }
}
