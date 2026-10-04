namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>Modal dialogs shown by view models.</summary>
public interface IDialogService
{
    /// <summary>Asks for a text; returns <c>null</c> if the user cancels.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string cancel, string placeholder, string? initialValue);

    /// <summary>Asks for confirmation; returns <c>true</c> if the user accepts.</summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
