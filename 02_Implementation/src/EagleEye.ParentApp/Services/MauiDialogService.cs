using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>Dialogs on the main window's page (host dialog AC-11, confirmation AC-26; US-008 name dialog and "Move to …").</summary>
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
    public Task<string?> PromptTextAsync(string title, string message, string accept, string cancel, string? initialValue, int maxLength)
    {
        return CurrentPage()?.DisplayPromptAsync(title, message, accept, cancel, null, maxLength, Keyboard.Text, initialValue ?? string.Empty)
            ?? Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        return CurrentPage()?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<int?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options)
    {
        if (CurrentPage() is not { } page)
        {
            return null;
        }

        // The sheet returns the text of the chosen button, or the cancel text (also when closed with Escape).
        var choice = await page.DisplayActionSheetAsync(title, cancel, null, [.. options]);
        var index = choice is null || choice == cancel ? -1 : options.ToList().IndexOf(choice);
        return index < 0 ? null : index;
    }

    private static Page? CurrentPage() => Application.Current?.Windows.FirstOrDefault()?.Page;
}
