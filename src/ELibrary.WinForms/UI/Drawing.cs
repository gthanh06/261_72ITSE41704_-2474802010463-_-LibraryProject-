using System.Drawing.Drawing2D;

namespace ELibrary.WinForms.UI;

/// <summary>Small GDI+ helpers shared by the custom-painted controls.</summary>
internal static class Draw
{
    public const TextFormatFlags SingleLine =
        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

    public const TextFormatFlags Wrapped =
        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;

    /// <summary>Flags for measuring one line. (Measuring with EndEllipsis and no width returns the width of "…".)</summary>
    public const TextFormatFlags MeasureLine = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    public static int TextWidth(string text, Font font, bool noPadding = false) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
            MeasureLine | (noPadding ? TextFormatFlags.NoPadding : TextFormatFlags.Default)).Width;

    public static void HighQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRounded(Graphics g, Color color, float width, RectangleF r, float radius)
    {
        // Stroke inside the rectangle so that the line is never clipped by the control's bounds.
        var inset = width / 2f;
        var inner = RectangleF.Inflate(r, -inset, -inset);
        using var path = RoundedRect(inner, Math.Max(0, radius - inset));
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    /// <summary>A soft drop shadow below a rounded rectangle (a few translucent layers).</summary>
    public static void Shadow(Graphics g, RectangleF r, float radius, int depth)
    {
        for (var i = depth; i >= 1; i--)
        {
            var alpha = (int)(14f / i);
            var layer = new RectangleF(r.X - i * 0.5f, r.Y + i, r.Width + i, r.Height);
            FillRounded(g, Color.FromArgb(alpha, Theme.Colors.Shadow), layer, radius + i * 0.5f);
        }
    }

    public static Color Blend(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(from.A + (to.A - from.A) * t),
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));
    }

    /// <summary>Draws an icon glyph centred in <paramref name="bounds"/>.</summary>
    public static void Glyph(Graphics g, string glyph, float points, Color color, Rectangle bounds) =>
        TextRenderer.DrawText(g, glyph, Theme.Fonts.Icon(points), bounds, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

    /// <summary>The first non-transparent background colour up the parent chain.</summary>
    public static Color EffectiveBackColor(Control? control)
    {
        for (var c = control; c != null; c = c.Parent)
        {
            if (c.BackColor.A == 255)
            {
                return c.BackColor;
            }
        }

        return Theme.Colors.Background;
    }
}

/// <summary>
/// A short time-based animation (ease-out), driven by the shared <see cref="AnimationClock"/> so it runs
/// once per display frame. When animations are switched off the end state is applied at once, so
/// behaviour never depends on motion.
/// </summary>
internal sealed class Tween : IDisposable
{
    private readonly Action<float> _step;
    private readonly Action? _done;
    private readonly int _durationMs;
    private readonly Action<double> _onFrame;
    private readonly Func<float, float> _ease;
    private double _startedMs;
    private bool _running;

    private Tween(int durationMs, Action<float> step, Action? done, Func<float, float>? ease)
    {
        _durationMs = durationMs;
        _ease = ease ?? Easing.OutCubic;
        _step = step;
        _done = done;
        _onFrame = OnFrame;
    }

    public bool IsRunning => _running;

    /// <summary>Runs <paramref name="step"/> with an eased progress from 0 to 1 (see <see cref="Easing"/>; out-cubic by default).</summary>
    public static Tween? Run(int durationMs, Action<float> step, Action? done = null, Func<float, float>? ease = null)
    {
        if (!Theme.Motion.Enabled || durationMs <= 0)
        {
            step(1f);
            done?.Invoke();
            return null;
        }

        var tween = new Tween(durationMs, step, done, ease) { _startedMs = AnimationClock.NowMs };
        step(0f);
        tween._running = true;
        AnimationClock.Subscribe(tween._onFrame);
        return tween;
    }

    public void Stop()
    {
        if (_running)
        {
            _running = false;
            AnimationClock.Unsubscribe(_onFrame);
        }
    }

    public void Dispose() => Stop();

    private void OnFrame(double nowMs)
    {
        if (!_running)
        {
            return;
        }

        var t = (float)Math.Min(1.0, (nowMs - _startedMs) / _durationMs);
        _step(_ease(t));
        if (t >= 1f)
        {
            Stop();
            _done?.Invoke();
        }
    }
}
