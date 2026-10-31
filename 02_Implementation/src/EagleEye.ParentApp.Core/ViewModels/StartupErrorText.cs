using EagleEye.ParentApp.Core.Abstractions;
using Microsoft.Data.Sqlite;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The start-up error message for an exception from <see cref="MainViewModel.StartAsync"/> (US-002, US-007 Decision 2).
/// A refused secret store gets its own text and does not touch the pairing; the parent restarts the app.
/// </summary>
public static class StartupErrorText
{
    /// <summary>The localized message for a handled start-up failure, or <c>null</c> if the exception is a bug.</summary>
    public static string? For(Exception exception) => exception switch
    {
        SecretStoreUnavailableException => AppTexts.ErrorKeychainAccess,
        SqliteException or InvalidDataException or IOException or UnauthorizedAccessException => AppTexts.ErrorStartup,
        _ => null,
    };
}
