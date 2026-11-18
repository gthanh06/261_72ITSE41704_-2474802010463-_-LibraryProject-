namespace ELibrary.WinForms.UI;

/// <summary>
/// A message box that sits inside a form (for example above a dialog's fields), so a validation or
/// business-rule message is shown next to the input instead of in a second pop-up window.
/// Hidden until <see cref="ShowMessage"/> is called.
/// </summary>
internal sealed class InlineBanner : Control
{
    private Tone _tone = Tone.Danger;

    public InlineBanner()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        Visible = false;
        AccessibleRole = AccessibleRole.Alert;
        Margin = this.Px(0, 0, 0, Theme.Space.M);
    }

    public void ShowMessage(string message, Tone tone, int width)
    {
        Text = message;
        _tone = tone;
        AccessibleName = message;
        Width = width;
        Height = MeasureHeight(width);
        Visible = true;
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        Invalidate();
    }

    public void HideMessage() => Visible = false;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.EffectiveBackColor(Parent));
        Draw.HighQuality(g);
        var rect = new RectangleF(0, 0, Width, Height);
        var radius = this.Px(Theme.Radius.Control);
        Draw.FillRounded(g, Tones.Soft(_tone), rect, radius);
        Draw.StrokeRounded(g, Color.FromArgb(70, Tones.Strong(_tone)), 1f, rect, radius);

        var pad = this.Px(Theme.Space.S);
        var iconSize = this.Px(16);
        Draw.Glyph(g, Tones.Glyph(_tone), 10f, Tones.Strong(_tone), new Rectangle(pad, pad + this.Px(1), iconSize, iconSize));
        var textLeft = pad + iconSize + this.Px(Theme.Space.Xs);
        TextRenderer.DrawText(g, Text, Theme.Fonts.Body, new Rectangle(textLeft, pad, Width - textLeft - pad, Height - pad * 2),
            Tones.Strong(_tone), Draw.Wrapped);
    }

    private int MeasureHeight(int width)
    {
        var pad = this.Px(Theme.Space.S);
        var textWidth = Math.Max(1, width - pad * 2 - this.Px(16 + Theme.Space.Xs));
        var text = TextRenderer.MeasureText(Text, Theme.Fonts.Body, new Size(textWidth, 0), Draw.Wrapped).Height;
        return pad * 2 + Math.Max(this.Px(18), text);
    }
}
