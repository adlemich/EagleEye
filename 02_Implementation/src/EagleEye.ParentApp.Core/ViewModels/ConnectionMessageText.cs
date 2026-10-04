using EagleEye.ParentApp.Core.Communication;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>Localizes the coordinator's <see cref="ConnectionMessage"/>.</summary>
internal static class ConnectionMessageText
{
    /// <summary>Returns the localized message of the state, or <c>null</c> if there is none.</summary>
    public static string? Compose(ConnectionState state)
    {
        return state.LastMessage switch
        {
            ConnectionMessage.None => null,
            ConnectionMessage.InvalidHost => AppTexts.ErrorInvalidHost,
            ConnectionMessage.Unreachable => AppTexts.Format(AppTexts.ErrorUnreachableFormat, state.Host),
            ConnectionMessage.CodeFormat => AppTexts.ErrorCodeFormat,
            ConnectionMessage.WrongCode => AppTexts.ErrorWrongCode,
            ConnectionMessage.CodeExpired => AppTexts.ErrorCodeExpired,
            ConnectionMessage.NoPendingCode => AppTexts.ErrorNoPendingCode,
            ConnectionMessage.DeviceNameRequired => AppTexts.ErrorDeviceNameRequired,
            ConnectionMessage.CertificateChanged => AppTexts.Format(AppTexts.ErrorCertificateChangedFormat, state.Host),
            ConnectionMessage.PairingLost => AppTexts.Format(AppTexts.InfoPairingLostFormat, state.Host),
            _ => AppTexts.ErrorRemoveFailed,
        };
    }
}
