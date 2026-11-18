namespace ELibrary.WinForms.UI;

/// <summary>
/// The top of every screen: a small section label (for example "Circulation"), the page title in the
/// heading face, an optional one-line description and an area on the right for page actions.
/// </summary>
internal sealed class PageHeader : Panel
{
    private string? _section;
    private string _title;
    private string? _description;

    public PageHeader(string title)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        _title = title;
        AccessibleRole = AccessibleRole.Grouping;
        AccessibleName = title;
        Actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = Padding.Empty
        };
        Controls.Add(Actions);
        UpdateHeight();
    }

    /// <summary>Buttons shown at the right of the header (added right to left).</summary>
    public FlowLayoutPanel Actions { get; }

    public string? Section
    {
        get => _section;
        set
        {
            _section = value;
            UpdateHeight();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            AccessibleName = value;
            Invalidate();
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            UpdateHeight();
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Actions.Location = new Point(Width - Actions.Width, Math.Max(0, TitleTop + (TitleHeight - Actions.Height) / 2));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var textWidth = Math.Max(1, Width - Actions.Width - this.Px(Theme.Space.M));
        if (_section != null)
        {
            TextRenderer.DrawText(g, _section, Theme.Fonts.SmallSemibold, new Rectangle(0, 0, textWidth, SectionHeight),
                Theme.Colors.TextMuted, Draw.SingleLine | TextFormatFlags.NoPadding);
        }

        TextRenderer.DrawText(g, _title, Theme.Fonts.PageTitle, new Rectangle(0, TitleTop, textWidth, TitleHeight),
            Theme.Colors.Text, Draw.SingleLine | TextFormatFlags.NoPadding);

        if (_description != null)
        {
            TextRenderer.DrawText(g, _description, Theme.Fonts.Body,
                new Rectangle(0, TitleTop + TitleHeight + this.Px(2), textWidth, DescriptionHeight),
                Theme.Colors.TextMuted, Draw.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    private int SectionHeight => _section != null ? TextRenderer.MeasureText("A", Theme.Fonts.SmallSemibold).Height : 0;

    private int TitleTop => _section != null ? SectionHeight + this.Px(4) : 0;

    private int TitleHeight => TextRenderer.MeasureText("Ag", Theme.Fonts.PageTitle).Height;

    private int DescriptionHeight => TextRenderer.MeasureText("Ag", Theme.Fonts.Body).Height;

    private void UpdateHeight()
    {
        var height = TitleTop + TitleHeight + (_description != null ? this.Px(2) + DescriptionHeight : 0) + this.Px(Theme.Space.L);
        if (Height != height)
        {
            Height = height;
        }

        PerformLayout();
        Invalidate();
    }
}
