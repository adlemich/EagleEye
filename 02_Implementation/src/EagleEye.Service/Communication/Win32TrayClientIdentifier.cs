using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using EagleEye.Service.Monitoring;

namespace EagleEye.Service.Communication;

/// <summary>
/// ADR-014 §1: finds the owner PID of the loopback TCP connection with <c>GetExtendedTcpTable</c>
/// (<c>TCP_TABLE_OWNER_PID_CONNECTIONS</c>, IPv4 and IPv6) — the row whose local end is the client's end and whose
/// remote end is the service's end — and accepts it only if that process is the installed tray client
/// (<c>&lt;install folder&gt;\TrayClient\EagleEye.TrayClient.exe</c>, full path) in a user session. Debug builds also
/// accept any <c>EagleEye.TrayClient.exe</c> (smoke tests from the build output). Thin Win32, verified manually.
/// </summary>
public sealed unsafe partial class Win32TrayClientIdentifier(IProcessInspector inspector, string trayClientPath) : ITrayClientIdentifier
{
    private const int TcpTableOwnerPidConnections = 4;
    private const int ErrorInsufficientBuffer = 122;
    private const int InetFamily = 2;
    private const int Inet6Family = 23;
    private const string TrayExe = "EagleEye.TrayClient.exe";

    /// <inheritdoc />
    public TrayClientIdentity? Identify(IPEndPoint serverEndPoint, IPEndPoint clientEndPoint)
    {
        ArgumentNullException.ThrowIfNull(serverEndPoint);
        ArgumentNullException.ThrowIfNull(clientEndPoint);
        if (OwnerOf(serverEndPoint, clientEndPoint) is not { } pid || inspector.Inspect(pid) is not { } facts || facts.SessionId <= 0)
        {
            return null;
        }

        return IsTrayClient(facts.ImagePath) ? new TrayClientIdentity(facts.SessionId, facts.OwnerSid, pid) : null;
    }

    private bool IsTrayClient(string path)
    {
#if DEBUG
        if (string.Equals(Path.GetFileName(path), TrayExe, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
#endif
        return string.Equals(path, trayClientPath, StringComparison.OrdinalIgnoreCase);
    }

    private static int? OwnerOf(IPEndPoint server, IPEndPoint client)
    {
        var family = client.AddressFamily == AddressFamily.InterNetworkV6 && !client.Address.IsIPv4MappedToIPv6 ? Inet6Family : InetFamily;
        var table = ReadTable(family);
        if (table is null)
        {
            return null;
        }

        fixed (byte* data = table)
        {
            var count = *(uint*)data;
            return family == InetFamily ? FindV4(data + sizeof(uint), count, server, client) : FindV6(data + sizeof(uint), count, server, client);
        }
    }

    private static int? FindV4(byte* rows, uint count, IPEndPoint server, IPEndPoint client)
    {
        var clientAddress = Ipv4(client.Address);
        var serverAddress = Ipv4(server.Address);
        var row = (TcpRow*)rows;
        for (var i = 0; i < count; i++, row++)
        {
            if (row->LocalAddr == clientAddress && Port(row->LocalPort) == client.Port
                && row->RemoteAddr == serverAddress && Port(row->RemotePort) == server.Port)
            {
                return (int)row->OwningPid;
            }
        }

        return null;
    }

    private static int? FindV6(byte* rows, uint count, IPEndPoint server, IPEndPoint client)
    {
        var clientAddress = client.Address.GetAddressBytes();
        var serverAddress = server.Address.GetAddressBytes();
        var row = (Tcp6Row*)rows;
        for (var i = 0; i < count; i++, row++)
        {
            if (Port(row->LocalPort) == client.Port && Port(row->RemotePort) == server.Port
                && new ReadOnlySpan<byte>(row->LocalAddr, 16).SequenceEqual(clientAddress)
                && new ReadOnlySpan<byte>(row->RemoteAddr, 16).SequenceEqual(serverAddress))
            {
                return (int)row->OwningPid;
            }
        }

        return null;
    }

    private static byte[]? ReadTable(int family)
    {
        var size = 0;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var buffer = new byte[Math.Max(size, 4)];
            fixed (byte* data = buffer)
            {
                var result = GetExtendedTcpTable(data, ref size, false, family, TcpTableOwnerPidConnections, 0);
                if (result == 0)
                {
                    return buffer;
                }

                if (result != ErrorInsufficientBuffer)
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static uint Ipv4(IPAddress address)
    {
        var bytes = (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).GetAddressBytes();
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static int Port(uint networkOrder) => ((int)(networkOrder & 0xFF) << 8) | (int)((networkOrder >> 8) & 0xFF);

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpRow
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Tcp6Row
    {
        public fixed byte LocalAddr[16];
        public uint LocalScopeId;
        public uint LocalPort;
        public fixed byte RemoteAddr[16];
        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningPid;
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial int GetExtendedTcpTable(
        byte* table, ref int size, [MarshalAs(UnmanagedType.Bool)] bool order, int family, int tableClass, uint reserved);
}
