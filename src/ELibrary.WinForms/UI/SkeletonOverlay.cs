using System.Drawing.Drawing2D;

namespace ELibrary.WinForms.UI;

/// <summary>
/// A loading placeholder laid over a control while its data loads: grey bars in the shape of table rows
/// with a soft highlight sweeping across them. Over a <see cref="DataGridView"/> the bars follow its
/// columns. Without motion the bars stay still. It only covers the control; nothing underneath changes.
/// </summary>
internal sealed class SkeletonOverlay : Control
{
    private const double SweepMs = 1400;
    private static readonly float[] DefaultColumns = { 0.34f, 0.22f, 0.16f, 0.14f };

    private readonly Control _target;
    private readonly Action<double> _onFrame;
    private System.Windows.Forms.Timer? _delay;
    private bool _attached;
    private bool _hidden;
    private float _phase;

    private SkeletonOverlay(Control target)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AccessibleRole = AccessibleRole.Animation;
        AccessibleName = "Loading";
        Cursor = Cursors.AppStarting;
        _target = target;
        _onFrame = OnFrame;
    }

    /// <summary>
    /// Covers <paramref name="target"/> after <paramref name="delayMs"/> (0 = at once). A short delay keeps
    /// fast reloads from flashing a placeholder. Call <see cref="Hide"/> when the data is in.
    /// </summary>
    public static SkeletonOverlay Show(Control target, int delayMs = 0)
    {
        var overlay = new SkeletonOverlay(target);
        if (delayMs <= 0)
        {
            overlay.Attach();
        }
        else
        {
            overlay._delay = new System.Windows.Forms.Timer { Interval = delayMs };
            overlay._delay.Tick += (_, _) =>
            {
                overlay._delay?.Stop();
                overlay.Attach();
            };
            overlay._delay.Start();
        }

        return overlay;
    }

    /// <summary>Removes the placeholder (safe to call more than once).</summary>
    public new void Hide()
    {
        if (_hidden)
        {
            return;
        }

        _hidden = true;
        _delay?.Dispose();
        _delay = null;
        if (_attached)
        {
            AnimationClock.Unsubscribe(_onFrame);
            _target.Resize -= FollowTarget;
            _target.Move -= FollowTarget;
            _target.VisibleChanged -= FollowTarget;
            Parent?.Controls.Remove(this);
        }

        Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Colors.Surface);

        var grid = _target as DataGridView;
        var top = 0;
        if (grid is { ColumnHeadersVisible: true })
        {
            top = grid.ColumnHeadersHeight;
            using var header = new SolidBrush(Theme.Colors.SurfaceSunken);
            g.FillRectangle(header, 0, 0, Width, top);
        }

        var rowHeight = grid?.RowTemplate.Height ?? this.Px(Theme.Size.GridRowHeight);
        var columns = ColumnSpans(grid);
        var barHeight = this.Px(10);
        var radius = barHeight / 2f;

        Draw.HighQuality(g);
        using var bars = new GraphicsPath();
        using (var line = new Pen(Theme.Colors.Border))
        {
            var row = 0;
            for (var y = top; y < Height; y += rowHeight, row++)
            {
                var barY = y + (rowHeight - barHeight) / 2;
                for (var c = 0; c < columns.Count; c++)
                {
                    var (left, width) = columns[c];
                    // Vary the lengths a little, the same way on every repaint.
                    var fill = 0.45f + 0.4f * Noise(row, c);
                    var barWidth = Math.Max(barHeight, (int)(width * fill));
                    using var bar = Draw.RoundedRect(new RectangleF(left, barY, barWidth, barHeight), radius);
                    bars.AddPath(bar, false);
                }

                g.DrawLine(line, 0, y + rowHeight - 1, Width, y + rowHeight - 1);
            }
        }

        using (var brush = new SolidBrush(Draw.Blend(Theme.Colors.SurfaceSunken, Theme.Colors.Border, 0.6f)))
        {
            g.FillPath(brush, bars);
        }

        if (Theme.Motion.Enabled)
        {
            PaintSweep(g, bars);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _delay?.Dispose();
            AnimationClock.Unsubscribe(_onFrame);
        }

        base.Dispose(disposing);
    }

    private void Attach()
    {
        var parent = _target.Parent;
        if (_hidden || _attached || parent == null || _target.IsDisposed)
        {
            return;
        }

        _attached = true;
        parent.Controls.Add(this);
        FollowTarget(this, EventArgs.Empty);
        BringToFront();
        _target.Resize += FollowTarget;
        _target.Move += FollowTarget;
        _target.VisibleChanged += FollowTarget;
        if (Theme.Motion.Enabled)
        {
            AnimationClock.Subscribe(_onFrame);
        }
    }

    private void FollowTarget(object? sender, EventArgs e)
    {
        Bounds = _target.Bounds;
        Visible = _target.Visible;
    }

    private void OnFrame(double nowMs)
    {
        _phase = (float)(nowMs % SweepMs / SweepMs);
        Invalidate();
    }

    private void PaintSweep(Graphics g, GraphicsPath bars)
    {
        var band = this.Px(220);
        var x = -band + (Width + band * 2) * _phase;
        var rect = new RectangleF(x, 0, band, Math.Max(1, Height));
        var shine = Color.White; // a soft highlight on light bars, a faint one on dark bars
        using var brush = new LinearGradientBrush(rect, Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.FromArgb(0, shine), Color.FromArgb(Theme.Colors.IsDark ? 50 : 170, shine), Color.FromArgb(0, shine) },
                Positions = new[] { 0f, 0.5f, 1f }
            }
        };
        var state = g.Save();
        g.SetClip(rect);
        g.FillPath(brush, bars);
        g.Restore(state);
    }

    /// <summary>Left edge and width of each column's bar area (the grid's own columns when it has some).</summary>
    private List<(int Left, int Width)> ColumnSpans(DataGridView? grid)
    {
        var pad = this.Px(Theme.Space.S);
        var spans = new List<(int, int)>();
        if (grid != null && grid.Columns.Count > 0)
        {
            var x = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible)
                {
                    continue;
                }

                if (x >= Width)
                {
                    break;
                }

                spans.Add((x + pad, Math.Max(0, column.Width - pad * 2)));
                x += column.Width;
            }

            return spans;
        }

        var left = pad;
        foreach (var share in DefaultColumns)
        {
            var width = (int)(Width * share);
            spans.Add((left, width - pad * 2));
            left += width;
        }

        return spans;
    }

    private static float Noise(int row, int column)
    {
        unchecked
        {
            var h = (uint)(row * 374761393 + column * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
