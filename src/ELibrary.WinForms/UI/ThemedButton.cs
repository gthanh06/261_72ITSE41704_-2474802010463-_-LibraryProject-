namespace ELibrary.WinForms.UI;

internal enum ButtonVariant
{
    /// <summary>The main action of a screen or dialog (one per area).</summary>
    Primary,

    /// <summary>Other actions.</summary>
    Secondary,

    /// <summary>Low-emphasis actions (clear filters, show password, icon-only tools).</summary>
    Ghost,

    /// <summary>Destructive actions (cancel a reservation, lock an account).</summary>
    Danger
}

/// <summary>
/// A themed button. It derives from <see cref="Button"/>, so Click, AcceptButton/CancelButton,
/// DialogResult, keyboard activation and accessibility work exactly as for a normal button.
/// States: hover (animated), pressed, keyboard focus ring, disabled and loading (spinner; clicks ignored).
/// </summary>
internal class ThemedButton : Button
{
    private ButtonVariant _variant;
    private string? _glyph;
    private bool _hover;
    private bool _pressed;
    private bool _loading;
    private float _hoverProgress;
    private Tween? _hoverTween;
    private System.Windows.Forms.Timer? _spinner;
    private int _spinnerAngle;
    private bool _compact;
    private Point _rippleCenter;
    private float _rippleGrow;
    private float _rippleFade;
    private bool _rippleHeld;
    private Tween? _rippleGrowTween;
    private Tween? _rippleFadeTween;

    public ThemedButton() : this(string.Empty)
    {
    }

    public ThemedButton(string text, ButtonVariant variant = ButtonVariant.Secondary, string? glyph = null)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Text = text;
        _variant = variant;
        _glyph = glyph;
        Font = Theme.Fonts.BodySemibold;
        Cursor = Cursors.Hand;
        Margin = this.PxAll(Theme.Space.Xxs);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    public ButtonVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            Invalidate();
        }
    }

    /// <summary>Optional icon drawn before the text (see <see cref="Icons"/>).</summary>
    public string? Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            PerformAutoSize();
        }
    }

    /// <summary>A smaller height for toolbars and table actions.</summary>
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            PerformAutoSize();
        }
    }

    /// <summary>Shows a spinner and ignores clicks while an operation runs.</summary>
    public bool IsLoading
    {
        get => _loading;
        set
        {
            if (_loading == value)
            {
                return;
            }

            _loading = value;
            Cursor = value ? Cursors.WaitCursor : Cursors.Hand;
            AccessibleDescription = value ? "Working…" : null;
            if (value)
            {
                _spinner ??= CreateSpinnerTimer();
                _spinner.Start();
            }
            else
            {
                _spinner?.Stop();
            }

            PerformAutoSize();
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = this.Px(_compact ? Theme.Size.CompactControlHeight : Theme.Size.ControlHeight);
        var iconOnly = string.IsNullOrEmpty(Text);
        if (iconOnly)
        {
            return AtLeastMinimum(new Size(height, height));
        }

        var textWidth = Draw.TextWidth(Text, Font);
        var padding = this.Px(_compact ? Theme.Space.S : Theme.Space.M);
        var glyphWidth = _glyph != null || _loading ? this.Px(16 + Theme.Space.Xs) : 0;
        return AtLeastMinimum(new Size(padding * 2 + glyphWidth + textWidth, height));
    }

    // Layout panels place controls by their preferred size, so it must already include MinimumSize;
    // otherwise neighbouring buttons overlap when the minimum is wider than the text.
    private Size AtLeastMinimum(Size size) =>
        new(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));

    protected override void OnClick(EventArgs e)
    {
        if (!_loading)
        {
            // Keyboard (Enter/Space) and PerformClick get a ripple from the centre; a mouse click already has one.
            if (!_rippleHeld)
            {
                StartRipple(new Point(Width / 2, Height / 2));
            }

            // Let the ripple fade even if the click opens a modal dialog.
            ReleaseRipple();
            base.OnClick(e);
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        PerformAutoSize();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHover(true);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _pressed = false;
        ReleaseRipple();
        SetHover(false);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            StartRipple(mevent.Location);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _pressed = false;
        ReleaseRipple();
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(Draw.EffectiveBackColor(Parent));
        Draw.HighQuality(g);

        var bounds = new RectangleF(0, 0, Width, Height);
        var radius = this.Px(Theme.Radius.Control);
        var (fill, border, fore) = CurrentColors();
        if (fill.A > 0)
        {
            Draw.FillRounded(g, fill, bounds, radius);
        }

        PaintRipple(g, bounds, radius);

        if (border.A > 0)
        {
            Draw.StrokeRounded(g, border, 1f, bounds, radius);
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            Draw.StrokeRounded(g, Theme.Colors.FocusRing, this.Px(Theme.Size.FocusRingWidth), bounds, radius);
        }

        PaintContent(g, fore);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hoverTween?.Dispose();
            _rippleGrowTween?.Dispose();
            _rippleFadeTween?.Dispose();
            _spinner?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PaintContent(Graphics g, Color fore)
    {
        var iconSize = this.Px(16);
        var gap = this.Px(Theme.Space.Xs);
        var hasText = !string.IsNullOrEmpty(Text);
        var leading = _loading ? "spinner" : _glyph;
        var textWidth = hasText ? Draw.TextWidth(Text, Font) : 0;
        var contentWidth = textWidth + (leading != null ? iconSize + (hasText ? gap : 0) : 0);
        var x = Math.Max(0, (Width - contentWidth) / 2);
        var iconRect = new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize);

        if (_loading)
        {
            PaintSpinner(g, iconRect, fore);
        }
        else if (_glyph != null)
        {
            Draw.Glyph(g, _glyph, 10.5f, fore, iconRect);
        }

        if (hasText)
        {
            var textX = leading != null ? iconRect.Right + gap : x;
            var textRect = new Rectangle(textX, 0, Width - textX, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, fore, Draw.SingleLine | TextFormatFlags.Left);
        }
    }

    /// <summary>
    /// Material-style ink: a circle that spreads from the pointer while the button is held and fades out
    /// after the click. Off when animations are off; the pressed colour still shows the press.
    /// </summary>
    private void StartRipple(Point center)
    {
        if (!Theme.Motion.Enabled || !Enabled || _loading)
        {
            return;
        }

        _rippleCenter = center;
        _rippleHeld = true;
        _rippleGrowTween?.Stop();
        _rippleFadeTween?.Stop();
        _rippleFade = 1f;
        _rippleGrowTween = Tween.Run(450, p =>
        {
            _rippleGrow = p;
            Invalidate();
        }, ease: Easing.OutQuint);
    }

    private void ReleaseRipple()
    {
        if (!_rippleHeld)
        {
            return;
        }

        _rippleHeld = false;
        var from = _rippleFade;
        _rippleFadeTween?.Stop();
        _rippleFadeTween = Tween.Run(400, p =>
        {
            _rippleFade = from * (1f - p);
            Invalidate();
        });
    }

    private void PaintRipple(Graphics g, RectangleF bounds, float radius)
    {
        if (_rippleFade <= 0f || !Enabled)
        {
            return;
        }

        // Reach the farthest corner so the ink covers the whole button.
        var dx = Math.Max(_rippleCenter.X, Width - _rippleCenter.X);
        var dy = Math.Max(_rippleCenter.Y, Height - _rippleCenter.Y);
        var reach = MathF.Sqrt(dx * dx + dy * dy);
        var r = reach * (0.12f + 0.88f * _rippleGrow);
        var onDark = _variant is ButtonVariant.Primary or ButtonVariant.Danger;
        var ink = onDark ? Color.FromArgb((int)(64 * _rippleFade), Color.White)
            : Color.FromArgb((int)(40 * _rippleFade), Theme.Colors.Primary);

        using var clip = Draw.RoundedRect(bounds, radius);
        using var brush = new SolidBrush(ink);
        var state = g.Save();
        g.SetClip(clip);
        g.FillEllipse(brush, _rippleCenter.X - r, _rippleCenter.Y - r, r * 2, r * 2);
        g.Restore(state);
    }

    private void PaintSpinner(Graphics g, Rectangle r, Color color)
    {
        var inset = this.Px(2);
        var rect = Rectangle.Inflate(r, -inset, -inset);
        using var track = new Pen(Color.FromArgb(60, color), this.Px(2));
        using var arc = new Pen(color, this.Px(2)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        g.DrawEllipse(track, rect);
        g.DrawArc(arc, rect, _spinnerAngle, 100);
    }

    private (Color Fill, Color Border, Color Fore) CurrentColors()
    {
        var c = Theme.Colors.Surface;
        if (!Enabled)
        {
            return _variant == ButtonVariant.Ghost
                ? (Color.Transparent, Color.Transparent, Theme.Colors.TextDisabled)
                : (Theme.Colors.SurfaceSunken, Color.Transparent, Theme.Colors.TextDisabled);
        }

        var t = _loading ? 1f : _hoverProgress;
        return _variant switch
        {
            ButtonVariant.Primary => (_pressed ? Theme.Colors.PrimaryPressed : Draw.Blend(Theme.Colors.Primary, Theme.Colors.PrimaryHover, t),
                Color.Transparent, Theme.Colors.OnPrimary),
            ButtonVariant.Danger => (_pressed ? Theme.Colors.DangerHover : Draw.Blend(Theme.Colors.Danger, Theme.Colors.DangerHover, t),
                Color.Transparent, Theme.Colors.OnPrimary),
            ButtonVariant.Ghost => (_pressed ? Theme.Colors.PrimarySoft : Draw.Blend(Color.FromArgb(0, Theme.Colors.PrimarySoft), Theme.Colors.PrimarySoft, t),
                Color.Transparent, Theme.Colors.Primary),
            _ => (_pressed ? Theme.Colors.Border : Draw.Blend(c, Theme.Colors.SurfaceSunken, t),
                Draw.Blend(Theme.Colors.Border, Theme.Colors.BorderStrong, t), Theme.Colors.Text)
        };
    }

    private void SetHover(bool hover)
    {
        if (_hover == hover)
        {
            return;
        }

        _hover = hover;
        var from = _hoverProgress;
        var to = hover ? 1f : 0f;
        _hoverTween?.Stop();
        _hoverTween = Tween.Run(Theme.Motion.FastMs, p =>
        {
            _hoverProgress = from + (to - from) * p;
            Invalidate();
        });
    }

    private System.Windows.Forms.Timer CreateSpinnerTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            _spinnerAngle = (_spinnerAngle + (Theme.Motion.Enabled ? 12 : 4)) % 360;
            Invalidate();
        };
        return timer;
    }

    private void PerformAutoSize()
    {
        if (AutoSize)
        {
            Size = GetPreferredSize(Size.Empty);
        }

        Invalidate();
    }
}
