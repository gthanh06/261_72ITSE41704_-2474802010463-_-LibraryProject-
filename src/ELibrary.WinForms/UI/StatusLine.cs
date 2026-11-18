namespace ELibrary.WinForms.UI;

/// <summary>
/// The one-line result message at the bottom of a screen ("12 titles found", "Copy B0012 was added").
/// A new message briefly tints the line so that the change is noticed; errors are red with an icon.
/// </summary>
internal sealed class StatusLine : Control
{
    private string _message = string.Empty;
    private Tone _tone = Tone.Success;
    private float _flash;
    private Tween? _tween;

    public StatusLine()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        Height = this.Px(40);
        AccessibleRole = AccessibleRole.StatusBar;
        AccessibleName = "Status";
    }

    public string Message => _message;

    public void ShowMessage(string message, Tone tone)
    {
        _message = message;
        _tone = tone;
        AccessibleName = string.IsNullOrEmpty(message) ? "Status" : message;
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        _tween?.Stop();
        _tween = Tween.Run(900, p =>
        {
            _flash = 1f - p;
            Invalidate();
        });
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var back = Draw.EffectiveBackColor(Parent);
        g.Clear(back);
        using (var pen = new Pen(Theme.Colors.Border))
        {
            g.DrawLine(pen, 0, 0, Width, 0);
        }

        if (_message.Length == 0)
        {
            return;
        }

        var isError = _tone == Tone.Danger;
        if (_flash > 0f)
        {
            Draw.HighQuality(g);
            var tint = Color.FromArgb((int)(255 * _flash * 0.9f), Tones.Soft(_tone));
            Draw.FillRounded(g, tint, new RectangleF(0, this.Px(6), Width, Height - this.Px(10)), this.Px(Theme.Radius.Control));
        }

        var iconSize = this.Px(16);
        var x = this.Px(4);
        Draw.Glyph(g, Tones.Glyph(_tone), 10f, Tones.Strong(_tone), new Rectangle(x, (Height - iconSize) / 2 + this.Px(2), iconSize, iconSize));
        x += iconSize + this.Px(Theme.Space.Xs);
        TextRenderer.DrawText(g, _message, Theme.Fonts.Small, new Rectangle(x, this.Px(2), Width - x, Height - this.Px(2)),
            isError ? Theme.Colors.Danger : Theme.Colors.TextMuted, Draw.SingleLine);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tween?.Dispose();
        }

        base.Dispose(disposing);
    }
}
