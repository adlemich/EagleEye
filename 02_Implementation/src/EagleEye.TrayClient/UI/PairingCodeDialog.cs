using System.Globalization;
using EagleEye.Shared.Constants;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Topmost window that shows a pairing code at the service PC (FR-TRAY-070, US-002 AC-14,
/// ADR-008 §6). A window instead of a balloon tip, because Focus Assist can suppress balloon tips.
/// Shown in the taskbar so that it can be found again; closes itself when the code expires.
/// </summary>
/// <remarks>
/// The layout has no fixed pixel sizes (ISSUE-004): the window sizes itself to its content,
/// which is measured with the font at the current DPI, so the code and all texts stay fully
/// visible at every display scaling and in every language. Spacing values are logical pixels
/// at 96 DPI and are scaled by WinForms (<see cref="AutoScaleMode.Dpi"/>).
/// </remarks>
internal sealed class PairingCodeDialog : Form
{
    private const float LogicalDpi = 96f;
    private const int OuterPadding = 16;
    private const int RowSpacing = 8;
    private const int ButtonSpacing = 16;
    private const int TextMaxWidth = 480;
    private const int ContentMinWidth = 360;
    private const float CodeFontScale = 2.25f;
    private const int ButtonMinWidth = 80;

    private readonly System.Windows.Forms.Timer _expiryTimer;
    private readonly Font _codeFont;

    /// <summary>Creates the window for the given code.</summary>
    public PairingCodeDialog(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(LogicalDpi, LogicalDpi);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = TrayTexts.PairingTitle;
        Icon = TrayIcons.Load(TrayIcons.Application, SystemInformation.IconSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        // AutoSize adds this padding right and below the content; the layout's Location gives left/top.
        Padding = new Padding(OuterPadding);

        // Relative to the dialog font (Segoe UI 9 pt → about 20 pt), so it scales with it.
        _codeFont = new Font(Font.FontFamily, Font.SizeInPoints * CodeFontScale, FontStyle.Bold, GraphicsUnit.Point);
        var codeLabel = CreateLabel(string.Format(CultureInfo.CurrentCulture, TrayTexts.PairingCodeFormat, code));
        codeLabel.Font = _codeFont;
        var okButton = CreateOkButton();

        Controls.Add(CreateLayout(codeLabel, okButton));
        AcceptButton = okButton;
        CancelButton = okButton;
        ResumeLayout(false);
        PerformLayout();

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

    /// <summary>One auto-sized column: code, instruction, validity, OK button.</summary>
    private static TableLayoutPanel CreateLayout(Label codeLabel, Button okButton)
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Location = new Point(OuterPadding, OuterPadding),
            Margin = Padding.Empty,
            MinimumSize = new Size(ContentMinWidth, 0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.Controls.Add(codeLabel);
        layout.Controls.Add(CreateLabel(TrayTexts.PairingInstruction));
        layout.Controls.Add(CreateLabel(string.Format(
            CultureInfo.CurrentCulture, TrayTexts.PairingValidityFormat, (int)PairingRules.CodeLifetime.TotalMinutes)));
        layout.Controls.Add(okButton);
        return layout;
    }

    /// <summary>A centered label that grows with its text and wraps beyond <see cref="TextMaxWidth"/>.</summary>
    private static Label CreateLabel(string text) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(TextMaxWidth, 0),
        Anchor = AnchorStyles.None,
        TextAlign = ContentAlignment.MiddleCenter,
        Margin = new Padding(0, 0, 0, RowSpacing),
        Text = text,
    };

    private Button CreateOkButton()
    {
        var okButton = new Button
        {
            Text = TrayTexts.Ok,
            DialogResult = DialogResult.OK,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(ButtonMinWidth, 0),
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, ButtonSpacing - RowSpacing, 0, 0),
        };
        okButton.Click += (_, _) => Close();
        return okButton;
    }
}
