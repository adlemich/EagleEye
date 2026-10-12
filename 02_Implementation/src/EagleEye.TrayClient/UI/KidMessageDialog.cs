namespace EagleEye.TrayClient.UI;

/// <summary>
/// The kid dialog (FR-TRAY-020 to FR-TRAY-022, US-005 AC-30, AC-31, US-006 AC-34, ADR-014 §3): title "EagleEye", an
/// information icon, the parent's text exactly as stored (line breaks, emojis; GDI draws emojis in monochrome), below a
/// separator EagleEye's info line in the normal UI font, one "OK". A newer message replaces the text (<see cref="UpdateText"/>).
/// Topmost, shown in the taskbar, centred, no close box, no minimise/maximise. Only "OK" and Enter close it; Esc and
/// Alt+F4 are ignored; sign-out, shutdown and Task Manager always close it. Sizes itself to the text (max. width 480
/// logical pixels, a scroll bar only for very long texts); DPI-scaled like <see cref="PairingCodeDialog"/>.
/// Verified manually.
/// </summary>
internal sealed class KidMessageDialog : Form
{
    private const float LogicalDpi = 96f;
    private const int OuterPadding = 16;
    private const int Spacing = 12;
    private const int TextMaxWidth = 480;
    private const int TextMaxHeight = 360;
    private const int ButtonMinWidth = 88;

    private const string EmojiFontFamily = "Segoe UI Emoji";

    private readonly Font _textFont;
    private readonly Label _text;
    private readonly Label _info;
    private bool _acknowledged;

    /// <summary>Creates the dialog for the content (line breaks already in the platform's form).</summary>
    public KidMessageDialog(KidMessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        SuspendLayout();
        AutoScaleDimensions = new SizeF(LogicalDpi, LogicalDpi);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = TrayTexts.BreakMessageTitle;
        Icon = TrayIcons.Load(TrayIcons.Application, SystemInformation.IconSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(OuterPadding);
        KeyPreview = true;

        var okButton = new Button
        {
            Text = TrayTexts.Ok,
            AutoSize = true,
            MinimumSize = new Size(ButtonMinWidth, 0),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, Spacing, 0, 0),
        };
        okButton.Click += (_, _) => Acknowledge();
        AcceptButton = okButton;

        // GDI draws emojis only from an emoji font (smoke check: boxes with the dialog font). "Segoe UI Emoji" also has the
        // Latin letters, so the whole text uses it, monochrome (ADR-014 §3, AC-31).
        _textFont = new Font(EmojiFontFamily, Font.SizeInPoints, FontStyle.Regular, GraphicsUnit.Point);
        _text = new Label { AutoSize = true, MaximumSize = new Size(TextMaxWidth, 0), UseMnemonic = false, Font = _textFont, Text = content.Text, Margin = Padding.Empty };
        _info = new Label { AutoSize = true, MaximumSize = new Size(TextMaxWidth, 0), UseMnemonic = false, Text = content.Info, Margin = new Padding(0, Spacing, 0, 0) };
        Controls.Add(CreateLayout(_text, _info, okButton));
        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>A newer message replaces the text (US-006 AC-34): re-layout, re-assert topmost, one foreground attempt.</summary>
    public void UpdateText(KidMessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        SuspendLayout();
        _text.Text = content.Text;
        _info.Text = content.Info;
        ResumeLayout(true);
        TopMost = false;
        TopMost = true;
        ForegroundHelper.BringToFront(this);
    }

    /// <inheritdoc />
    protected override bool ProcessDialogKey(Keys keyData)
    {
        // Esc must not close the message (AC-30); Enter is handled by the AcceptButton.
        return keyData == Keys.Escape || base.ProcessDialogKey(keyData);
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Alt+F4 arrives as UserClosing and is ignored; sign-out, shutdown and Task Manager are never blocked.
        if (!_acknowledged && e.CloseReason is CloseReason.UserClosing or CloseReason.None)
        {
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    private void Acknowledge()
    {
        _acknowledged = true;
        Close();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private static TableLayoutPanel CreateLayout(Label text, Label info, Button okButton)
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Location = new Point(OuterPadding, OuterPadding),
            Margin = Padding.Empty,
        };
        var icon = new PictureBox
        {
            Image = SystemIcons.Information.ToBitmap(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Margin = new Padding(0, 0, Spacing, 0),
        };
        layout.Controls.Add(icon, 0, 0);
        layout.Controls.Add(CreateText(text), 1, 0);
        layout.Controls.Add(new Label { AutoSize = false, Height = 2, Width = 1, Anchor = AnchorStyles.Left | AnchorStyles.Right, BorderStyle = BorderStyle.Fixed3D, Margin = new Padding(0, Spacing, 0, 0) }, 1, 1);
        layout.Controls.Add(info, 1, 2);
        layout.Controls.Add(okButton, 1, 3);
        return layout;
    }

    /// <summary>The text in a panel that scrolls only when the text is very long.</summary>
    private static Panel CreateText(Label label)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = true,
            MaximumSize = new Size(TextMaxWidth + SystemInformation.VerticalScrollBarWidth, TextMaxHeight),
            Margin = Padding.Empty,
        };
        panel.Controls.Add(label);
        return panel;
    }
}
