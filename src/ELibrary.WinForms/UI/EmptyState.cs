namespace ELibrary.WinForms.UI;

/// <summary>
/// What a list shows when it has no rows: an icon, a short title, a hint about what to do next and an
/// optional action button. In a small area it collapses to a single centred line.
/// </summary>
internal sealed class EmptyState : Control
{
    private readonly ThemedButton? _action;
    private string _glyph;
    private string _title;
    private string _message;

    public EmptyState(string glyph, string title, string message, string? actionText = null, Action? action = null)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        _glyph = glyph;
        _title = title;
        _message = message;
        BackColor = Theme.Colors.Surface;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = $"{title}. {message}";

        if (actionText != null && action != null)
        {
            _action = new ThemedButton(actionText, ButtonVariant.Secondary);
            _action.Click += (_, _) => action();
            Controls.Add(_action);
        }
    }

    public void SetContent(string glyph, string title, string message)
    {
        _glyph = glyph;
        _title = title;
        _message = message;
        AccessibleName = $"{title}. {message}";
        PerformLayout();
        Invalidate();
    }

    private bool IsCompact => Height < this.Px(170);

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_action == null)
        {
            return;
        }

        _action.Visible = !IsCompact;
        var (_, _, bottom) = Measure();
        _action.Location = new Point((Width - _action.Width) / 2, bottom + this.Px(Theme.Space.M));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Draw.HighQuality(g);

        if (IsCompact)
        {
            var line = $"{_title} — {_message}";
            var iconSize = this.Px(18);
            var textSize = TextRenderer.MeasureText(line, Theme.Fonts.Small, new Size(Math.Max(1, Width - iconSize - this.Px(40)), 0), Draw.Wrapped);
            var x = (Width - (iconSize + this.Px(8) + textSize.Width)) / 2;
            var y = (Height - textSize.Height) / 2;
            Draw.Glyph(g, _glyph, 11f, Theme.Colors.TextMuted, new Rectangle(Math.Max(0, x), (Height - iconSize) / 2, iconSize, iconSize));
            TextRenderer.DrawText(g, line, Theme.Fonts.Small, new Rectangle(Math.Max(0, x) + iconSize + this.Px(8), y, textSize.Width, textSize.Height),
                Theme.Colors.TextMuted, Draw.Wrapped);
            return;
        }

        var (circle, top, _) = Measure();
        var circleRect = new Rectangle((Width - circle) / 2, top, circle, circle);
        Draw.FillRounded(g, Theme.Colors.PrimarySoft, circleRect, circle / 2f);
        Draw.Glyph(g, _glyph, 20f, Theme.Colors.Primary, circleRect);

        var textWidth = Math.Min(this.Px(380), Width - this.Px(Theme.Space.L) * 2);
        var titleTop = circleRect.Bottom + this.Px(Theme.Space.M);
        var titleHeight = TextRenderer.MeasureText(_title, Theme.Fonts.CardTitle).Height;
        TextRenderer.DrawText(g, _title, Theme.Fonts.CardTitle, new Rectangle((Width - textWidth) / 2, titleTop, textWidth, titleHeight),
            Theme.Colors.Text, Draw.SingleLine | TextFormatFlags.HorizontalCenter);
        var messageSize = MessageSize(textWidth);
        TextRenderer.DrawText(g, _message, Theme.Fonts.Body,
            new Rectangle((Width - textWidth) / 2, titleTop + titleHeight + this.Px(4), textWidth, messageSize.Height),
            Theme.Colors.TextMuted, Draw.Wrapped | TextFormatFlags.HorizontalCenter);
    }

    private Size MessageSize(int width) =>
        TextRenderer.MeasureText(_message, Theme.Fonts.Body, new Size(Math.Max(1, width), 0), Draw.Wrapped | TextFormatFlags.HorizontalCenter);

    /// <summary>Circle size, top of the block and bottom of the message, for a vertically centred block.</summary>
    private (int Circle, int Top, int Bottom) Measure()
    {
        var circle = this.Px(64);
        var textWidth = Math.Min(this.Px(380), Width - this.Px(Theme.Space.L) * 2);
        var titleHeight = TextRenderer.MeasureText(_title, Theme.Fonts.CardTitle).Height;
        var blockHeight = circle + this.Px(Theme.Space.M) + titleHeight + this.Px(4) + MessageSize(textWidth).Height;
        if (_action != null)
        {
            blockHeight += this.Px(Theme.Space.M) + _action.Height;
        }

        var top = Math.Max(this.Px(Theme.Space.M), (Height - blockHeight) / 2);
        var bottom = top + circle + this.Px(Theme.Space.M) + titleHeight + this.Px(4) + MessageSize(textWidth).Height;
        return (circle, top, bottom);
    }
}
