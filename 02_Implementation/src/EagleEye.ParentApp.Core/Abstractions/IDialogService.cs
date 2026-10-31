namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>Modal dialogs shown by view models.</summary>
public interface IDialogService
{
    /// <summary>Asks for a host name; returns <c>null</c> if the user cancels.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string cancel, string placeholder, string? initialValue);

    /// <summary>Asks for a plain text of at most <paramref name="maxLength"/> characters (US-008 group name); <c>null</c> if the user cancels.</summary>
    Task<string?> PromptTextAsync(string title, string message, string accept, string cancel, string? initialValue, int maxLength);

    /// <summary>Asks for confirmation; returns <c>true</c> if the user accepts.</summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Lets the user choose one of <paramref name="options"/> (action sheet, US-008 "Move to …"); the index, or <c>null</c> on cancel.</summary>
    Task<int?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options);
}
