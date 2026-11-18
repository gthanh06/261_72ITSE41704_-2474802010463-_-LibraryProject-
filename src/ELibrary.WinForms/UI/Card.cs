namespace ELibrary.WinForms.UI;

/// <summary>
/// A white rounded panel with a hairline border and a soft shadow, optionally with a heading and a
/// short description. Child controls go inside <see cref="Panel.Padding"/>, below the heading.
/// </summary>
internal class Card : Panel
{
    private string? _title;
    private string? _description;

    public Card(string? title = null, string? description = null)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        BackColor = Theme.Colors.Surface;
        ForeColor = Theme.Colors.Text;
        Font = Theme.Fonts.Body;
        _title = title;
        _description = description;
        Margin = this.Px(0, 0, 0, Theme.Space.M);
        UpdatePadding();
    }

    public string? Title
    {
        get => _title;
        set
        {
            _title = value;
            UpdatePadding();
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            UpdatePadding();
        }
    }

    /// <summary>Space reserved for the shadow under the card.</summary>
    protected int ShadowDepth => this.Px(3);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.EffectiveBackColor(Parent));
        Draw.HighQuality(g);
        var radius = this.Px(Theme.Radius.Card);
        var body = new RectangleF(1, 0, Width - 2, Height - ShadowDepth - 1);
        Draw.Shadow(g, body, radius, ShadowDepth);
        Draw.FillRounded(g, BackColor, body, radius);
        Draw.StrokeRounded(g, Theme.Colors.Border, 1f, body, radius);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_title == null)
        {
            return;
        }

        var pad = this.Px(Theme.Space.L);
        var g = e.Graphics;
        var titleHeight = TextRenderer.MeasureText(_title, Theme.Fonts.CardTitle).Height;
        TextRenderer.DrawText(g, _title, Theme.Fonts.CardTitle, new Rectangle(pad, pad, Width - pad * 2, titleHeight),
            Theme.Colors.Text, Draw.SingleLine | TextFormatFlags.NoPadding);
        if (_description != null)
        {
            var top = pad + titleHeight + this.Px(2);
            TextRenderer.DrawText(g, _description, Theme.Fonts.Small, new Rectangle(pad, top, Width - pad * 2, DescriptionHeight()),
                Theme.Colors.TextMuted, Draw.Wrapped | TextFormatFlags.NoPadding);
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (_description != null)
        {
            UpdatePadding();
        }
    }

    private int DescriptionHeight()
    {
        if (_description == null)
        {
            return 0;
        }

        var width = Math.Max(1, Width - this.Px(Theme.Space.L) * 2);
        return TextRenderer.MeasureText(_description, Theme.Fonts.Small, new Size(width, 0), Draw.Wrapped | TextFormatFlags.NoPadding).Height;
    }

    private void UpdatePadding()
    {
        var pad = this.Px(Theme.Space.L);
        var top = pad;
        if (_title != null)
        {
            top += TextRenderer.MeasureText(_title, Theme.Fonts.CardTitle).Height + this.Px(Theme.Space.M);
            if (_description != null)
            {
                top += DescriptionHeight() + this.Px(2);
            }
        }

        var padding = new Padding(pad, top, pad, pad + ShadowDepth);
        if (Padding != padding)
        {
            Padding = padding;
        }

        Invalidate();
    }
}
