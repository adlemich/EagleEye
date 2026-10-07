using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>Dialogs on the main window's page (host dialog AC-11, confirmation AC-26).</summary>
public sealed class MauiDialogService : IDialogService
{
    private const int HostMaxLength = 253;

    /// <inheritdoc />
    public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string placeholder, string? initialValue)
    {
        return CurrentPage()?.DisplayPromptAsync(title, message, accept, cancel, placeholder, HostMaxLength, Keyboard.Url, initialValue ?? string.Empty)
            ?? Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        return CurrentPage()?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);
    }

    private static Page? CurrentPage() => Application.Current?.Windows.FirstOrDefault()?.Page;
}
