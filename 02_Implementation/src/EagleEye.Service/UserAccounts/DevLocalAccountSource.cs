#if DEBUG
namespace EagleEye.Service.UserAccounts;

/// <summary>
/// <b>Debug builds only</b> (ADR-011 T-13): reports the account of <see cref="DevSettings.WatchSidVariable"/> as a
/// standard account, so DEV can tick their own (admin) account in the parent app for the smoke check.
/// </summary>
public sealed class DevLocalAccountSource(ILocalAccountSource inner, string watchedSid) : ILocalAccountSource
{
    /// <inheritdoc />
    public IReadOnlyList<LocalAccountInfo> GetAccounts()
    {
        return [.. inner.GetAccounts().Select(a => StringComparer.OrdinalIgnoreCase.Equals(a.Sid, watchedSid) ? a with { IsAdmin = false } : a)];
    }
}
#endif
