using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// Pairing state of a parent hub connection, kept in <see cref="HubCallerContext.Items"/>
/// (ADR-008 §5). A connection is paired when it carries a device ID.
/// </summary>
internal static class ParentConnectionState
{
    private const string DeviceIdKey = "EagleEye.DeviceId";
    private const string DeviceNameKey = "EagleEye.DeviceName";

    /// <summary>Returns the device ID of a paired connection, or <c>null</c>.</summary>
    public static string? GetDeviceId(HubCallerContext context)
    {
        return context.Items.TryGetValue(DeviceIdKey, out var value) ? value as string : null;
    }

    /// <summary>Returns the device name of a paired connection, or <c>null</c>.</summary>
    public static string? GetDeviceName(HubCallerContext context)
    {
        return context.Items.TryGetValue(DeviceNameKey, out var value) ? value as string : null;
    }

    /// <summary>Whether the connection is authenticated as a paired device.</summary>
    public static bool IsPaired(HubCallerContext context)
    {
        return GetDeviceId(context) is not null;
    }

    /// <summary>Marks the connection as paired.</summary>
    public static void SetPaired(HubCallerContext context, string deviceId, string deviceName)
    {
        context.Items[DeviceIdKey] = deviceId;
        context.Items[DeviceNameKey] = deviceName;
    }

    /// <summary>Marks the connection as unpaired.</summary>
    public static void Clear(HubCallerContext context)
    {
        context.Items.Remove(DeviceIdKey);
        context.Items.Remove(DeviceNameKey);
    }
}
