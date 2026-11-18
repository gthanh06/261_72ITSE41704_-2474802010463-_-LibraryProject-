namespace ELibrary.WinForms.UI;

/// <summary>
/// A dashboard tile: an icon in the tone's colour, a label, a large number that counts up when it is set,
/// and a short caption ("of 1 240 copies"). Shows "–" until a value arrives.
/// </summary>
internal sealed class StatCard : Card
{
    private readonly string _label;
    private readonly string _glyph;
    private readonly Tone _tone;
    private string? _caption;
    private int? _value;
    private float _shown;
    private Tween? _countUp;

    public StatCard(string label, string glyph, Tone tone)
    {
        _label = label;
        _glyph = glyph;
        _tone = tone;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = label;
        MinimumSize = new Size(this.Px(180), this.Px(118));
        Height = MinimumSize.Height;
    }

    /// <summary>Sets the number (counting up from the shown one) and an optional caption under it.</summary>
    public void SetValue(int value, string? caption = null)
    {
        _caption = caption;
        var from = _value.HasValue ? _shown : 0f;
        _value = value;
        AccessibleDescription = caption == null ? value.ToString("N0") : $"{value:N0}, {caption}";
        _countUp?.Stop();
        _countUp = Tween.Run(700, p =>
        {
            _shown = from + (value - from) * p;
            Invalidate();
        }, ease: Easing.OutQuint);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var pad = this.Px(Theme.Space.M);
        var circle = this.Px(36);
        var circleRect = new Rectangle(pad, pad, circle, circle);
        Draw.HighQuality(g);
        Draw.FillRounded(g, Tones.Soft(_tone), circleRect, circle / 2f);
        Draw.Glyph(g, _glyph, 12f, Tones.Strong(_tone), circleRect);

        var textLeft = circleRect.Right + this.Px(Theme.Space.S);
        var textWidth = Width - textLeft - pad;
        TextRenderer.DrawText(g, _label, Theme.Fonts.SmallSemibold, new Rectangle(textLeft, circleRect.Y, textWidth, circle),
            Theme.Colors.TextMuted, Draw.SingleLine);

        var number = _value.HasValue ? ((int)Math.Round(_shown)).ToString("N0") : "–";
        var numberTop = circleRect.Bottom + this.Px(Theme.Space.Xs);
        var numberHeight = TextRenderer.MeasureText(number, Theme.Fonts.PageTitle).Height;
        TextRenderer.DrawText(g, number, Theme.Fonts.PageTitle, new Rectangle(pad, numberTop, Width - pad * 2, numberHeight),
            Theme.Colors.Text, Draw.SingleLine);

        if (_caption != null)
        {
            TextRenderer.DrawText(g, _caption, Theme.Fonts.Small,
                new Rectangle(pad, numberTop + numberHeight, Width - pad * 2, this.Px(18)), Theme.Colors.TextMuted, Draw.SingleLine);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countUp?.Dispose();
        }

        base.Dispose(disposing);
    }
}
