using EagleEye.Shared.Models;

namespace EagleEye.TrayClient.UI;

/// <summary>Decides whether a break-time message is shown (ADR-014 §2, AC-32).</summary>
internal interface IBreakTimeMessagePresenter
{
    /// <summary>Shows the text unless a message is already open; returns at once, without waiting for "OK".</summary>
    KidMessageResult Present(string displayText);
}

/// <summary>
/// One break-time dialog at a time per tray client (AC-32): the decision is made synchronously with a flag, the
/// dialog itself is created on the UI thread by <c>show</c>, which calls its second argument when the dialog closes.
/// The display text is shown exactly as stored; only its line breaks ("\n") become <see cref="Environment.NewLine"/>.
/// </summary>
internal sealed class BreakTimeMessagePresenter(Action<string, Action> show) : IBreakTimeMessagePresenter
{
    private int _open;

    /// <inheritdoc />
    public KidMessageResult Present(string displayText)
    {
        ArgumentNullException.ThrowIfNull(displayText);
        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0)
        {
            return KidMessageResult.AlreadyOpen;
        }

        show(ToDisplay(displayText), () => Volatile.Write(ref _open, 0));
        return KidMessageResult.Shown;
    }

    /// <summary>The text with the platform's line breaks.</summary>
    internal static string ToDisplay(string displayText) =>
        displayText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
