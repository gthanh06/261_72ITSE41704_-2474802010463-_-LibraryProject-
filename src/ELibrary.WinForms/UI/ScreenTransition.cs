using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ELibrary.WinForms.UI;

/// <summary>
/// "Fade through" between two screens: the old screen fades into the page colour, then the new one fades
/// in while rising a few pixels into place. It is a picture laid over the host for a quarter of a second;
/// the real screens are swapped underneath at once, so behaviour never waits for the animation.
/// Usage: <c>var t = ScreenTransition.Capture(host, oldScreen)</c> → swap screens → <c>t?.Play(newScreen)</c>.
/// </summary>
internal sealed class ScreenTransition : Control
{
    private const int DurationMs = 280;
    private const float FadeOutShare = 0.35f;   // first 35 %: old screen out; rest: new screen in
    private const int RiseLogicalPx = 14;

    private readonly Bitmap _from;
    private Bitmap? _to;
    private Tween? _tween;
    private float _progress;
    private bool _finished;

    private ScreenTransition(Control host, Bitmap from)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.Opaque, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AccessibleRole = AccessibleRole.None;
        _from = from;
        BackColor = Draw.EffectiveBackColor(host);
        Bounds = host.ClientRectangle;
        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
    }

    /// <summary>
    /// Covers <paramref name="host"/> with a picture of <paramref name="from"/>. Returns null (no animation)
    /// when motion is off or there is nothing to fade from.
    /// </summary>
    public static ScreenTransition? Capture(Control host, Control? from)
    {
        if (!Theme.Motion.Enabled || from == null || from.IsDisposed || !from.Visible || !host.IsHandleCreated ||
            host.ClientSize.Width <= 0 || host.ClientSize.Height <= 0)
        {
            return null;
        }

        var bitmap = Snapshot(host, from);
        if (bitmap == null)
        {
            return null;
        }

        var transition = new ScreenTransition(host, bitmap);
        host.Controls.Add(transition);
        transition.BringToFront();
        transition.Update();
        return transition;
    }

    /// <summary>Animates to <paramref name="to"/> (already shown underneath) and removes itself when done.</summary>
    public void Play(Control to)
    {
        if (_finished || IsDisposed || Parent == null)
        {
            return;
        }

        BringToFront();
        _to = Snapshot(Parent, to);
        if (_to == null)
        {
            Finish();
            return;
        }

        _tween = Tween.Run(DurationMs, p =>
        {
            _progress = p;
            Invalidate();
        }, Finish, t => t);
    }

    /// <summary>Ends the transition at once (safe to call more than once).</summary>
    public void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _tween?.Stop();
        Parent?.Controls.Remove(this);
        Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.SmoothingMode = SmoothingMode.None;

        if (_to == null || _progress < FadeOutShare)
        {
            // Phase 1: the old screen fades into the page colour.
            var t = _to == null ? 0f : _progress / FadeOutShare;
            g.DrawImageUnscaled(_from, 0, 0);
            Veil(g, Easing.OutCubic(t));
            return;
        }

        // Phase 2: the new screen fades in and rises into place.
        var q = Easing.OutCubic((_progress - FadeOutShare) / (1f - FadeOutShare));
        var rise = (int)Math.Round(this.Px(RiseLogicalPx) * (1f - q));
        g.Clear(BackColor);
        g.DrawImageUnscaled(_to, 0, rise);
        Veil(g, 1f - q);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tween?.Dispose();
            _from.Dispose();
            _to?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Veil(Graphics g, float amount)
    {
        var alpha = (int)Math.Round(255 * Math.Clamp(amount, 0f, 1f));
        if (alpha <= 0)
        {
            return;
        }

        using var brush = new SolidBrush(Color.FromArgb(alpha, BackColor));
        g.FillRectangle(brush, ClientRectangle);
    }

    /// <summary>A picture of <paramref name="screen"/> as it sits in <paramref name="host"/> (null if it cannot be drawn).</summary>
    private static Bitmap? Snapshot(Control host, Control screen)
    {
        var size = host.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }

        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        try
        {
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Draw.EffectiveBackColor(host));
            }

            var bounds = screen.Bounds;
            screen.DrawToBitmap(bitmap, bounds);
            // Draw at 1:1 regardless of the screen's DPI.
            bitmap.SetResolution(host.DeviceDpi, host.DeviceDpi);
            return bitmap;
        }
        catch (Exception)
        {
            bitmap.Dispose();
            return null;
        }
    }
}
