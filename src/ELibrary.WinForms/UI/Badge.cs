using ELibrary.Domain.Enums;

namespace ELibrary.WinForms.UI;

/// <summary>
/// A value shown as a status pill. Put it in a grid column and <see cref="GridStyle"/> paints it as a badge;
/// it sorts and copies as its text.
/// </summary>
internal readonly record struct BadgeValue(string Text, Tone Tone) : IComparable<BadgeValue>, IComparable
{
    public int CompareTo(BadgeValue other) => string.Compare(Text, other.Text, StringComparison.CurrentCulture);

    public int CompareTo(object? obj) => obj is BadgeValue other ? CompareTo(other) : 1;

    public override string ToString() => Text;
}

/// <summary>The wording and tone of every status the application shows as a badge.</summary>
internal static class StatusBadges
{
    public static BadgeValue For(ReservationStatus status) => status switch
    {
        ReservationStatus.Waiting => new("Waiting", Tone.Warning),
        ReservationStatus.OnHold => new("Ready for pickup", Tone.Info),
        ReservationStatus.Fulfilled => new("Collected", Tone.Success),
        ReservationStatus.Cancelled => new("Cancelled", Tone.Neutral),
        ReservationStatus.Expired => new("Expired", Tone.Danger),
        _ => new(status.ToString(), Tone.Neutral)
    };

    public static BadgeValue Account(bool isLocked) =>
        isLocked ? new("Locked", Tone.Danger) : new("Active", Tone.Success);

    public static BadgeValue Role(RoleCode role) => role switch
    {
        RoleCode.Administrator => new("Administrator", Tone.Accent),
        RoleCode.Librarian => new("Librarian", Tone.Info),
        _ => new("Member", Tone.Neutral)
    };
}

/// <summary>A small status pill: a coloured dot and a label on a soft background.</summary>
internal sealed class Badge : Control
{
    private Tone _tone;

    public Badge(string text, Tone tone = Tone.Neutral)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        Text = text;
        _tone = tone;
        Font = Theme.Fonts.SmallSemibold;
        Margin = this.Px(0, 0, Theme.Space.Xs, Theme.Space.Xs);
        AccessibleRole = AccessibleRole.StaticText;
        AutoSize = true;
    }

    public Badge(BadgeValue value) : this(value.Text, value.Tone)
    {
    }

    public Tone Tone
    {
        get => _tone;
        set
        {
            _tone = value;
            Invalidate();
        }
    }

    public BadgeValue Value
    {
        set
        {
            Text = value.Text;
            Tone = value.Tone;
        }
    }

    public override Size GetPreferredSize(Size proposedSize) => Measure(this, Text, Font);

    /// <summary>Paints a badge at the left of <paramref name="cell"/>, vertically centred (used by grids too).</summary>
    public static void PaintPill(Graphics g, Control dpiSource, Rectangle cell, string text, Tone tone, Font font)
    {
        var size = Measure(dpiSource, text, font);
        var rect = new Rectangle(cell.X, cell.Y + (cell.Height - size.Height) / 2, Math.Min(size.Width, cell.Width), size.Height);
        Draw.HighQuality(g);
        Draw.FillRounded(g, Tones.Soft(tone), rect, rect.Height / 2f);

        var dot = dpiSource.Px(6);
        var padLeft = dpiSource.Px(8);
        var strong = Tones.Strong(tone);
        using (var brush = new SolidBrush(strong))
        {
            g.FillEllipse(brush, rect.X + padLeft, rect.Y + (rect.Height - dot) / 2f, dot, dot);
        }

        var textLeft = rect.X + padLeft + dot + dpiSource.Px(5);
        TextRenderer.DrawText(g, text, font, new Rectangle(textLeft, rect.Y, rect.Right - textLeft, rect.Height), strong,
            Draw.SingleLine | TextFormatFlags.NoPadding);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AccessibleName = Text;
        if (AutoSize)
        {
            Size = GetPreferredSize(Size.Empty);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Draw.EffectiveBackColor(Parent));
        PaintPill(e.Graphics, this, ClientRectangle, Text, _tone, Font);
    }

    private static Size Measure(Control dpiSource, string text, Font font)
    {
        var textSize = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), Draw.MeasureLine | TextFormatFlags.NoPadding);
        var width = dpiSource.Px(8) + dpiSource.Px(6) + dpiSource.Px(5) + textSize.Width + dpiSource.Px(10);
        return new Size(width, Math.Max(dpiSource.Px(22), textSize.Height + dpiSource.Px(6)));
    }
}
