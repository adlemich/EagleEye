using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace EagleEye.Service.Tests.Communication;

/// <summary>Creates mocked <see cref="HubCallerContext"/>s backed by a real <see cref="DefaultHttpContext"/>.</summary>
internal static class HubContextFactory
{
    public static Mock<HubCallerContext> Create(
        string connectionId,
        IPAddress? remoteAddress = null,
        string? authorization = null,
        bool withHttpContext = true,
        IPAddress? localAddress = null)
    {
        var features = new FeatureCollection();
        if (withHttpContext)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = remoteAddress;
            httpContext.Connection.RemotePort = 50123;
            httpContext.Connection.LocalIpAddress = localAddress;
            httpContext.Connection.LocalPort = 5080;
            if (authorization is not null)
            {
                httpContext.Request.Headers.Authorization = authorization;
            }

            features.Set<IHttpContextFeature>(new HttpContextFeature(httpContext));
        }

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(connectionId);
        context.SetupGet(c => c.Items).Returns(new Dictionary<object, object?>());
        context.SetupGet(c => c.Features).Returns(features);
        return context;
    }

    private sealed class HttpContextFeature(HttpContext httpContext) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }
}
