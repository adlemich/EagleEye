using System.Globalization;
using EagleEye.Shared.Models;

namespace EagleEye.TrayClient.UI;

/// <summary>Shows kid messages (ADR-014 §2 as amended by US-006, AC-34).</summary>
internal interface IKidMessagePresenter
{
    /// <summary>Shows the message, or replaces the text of the open dialog; returns at once, without waiting for "OK".</summary>
    KidMessageResult Present(KidMessageDto message);
}

/// <summary>
/// One kid dialog at a time per tray client (US-006 AC-34): the decision is made synchronously with a flag. No dialog
/// open → <c>show</c> creates one on the UI thread (<see cref="KidMessageResult.Shown"/>); a dialog open →
/// <c>replace</c> replaces its text (<see cref="KidMessageResult.Replaced"/>). The dialog calls <see cref="DialogClosed"/>.
/// The texts are rendered in the kid's language (<see cref="KidMessageText"/>) and shown exactly as stored; only line
/// breaks ("\n") become <see cref="Environment.NewLine"/>.
/// </summary>
internal sealed class KidMessagePresenter(Action<KidMessageContent> show, Action<KidMessageContent> replace, Func<KidMessageDto, KidMessageContent> compose)
    : IKidMessagePresenter
{
    private int _open;

    /// <summary>Creates the presenter with the current UI culture (language) and culture (time format) of the tray client.</summary>
    public KidMessagePresenter(Action<KidMessageContent> show, Action<KidMessageContent> replace)
        : this(show, replace, message => KidMessageText.Compose(message, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture))
    {
    }

    /// <inheritdoc />
    public KidMessageResult Present(KidMessageDto message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var content = compose(message);
        content = new KidMessageContent(ToDisplay(content.Text), content.Info);
        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0)
        {
            replace(content);
            return KidMessageResult.Replaced;
        }

        show(content);
        return KidMessageResult.Shown;
    }

    /// <summary>The dialog was closed with "OK" (or by sign-out): the next message opens a new dialog.</summary>
    public void DialogClosed() => Volatile.Write(ref _open, 0);

    /// <summary>A replacement found no dialog (closed at the same moment) and opened a new one.</summary>
    public void DialogReopened() => Volatile.Write(ref _open, 1);

    /// <summary>The text with the platform's line breaks.</summary>
    internal static string ToDisplay(string displayText) =>
        displayText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
