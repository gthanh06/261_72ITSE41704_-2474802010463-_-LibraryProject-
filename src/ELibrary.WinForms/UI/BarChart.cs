namespace ELibrary.WinForms.UI;

/// <summary>
/// A small horizontal bar chart inside a card: one row per item with its label, a bar scaled to the
/// largest value and the value at the end. Bars grow in one after another when data is set.
/// </summary>
internal sealed class BarChart : Card
{
    private const int StaggerMs = 45;
    private const int GrowMs = 520;

    private IReadOnlyList<(string Label, int Value)> _items = Array.Empty<(string, int)>();
    private string _emptyText = "No data yet.";
    private double _startedMs = -1;
    private Action<double>? _onFrame;
    private float _elapsedMs = float.MaxValue;

    public BarChart(string title, string? description = null) : base(title, description)
    {
        MinimumSize = new Size(0, this.Px(220));
        AccessibleRole = AccessibleRole.Chart;
        AccessibleName = title;
    }

    public Color BarColor { get; set; } = Theme.Colors.Primary;

    public void SetItems(IEnumerable<(string Label, int Value)> items, string? emptyText = null)
    {
        _items = items.ToList();
        _emptyText = emptyText ?? _emptyText;
        AccessibleDescription = string.Join("; ", _items.Select(i => $"{i.Label}: {i.Value:N0}"));
        Height = Math.Max(MinimumSize.Height, Padding.Top + Padding.Bottom + _items.Count * RowHeight + this.Px(Theme.Space.Xs));
        StartGrow();
    }

    private int RowHeight => this.Px(30);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); // title and description
        var g = e.Graphics;
        var area = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
        if (_items.Count == 0)
        {
            TextRenderer.DrawText(g, _emptyText, Theme.Fonts.Body, area, Theme.Colors.TextMuted, Draw.SingleLine);
            return;
        }

        Draw.HighQuality(g);
        var max = Math.Max(1, _items.Max(i => i.Value));
        var labelWidth = Math.Min(area.Width / 3, _items.Max(i => Draw.TextWidth(i.Label, Theme.Fonts.Small)) + this.Px(Theme.Space.S));
        var valueWidth = _items.Max(i => Draw.TextWidth(i.Value.ToString("N0"), Theme.Fonts.SmallSemibold)) + this.Px(Theme.Space.Xs);
        var barLeft = area.Left + labelWidth;
        var barSpace = Math.Max(0, area.Width - labelWidth - valueWidth);
        var barHeight = this.Px(12);
        var track = Theme.Colors.SurfaceSunken;

        for (var i = 0; i < _items.Count; i++)
        {
            var (label, value) = _items[i];
            var y = area.Top + i * RowHeight;
            TextRenderer.DrawText(g, label, Theme.Fonts.Small, new Rectangle(area.Left, y, labelWidth - this.Px(Theme.Space.Xs), RowHeight),
                Theme.Colors.Text, Draw.SingleLine);

            var barY = y + (RowHeight - barHeight) / 2f;
            Draw.FillRounded(g, track, new RectangleF(barLeft, barY, barSpace, barHeight), barHeight / 2f);

            var grow = Easing.OutCubic(Math.Clamp((_elapsedMs - i * StaggerMs) / GrowMs, 0f, 1f));
            var width = barSpace * (value / (float)max) * grow;
            if (width >= 1f)
            {
                Draw.FillRounded(g, BarColor, new RectangleF(barLeft, barY, Math.Max(width, barHeight), barHeight), barHeight / 2f);
            }

            TextRenderer.DrawText(g, value.ToString("N0"), Theme.Fonts.SmallSemibold,
                new Rectangle(barLeft + barSpace + this.Px(Theme.Space.Xs), y, valueWidth, RowHeight), Theme.Colors.TextMuted, Draw.SingleLine);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopGrow();
        }

        base.Dispose(disposing);
    }

    private void StartGrow()
    {
        StopGrow();
        if (!Theme.Motion.Enabled || _items.Count == 0)
        {
            _elapsedMs = float.MaxValue;
            Invalidate();
            return;
        }

        _elapsedMs = 0;
        _startedMs = AnimationClock.NowMs;
        var total = GrowMs + StaggerMs * _items.Count;
        _onFrame = now =>
        {
            _elapsedMs = (float)(now - _startedMs);
            Invalidate();
            if (_elapsedMs >= total)
            {
                StopGrow();
            }
        };
        AnimationClock.Subscribe(_onFrame);
        Invalidate();
    }

    private void StopGrow()
    {
        if (_onFrame != null)
        {
            AnimationClock.Unsubscribe(_onFrame);
            _onFrame = null;
        }
    }
}
