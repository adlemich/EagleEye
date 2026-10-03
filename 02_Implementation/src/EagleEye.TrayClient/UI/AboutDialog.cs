using System.Globalization;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Shows the server version, queried live from the service when the dialog is opened (AC-12).
/// </summary>
internal sealed class AboutDialog : Form
{
    private const int DialogWidth = 300;
    private const int DialogHeight = 150;
    private const int Margin16 = 16;
    private const int ButtonWidth = 80;
    private const int ButtonHeight = 28;

    /// <summary>Creates the dialog.</summary>
    /// <param name="serverVersion">The version string, or <c>null</c> if it could not be retrieved.</param>
    public AboutDialog(string? serverVersion)
    {
        Text = TrayTexts.AboutTitle;
        Icon = TrayIcons.Load(TrayIcons.Application, SystemInformation.IconSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(DialogWidth, DialogHeight);

        var versionLabel = new Label
        {
            AutoSize = false,
            Location = new Point(Margin16, Margin16),
            Size = new Size(DialogWidth - (2 * Margin16), DialogHeight - ButtonHeight - (3 * Margin16)),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = string.Format(
                CultureInfo.CurrentCulture,
                TrayTexts.ServerVersionFormat,
                serverVersion ?? TrayTexts.VersionUnavailable),
        };

        var okButton = new Button
        {
            Text = TrayTexts.Ok,
            DialogResult = DialogResult.OK,
            Size = new Size(ButtonWidth, ButtonHeight),
            Location = new Point((DialogWidth - ButtonWidth) / 2, DialogHeight - ButtonHeight - Margin16),
        };

        Controls.Add(versionLabel);
        Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = okButton;
    }
}
