using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using EagleEye.Shared.Constants;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// The service PC's address as entered by the parent: a DNS hostname (RFC 1123 labels) or an
/// IPv4/IPv6 address. Schemes, ports, paths and spaces are rejected (US-002 AC-11).
/// </summary>
public sealed class HostAddress
{
    private const int MaxHostLength = 253;
    private const int MaxLabelLength = 63;

    private HostAddress(string display, string uriHost)
    {
        Display = display;
        UriHost = uriHost;
    }

    /// <summary>The host as typed by the parent (trimmed); shown as <c>&lt;host&gt;</c>.</summary>
    public string Display { get; }

    /// <summary>The host as it appears in a URI (IPv6 in brackets).</summary>
    public string UriHost { get; }

    /// <summary>Parses the parent's input.</summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out HostAddress? host)
    {
        host = null;
        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (IPAddress.TryParse(trimmed, out var address) && IsPlainAddress(trimmed, address))
        {
            var uriHost = address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
            host = new HostAddress(trimmed, uriHost);
            return true;
        }

        if (IsValidHostName(trimmed))
        {
            host = new HostAddress(trimmed, trimmed);
            return true;
        }

        return false;
    }

    /// <summary>Returns <c>https://&lt;host&gt;:5443/hubs/parent</c>.</summary>
    public Uri ToParentHubUri()
    {
        return new Uri($"https://{UriHost}:{ServiceDefaults.ParentPort}{HubRoutes.Parent}");
    }

    /// <inheritdoc />
    public override string ToString() => Display;

    private static bool IsPlainAddress(string text, IPAddress address)
    {
        // IPAddress.TryParse also accepts forms such as "1" or "[::1]:80"; only accept an exact IPv4
        // dotted quad or an IPv6 literal without brackets, port or scope.
        return address.AddressFamily == AddressFamily.InterNetwork
            ? text.Count(c => c == '.') == 3 && text.All(c => char.IsAsciiDigit(c) || c == '.')
            : text.All(c => char.IsAsciiHexDigit(c) || c is ':' or '.');
    }

    private static bool IsValidHostName(string text)
    {
        if (text.Length > MaxHostLength)
        {
            return false;
        }

        var labels = text.EndsWith('.') ? text[..^1].Split('.') : text.Split('.');
        return labels.All(IsValidLabel) && !labels[^1].All(char.IsAsciiDigit);
    }

    private static bool IsValidLabel(string label)
    {
        return label.Length is > 0 and <= MaxLabelLength
            && label[0] != '-'
            && label[^1] != '-'
            && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }
}
