using System.Globalization;
using EagleEye.Shared.Constants;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Topmost window that shows a pairing code at the service PC (FR-TRAY-070, US-002 AC-14,
/// ADR-008 §6). A window instead of a balloon tip, because Focus Assist can suppress balloon tips.
/// Shown in the taskbar so that it can be found again; closes itself when the code expires.
/// </summary>
internal sealed class PairingCodeDialog : Form
{
    private const int DialogWidth = 420;
    private const int Margin16 = 16;
    private const int TextHeight = 24;
    private const int CodeHeight = 64;
    private const float CodeFontSize = 20f;
    private const int ButtonWidth = 80;
    private const int ButtonHeight = 28;

    private readonly System.Windows.Forms.Timer _expiryTimer;
    private readonly Font _codeFont;

    /// <summary>Creates the window for the given code.</summary>
    public PairingCodeDialog(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Text = TrayTexts.PairingTitle;
        Icon = TrayIcons.Load(TrayIcons.Application, SystemInformation.IconSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;

        var y = Margin16;
        var codeLabel = CreateLabel(
            string.Format(CultureInfo.CurrentCulture, TrayTexts.PairingCodeFormat, code), ref y, CodeHeight);
        _codeFont = new Font(Font.FontFamily, CodeFontSize, FontStyle.Bold);
        codeLabel.Font = _codeFont;
        var instructionLabel = CreateLabel(TrayTexts.PairingInstruction, ref y, TextHeight);
        var validityLabel = CreateLabel(
            string.Format(CultureInfo.CurrentCulture, TrayTexts.PairingValidityFormat, (int)PairingRules.CodeLifetime.TotalMinutes),
            ref y,
            TextHeight);

        var okButton = new Button
        {
            Text = TrayTexts.Ok,
            DialogResult = DialogResult.OK,
            Size = new Size(ButtonWidth, ButtonHeight),
            Location = new Point((DialogWidth - ButtonWidth) / 2, y + Margin16),
        };
        okButton.Click += (_, _) => Close();

        ClientSize = new Size(DialogWidth, okButton.Bottom + Margin16);
        Controls.AddRange([codeLabel, instructionLabel, validityLabel, okButton]);
        AcceptButton = okButton;
        CancelButton = okButton;

        _expiryTimer = new System.Windows.Forms.Timer { Interval = (int)PairingRules.CodeLifetime.TotalMilliseconds };
        _expiryTimer.Tick += (_, _) => Close();
        _expiryTimer.Start();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _expiryTimer.Dispose();
            _codeFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Label CreateLabel(string text, ref int y, int height)
    {
        var label = new Label
        {
            AutoSize = false,
            Location = new Point(Margin16, y),
            Size = new Size(DialogWidth - (2 * Margin16), height),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = text,
        };
        y += height + (Margin16 / 2);
        return label;
    }
}
