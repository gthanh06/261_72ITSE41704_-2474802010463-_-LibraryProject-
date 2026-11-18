using System.Runtime.CompilerServices;
using System.Windows.Forms.Automation;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Short notifications in the bottom-right corner of a window ("Reservation cancelled"). They slide in,
/// disappear on their own (later for warnings and errors), stay while the mouse is over them and can be
/// closed with a click. Screen readers announce them.
/// </summary>
internal static class Toasts
{
    private static readonly ConditionalWeakTable<Form, ToastStack> Stacks = new();

    public static void Show(Control anchor, Tone tone, string title, string? message = null)
    {
        var form = anchor as Form ?? anchor.FindForm();
        if (form == null || form.IsDisposed)
        {
            return;
        }

        Stacks.GetValue(form, f => new ToastStack(f)).Push(new Toast(tone, title, message));
    }

    private sealed class ToastStack
    {
        private const int MaxVisible = 3;
        private readonly Form _form;
        private readonly List<Toast> _toasts = new();

        public ToastStack(Form form)
        {
            _form = form;
            _form.Resize += (_, _) => Arrange(animate: false);
        }

        public void Push(Toast toast)
        {
            while (_toasts.Count >= MaxVisible)
            {
                Remove(_toasts[0]);
            }

            toast.Dismissed += (_, _) => Remove(toast);
            _toasts.Add(toast);
            _form.Controls.Add(toast);
            toast.BringToFront();
            Arrange(animate: true);
            toast.Announce();
        }

        private void Remove(Toast toast)
        {
            if (_toasts.Remove(toast))
            {
                _form.Controls.Remove(toast);
                toast.Dispose();
                Arrange(animate: false);
            }
        }

        private void Arrange(bool animate)
        {
            var margin = _form.Px(Theme.Space.L);
            var bottom = _form.ClientSize.Height - margin;
            for (var i = _toasts.Count - 1; i >= 0; i--)
            {
                var toast = _toasts[i];
                var target = new Point(_form.ClientSize.Width - toast.Width - margin, bottom - toast.Height);
                bottom = target.Y - _form.Px(Theme.Space.S);
                if (animate && i == _toasts.Count - 1)
                {
                    toast.SlideIn(target);
                }
                else
                {
                    toast.Location = target;
                }
            }
        }
    }
}

internal sealed class Toast : Control
{
    private readonly Tone _tone;
    private readonly string _title;
    private readonly string? _message;
    private readonly System.Windows.Forms.Timer _life;
    private Tween? _slide;
    private bool _closing;

    public Toast(Tone tone, string title, string? message)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        _tone = tone;
        _title = title;
        _message = message;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.Alert;
        AccessibleName = message == null ? title : $"{title}. {message}";
        Width = this.Px(380);
        Height = MeasureHeight();

        var longLived = tone is Tone.Warning or Tone.Danger;
        _life = new System.Windows.Forms.Timer { Interval = longLived ? Theme.Motion.ToastLongMs : Theme.Motion.ToastMs };
        _life.Tick += (_, _) => Close();
        _life.Start();
    }

    public event EventHandler? Dismissed;

    public void SlideIn(Point target)
    {
        var startX = target.X + this.Px(40);
        Location = new Point(startX, target.Y);
        _slide?.Stop();
        _slide = Tween.Run(Theme.Motion.NormalMs, p => Left = (int)(startX + (target.X - startX) * p), ease: Easing.OutQuint);
    }

    public void Announce()
    {
        if (IsHandleCreated)
        {
            AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.Other,
                AutomationNotificationProcessing.ImportantMostRecent, AccessibleName ?? _title);
        }
    }

    public void Close()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _life.Stop();
        _slide?.Stop();
        var startX = Left;
        _slide = Tween.Run(Theme.Motion.FastMs, p => Left = startX + (int)(this.Px(60) * p),
            () => Dismissed?.Invoke(this, EventArgs.Empty), Easing.InOutCubic);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _life.Stop();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_closing)
        {
            _life.Start();
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.EffectiveBackColor(Parent));
        Draw.HighQuality(g);
        var radius = this.Px(Theme.Radius.Card);
        var shadow = this.Px(4);
        var body = new RectangleF(1, 0, Width - 2, Height - shadow - 1);
        Draw.Shadow(g, body, radius, shadow);
        Draw.FillRounded(g, Theme.Colors.Surface, body, radius);
        Draw.StrokeRounded(g, Theme.Colors.Border, 1f, body, radius);

        // Tone stripe on the left edge, clipped to the card's rounded shape.
        using (var clip = Draw.RoundedRect(body, radius))
        {
            var state = g.Save();
            g.SetClip(clip);
            using var brush = new SolidBrush(Tones.Strong(_tone));
            g.FillRectangle(brush, body.X, body.Y, this.Px(4), body.Height);
            g.Restore(state);
        }

        var pad = this.Px(Theme.Space.M);
        var circle = this.Px(28);
        var circleRect = new Rectangle(pad + this.Px(2), pad, circle, circle);
        Draw.FillRounded(g, Tones.Soft(_tone), circleRect, circle / 2f);
        Draw.Glyph(g, Tones.Glyph(_tone), 10.5f, Tones.Strong(_tone), circleRect);

        var closeSize = this.Px(16);
        Draw.Glyph(g, Icons.Close, 8f, Theme.Colors.TextMuted, new Rectangle(Width - pad - closeSize, pad, closeSize, closeSize));

        var textLeft = circleRect.Right + this.Px(Theme.Space.S);
        var textWidth = Width - textLeft - pad - closeSize - this.Px(Theme.Space.Xs);
        var titleHeight = TextRenderer.MeasureText(_title, Theme.Fonts.BodySemibold).Height;
        TextRenderer.DrawText(g, _title, Theme.Fonts.BodySemibold, new Rectangle(textLeft, pad + this.Px(4), textWidth, titleHeight),
            Theme.Colors.Text, Draw.SingleLine);
        if (_message != null)
        {
            TextRenderer.DrawText(g, _message, Theme.Fonts.Small,
                new Rectangle(textLeft, pad + this.Px(4) + titleHeight, textWidth, Height - pad - titleHeight),
                Theme.Colors.TextMuted, Draw.Wrapped);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _life.Dispose();
            _slide?.Dispose();
        }

        base.Dispose(disposing);
    }

    private int MeasureHeight()
    {
        var pad = this.Px(Theme.Space.M);
        var textWidth = Width - pad * 2 - this.Px(28 + Theme.Space.S + 16 + Theme.Space.Xs + 2);
        var height = TextRenderer.MeasureText(_title, Theme.Fonts.BodySemibold).Height + this.Px(4);
        if (_message != null)
        {
            height += TextRenderer.MeasureText(_message, Theme.Fonts.Small, new Size(Math.Max(1, textWidth), 0), Draw.Wrapped).Height;
        }

        return pad * 2 + Math.Max(this.Px(28), height) + this.Px(4);
    }
}
