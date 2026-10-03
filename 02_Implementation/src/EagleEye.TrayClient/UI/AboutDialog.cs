namespace EagleEye.TrayClient.UI;

/// <summary>
/// Application information dialog: shows the server version queried live from the service
/// (AC-12), or a connection error naming the server address (AC-13). See <see cref="AboutText"/>.
/// </summary>
internal sealed class AboutDialog : Form
{
    private const int DialogWidth = 380;
    private const int DialogHeight = 160;
    private const int Margin16 = 16;
    private const int ButtonWidth = 80;
    private const int ButtonHeight = 28;

    /// <summary>Creates the dialog.</summary>
    /// <param name="text">The localized body text, composed by <see cref="AboutText.Compose"/>.</param>
    public AboutDialog(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = TrayTexts.AboutTitle;
        Icon = TrayIcons.Load(TrayIcons.Application, SystemInformation.IconSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(DialogWidth, DialogHeight);

        var textLabel = new Label
        {
            AutoSize = false,
            Location = new Point(Margin16, Margin16),
            Size = new Size(DialogWidth - (2 * Margin16), DialogHeight - ButtonHeight - (3 * Margin16)),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = text,
        };

        var okButton = new Button
        {
            Text = TrayTexts.Ok,
            DialogResult = DialogResult.OK,
            Size = new Size(ButtonWidth, ButtonHeight),
            Location = new Point((DialogWidth - ButtonWidth) / 2, DialogHeight - ButtonHeight - Margin16),
        };

        Controls.Add(textLabel);
        Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = okButton;
    }
}
