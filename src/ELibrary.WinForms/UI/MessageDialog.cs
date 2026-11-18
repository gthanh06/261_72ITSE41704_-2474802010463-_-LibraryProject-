using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// The themed replacement for MessageBox: an icon in the tone's colour, a title, the message and
/// clearly named buttons. Confirmations of destructive actions use a red button and start with the
/// focus on Cancel, so pressing Enter by accident does not delete or cancel anything.
/// </summary>
internal sealed class MessageDialog : Form
{
    private readonly Tone _tone;
    private readonly string _title;
    private readonly string _message;
    private readonly FlowLayoutPanel _buttons;
    private readonly Control? _initialFocus;

    private MessageDialog(string title, string message, Tone tone, string confirmText, string? cancelText, bool destructive)
    {
        _title = title;
        _message = message;
        _tone = tone;
        Text = title;
        AccessibleName = title;
        AccessibleDescription = message;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        KeyPreview = true;
        BackColor = Theme.Colors.Surface;
        ForeColor = Theme.Colors.Text;
        Font = Theme.Fonts.Body;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);

        var confirm = new ThemedButton(confirmText, destructive ? ButtonVariant.Danger : ButtonVariant.Primary)
        {
            DialogResult = DialogResult.OK
        };
        _buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Colors.Surface
        };
        _buttons.Controls.Add(confirm);
        AcceptButton = confirm;
        CancelButton = confirm;

        if (cancelText != null)
        {
            var cancel = new ThemedButton(cancelText, ButtonVariant.Secondary) { DialogResult = DialogResult.Cancel };
            _buttons.Controls.Add(cancel);
            CancelButton = cancel;
            if (destructive)
            {
                _initialFocus = cancel;
            }
        }

        Controls.Add(_buttons);
        ClientSize = new Size(this.Px(460), MeasureHeight());
        _buttons.Location = new Point(ClientSize.Width - _buttons.Width - this.Px(Theme.Space.L) + this.Px(Theme.Space.Xxs),
            ClientSize.Height - _buttons.Height - this.Px(Theme.Space.M));
    }

    /// <summary>Shows a message with an OK button.</summary>
    public static void Inform(IWin32Window? owner, string title, string message, Tone tone = Tone.Info)
    {
        using var dialog = new MessageDialog(title, message, tone, "OK", null, destructive: false);
        Run(dialog, owner);
    }

    /// <summary>Asks for confirmation; returns true when the user chose <paramref name="confirmText"/>.</summary>
    public static bool Confirm(IWin32Window? owner, string title, string message, string confirmText = "Confirm",
        string cancelText = "Cancel", bool destructive = false)
    {
        using var dialog = new MessageDialog(title, message, destructive ? Tone.Danger : Tone.Warning, confirmText, cancelText, destructive);
        return Run(dialog, owner) == DialogResult.OK;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int csDropShadow = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= csDropShadow;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // A control can only take the focus once the window is visible.
        _initialFocus?.Focus();
        DimOverlay.AttachTo(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        Draw.HighQuality(g);
        using (var pen = new Pen(Theme.Colors.BorderStrong))
        {
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        var pad = this.Px(Theme.Space.L);
        var circle = this.Px(40);
        var circleRect = new Rectangle(pad, pad, circle, circle);
        Draw.FillRounded(g, Tones.Soft(_tone), circleRect, circle / 2f);
        Draw.Glyph(g, Tones.Glyph(_tone), 13f, Tones.Strong(_tone), circleRect);

        var (textLeft, textWidth) = TextColumn();
        var titleHeight = TextRenderer.MeasureText(_title, Theme.Fonts.CardTitle).Height;
        TextRenderer.DrawText(g, _title, Theme.Fonts.CardTitle, new Rectangle(textLeft, pad, textWidth, titleHeight),
            Theme.Colors.Text, Draw.SingleLine);
        TextRenderer.DrawText(g, _message, Theme.Fonts.Body,
            new Rectangle(textLeft, pad + titleHeight + this.Px(Theme.Space.Xxs), textWidth, MessageHeight(textWidth)),
            Theme.Colors.Text, Draw.Wrapped);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            // Borderless window: let the user drag it by its background.
            ReleaseCapture();
            SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
        }
    }

    private static DialogResult Run(MessageDialog dialog, IWin32Window? owner)
    {
        var ownerForm = (owner as Control)?.FindForm() ?? owner as Form ?? Form.ActiveForm;
        if (ownerForm is { Visible: true })
        {
            dialog.StartPosition = FormStartPosition.CenterParent;
            return dialog.ShowDialog(ownerForm);
        }

        dialog.StartPosition = FormStartPosition.CenterScreen;
        return dialog.ShowDialog();
    }

    private (int Left, int Width) TextColumn()
    {
        var pad = this.Px(Theme.Space.L);
        var left = pad + this.Px(40) + this.Px(Theme.Space.M);
        return (left, this.Px(460) - left - pad);
    }

    private int MessageHeight(int width) =>
        TextRenderer.MeasureText(_message, Theme.Fonts.Body, new Size(Math.Max(1, width), 0), Draw.Wrapped).Height;

    private int MeasureHeight()
    {
        var pad = this.Px(Theme.Space.L);
        var (_, textWidth) = TextColumn();
        var text = TextRenderer.MeasureText(_title, Theme.Fonts.CardTitle).Height + this.Px(Theme.Space.Xxs) + MessageHeight(textWidth);
        var content = Math.Max(this.Px(40), text);
        return pad + content + this.Px(Theme.Space.L) + _buttons.Height + this.Px(Theme.Space.M);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
