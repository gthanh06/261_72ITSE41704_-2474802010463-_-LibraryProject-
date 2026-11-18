using System.Diagnostics.CodeAnalysis;

namespace ELibrary.WinForms.UI;

/// <summary>
/// A labelled text input: caption above (with * when required), a rounded box with placeholder text,
/// and a help or error line right under the box. Setting <see cref="Error"/> turns the border red and
/// shows the message with an icon; typing clears it again.
/// </summary>
internal sealed class FormField : Panel
{
    private readonly InputBox _box;
    private string _caption;
    private bool _required;
    private string? _help;
    private string? _error;

    public FormField(string caption, bool required = false, string? placeholder = null, bool password = false,
        string? help = null, int width = 320)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        _caption = caption;
        _required = required;
        _help = help;
        Font = Theme.Fonts.Body;
        Input = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = Theme.Fonts.Body,
            ForeColor = Theme.Colors.Text,
            BackColor = Theme.Colors.Surface,
            PlaceholderText = placeholder ?? string.Empty,
            UseSystemPasswordChar = password,
            AccessibleName = caption,
            AccessibleDescription = help
        };
        Input.TextChanged += (_, _) =>
        {
            if (ClearErrorOnEdit && _error != null)
            {
                Error = null;
            }
        };

        _box = new InputBox(Input, password);
        Controls.Add(_box);
        Width = this.Px(width);
        Margin = this.Px(0, 0, Theme.Space.M, Theme.Space.Xs);
        UpdateHeight();
    }

    /// <summary>The inner text box (for events such as KeyDown or for setting MaxLength).</summary>
    public TextBox Input { get; }

    [AllowNull]
    public override string Text
    {
        get => Input.Text;
        set => Input.Text = value;
    }

    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value;
            Input.AccessibleName = value;
            Invalidate();
        }
    }

    public bool Required
    {
        get => _required;
        set
        {
            _required = value;
            Invalidate();
        }
    }

    public string? HelpText
    {
        get => _help;
        set
        {
            _help = value;
            UpdateMessage();
        }
    }

    /// <summary>The validation message shown under the box, or null when the value is valid.</summary>
    public string? Error
    {
        get => _error;
        set
        {
            _error = string.IsNullOrWhiteSpace(value) ? null : value;
            _box.Invalid = _error != null;
            UpdateMessage();
        }
    }

    public bool ClearErrorOnEdit { get; set; } = true;

    /// <summary>Moves the keyboard focus into the text box and selects its text.</summary>
    public void FocusInput()
    {
        Input.Focus();
        Input.SelectAll();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _box.Invalidate();
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        // The box includes room for the focus halo, so the caption and message are inset by the same amount.
        _box.Bounds = new Rectangle(0, CaptionHeight + this.Px(6) - Halo, Width, BoxHeight + Halo * 2);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateHeight();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // Caption + required marker
        var captionColor = Enabled ? Theme.Colors.Text : Theme.Colors.TextDisabled;
        var captionRect = new Rectangle(Halo, 0, Width - Halo, CaptionHeight);
        TextRenderer.DrawText(g, _caption, Theme.Fonts.SmallSemibold, captionRect, captionColor, Draw.SingleLine | TextFormatFlags.NoPadding);
        if (_required)
        {
            var w = Draw.TextWidth(_caption, Theme.Fonts.SmallSemibold, noPadding: true);
            TextRenderer.DrawText(g, " *", Theme.Fonts.SmallSemibold, new Rectangle(Halo + w, 0, this.Px(20), CaptionHeight),
                Theme.Colors.Danger, Draw.SingleLine | TextFormatFlags.NoPadding);
        }

        // Help or error line
        var message = _error ?? _help;
        if (message == null)
        {
            return;
        }

        var top = MessageTop;
        var x = Halo;
        var color = _error != null ? Theme.Colors.Danger : Theme.Colors.TextMuted;
        if (_error != null)
        {
            var iconSize = this.Px(14);
            Draw.Glyph(g, Icons.Error, 9f, color, new Rectangle(x, top + this.Px(1), iconSize, iconSize));
            x += iconSize + this.Px(6);
        }

        TextRenderer.DrawText(g, message, Theme.Fonts.Small, new Rectangle(x, top, Width - x, Height - top), color, Draw.Wrapped);
    }

    private int Halo => this.Px(3);

    private int CaptionHeight => this.Px(20);

    private int BoxHeight => this.Px(Theme.Size.ControlHeight);

    private int MessageTop => CaptionHeight + this.Px(6) + BoxHeight + this.Px(5);

    private void UpdateMessage()
    {
        Input.AccessibleDescription = _error ?? _help;
        UpdateHeight();
        Invalidate();
    }

    private void UpdateHeight()
    {
        var message = _error ?? _help;
        var lineHeight = this.Px(18);
        var messageHeight = lineHeight;
        if (message != null && Width > 0)
        {
            var textWidth = Math.Max(1, Width - Halo - (_error != null ? this.Px(20) : 0));
            var measured = TextRenderer.MeasureText(message, Theme.Fonts.Small, new Size(textWidth, 0), Draw.Wrapped).Height;
            messageHeight = Math.Max(lineHeight, measured);
        }

        var height = MessageTop + messageHeight;
        if (Height != height)
        {
            Height = height;
        }
    }

    /// <summary>The rounded box around the text box; it also draws the focus halo.</summary>
    private sealed class InputBox : Control
    {
        private readonly TextBox _input;
        private readonly ThemedButton? _reveal;
        private bool _hover;
        private bool _invalid;

        public InputBox(TextBox input, bool password)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            _input = input;
            Cursor = Cursors.IBeam;
            Controls.Add(input);
            input.Enter += (_, _) => Invalidate();
            input.Leave += (_, _) => Invalidate();
            input.MouseEnter += (_, _) => SetHover(true);
            input.MouseLeave += (_, _) => SetHover(ClientRectangle.Contains(PointToClient(MousePosition)));

            if (password)
            {
                _reveal = new ThemedButton(string.Empty, ButtonVariant.Ghost, Icons.Show)
                {
                    Compact = true,
                    AccessibleName = "Show password",
                    Margin = Padding.Empty,
                    Cursor = Cursors.Hand
                };
                _reveal.Click += (_, _) => ToggleReveal();
                Controls.Add(_reveal);
            }
        }

        public bool Invalid
        {
            get => _invalid;
            set
            {
                _invalid = value;
                Invalidate();
            }
        }

        private int Halo => this.Px(3);

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var inner = Rectangle.Inflate(ClientRectangle, -Halo, -Halo);
            var padding = this.Px(10);
            var right = inner.Right - padding;
            if (_reveal != null)
            {
                var size = this.Px(Theme.Size.CompactControlHeight);
                _reveal.Bounds = new Rectangle(inner.Right - size - this.Px(3), inner.Top + (inner.Height - size) / 2, size, size);
                right = _reveal.Left - this.Px(4);
            }

            _input.Bounds = new Rectangle(inner.Left + padding, inner.Top + (inner.Height - _input.Height) / 2,
                Math.Max(10, right - inner.Left - padding), _input.Height);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            SetHover(true);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHover(false);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _input.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Draw.EffectiveBackColor(Parent));
            Draw.HighQuality(g);

            var radius = this.Px(Theme.Radius.Control);
            var inner = new RectangleF(Halo, Halo, Width - Halo * 2, Height - Halo * 2);
            var focused = _input.Focused;
            var enabled = Enabled && _input.Enabled;

            if (focused)
            {
                var halo = _invalid ? Theme.Colors.Danger : Theme.Colors.Primary;
                Draw.FillRounded(g, Color.FromArgb(46, halo), new RectangleF(0, 0, Width, Height), radius + Halo);
            }

            var fill = enabled ? Theme.Colors.Surface : Theme.Colors.SurfaceSunken;
            Draw.FillRounded(g, fill, inner, radius);
            _input.BackColor = fill;
            _input.ForeColor = enabled ? Theme.Colors.Text : Theme.Colors.TextDisabled;

            var border = !enabled ? Theme.Colors.Border
                : _invalid ? Theme.Colors.Danger
                : focused ? Theme.Colors.Primary
                : _hover ? Theme.Colors.InputBorderHover
                : Theme.Colors.InputBorder;
            Draw.StrokeRounded(g, border, focused || _invalid ? this.Px(2) * 0.75f : 1f, inner, radius);
        }

        private void SetHover(bool hover)
        {
            if (_hover != hover)
            {
                _hover = hover;
                Invalidate();
            }
        }

        private void ToggleReveal()
        {
            var hidden = _input.UseSystemPasswordChar;
            _input.UseSystemPasswordChar = !hidden;
            _reveal!.Glyph = hidden ? Icons.Hide : Icons.Show;
            _reveal.AccessibleName = hidden ? "Hide password" : "Show password";
            _input.Focus();
        }
    }
}
