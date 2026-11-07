using System.Security;
using Microsoft.Win32;

namespace EagleEye.TrayClient.Overlay;

/// <summary>
/// The overlay settings in <c>HKCU\Software\EagleEye\TrayClient</c> (US-010 Decision 10, ADR-022 §7): four <c>REG_DWORD</c>
/// values of the signed-in account. The installer never touches <c>HKCU</c>, so the values survive sign-out, restart and
/// updates. Thin wrapper over the registry; verified manually.
/// </summary>
internal sealed class RegistryOverlaySettingsStore : IOverlaySettingsStore
{
    private const string KeyPath = @"Software\EagleEye\TrayClient";
    private const string OpacityValue = "OverlayOpacity";
    private const string CornerValue = "OverlayCorner";
    private const string ShowFromValue = "OverlayShowFromMinutes";
    private const string SoundValue = "OverlaySound";

    /// <inheritdoc />
    public OverlaySettings Load()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return OverlaySettings.Sanitize(Read(key, OpacityValue), Read(key, CornerValue), Read(key, ShowFromValue), Read(key, SoundValue));
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return OverlaySettings.Defaults;
        }
    }

    /// <inheritdoc />
    public void Save(OverlaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key.SetValue(OpacityValue, settings.OpacityPercent, RegistryValueKind.DWord);
            key.SetValue(CornerValue, (int)settings.Corner, RegistryValueKind.DWord);
            key.SetValue(ShowFromValue, settings.ShowFromMinutes, RegistryValueKind.DWord);
            key.SetValue(SoundValue, settings.Sound ? 1 : 0, RegistryValueKind.DWord);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Decision 10: a failing write (policy) keeps the value in memory for this run, no message.
        }
    }

    private static int? Read(RegistryKey? key, string name) => key?.GetValue(name) is int value ? value : null;
}
