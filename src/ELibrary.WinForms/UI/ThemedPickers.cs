using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// A <see cref="DateTimePicker"/> drawn like the themed text box: a flat 1-pixel border in the theme colours
/// and a calendar icon instead of the classic drop-down button. Value, formats and events are unchanged.
/// </summary>
internal sealed class ThemedDatePicker : DateTimePicker
{
    private bool _hover;

    public ThemedDatePicker()
    {
        // The native control mis-measures Segoe UI Variable ("30/ 11/2026"), so it uses the classic family.
        Font = Theme.Fonts.NativeBody;
        CalendarFont = Theme.Fonts.NativeBody;
        UseControlHeight();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UseControlHeight();
    }

    private void UseControlHeight() => NativeChrome.UseControlHeight(this, h =>
    {
        FontHeight = h;
        // PreferredHeight is cached; clear the cache so the new FontHeight is used (ignored if the field is renamed).
        var cache = typeof(DateTimePicker).GetField("_prefHeightCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        cache?.SetValue(this, Convert.ChangeType(-1, cache.FieldType, System.Globalization.CultureInfo.InvariantCulture));
    });

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is NativeChrome.WmPaint or NativeChrome.WmNcPaint)
        {
            PaintChrome();
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        NativeChrome.Redraw(this);
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        NativeChrome.Redraw(this);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        NativeChrome.Redraw(this);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        NativeChrome.Redraw(this);
    }

    private void PaintChrome()
    {
        var info = new DateTimePickerInfo { Size = Marshal.SizeOf<DateTimePickerInfo>() };
        SendMessage(Handle, DtmGetDateTimePickerInfo, IntPtr.Zero, ref info);

        NativeChrome.PaintOnWindow(this, (g, inset) =>
        {
            NativeChrome.PaintBorder(g, this, NativeChrome.BorderColor(this, Focused, _hover));

            // Cover the classic drop-down button (client coordinates → window coordinates).
            var button = Rectangle.FromLTRB(info.ButtonLeft, info.ButtonTop, info.ButtonRight, info.ButtonBottom);
            if (button.Width <= 0)
            {
                return;
            }

            button.Offset(inset, inset);
            button = Rectangle.FromLTRB(button.Left, 1 + inset, Width - 1 - inset, Height - 1 - inset);
            using (var fill = new SolidBrush(Enabled ? Theme.Colors.Surface : Theme.Colors.SurfaceSunken))
            {
                g.FillRectangle(fill, button);
            }

            Draw.Glyph(g, Icons.Calendar, 10f, Enabled ? Theme.Colors.TextMuted : Theme.Colors.TextDisabled, button);
        });
    }

    private const int DtmGetDateTimePickerInfo = 0x100E;

    [StructLayout(LayoutKind.Sequential)]
    private struct DateTimePickerInfo
    {
        public int Size;
        public int CheckLeft, CheckTop, CheckRight, CheckBottom;
        public int CheckState;
        public int ButtonLeft, ButtonTop, ButtonRight, ButtonBottom;
        public int ButtonState;
        public IntPtr Edit, UpDown, DropDown;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref DateTimePickerInfo lParam);
}

/// <summary>
/// A <see cref="NumericUpDown"/> with a flat themed border and chevron buttons instead of the classic
/// spin arrows. Value, limits and events are unchanged.
/// </summary>
internal sealed class ThemedNumber : NumericUpDown
{
    private SpinButtonsPainter? _buttons;
    private EditPadding? _editPadding;
    private bool _hover;

    public ThemedNumber()
    {
        BorderStyle = BorderStyle.FixedSingle;
        Font = Theme.Fonts.Body;
        ForeColor = Theme.Colors.Text;
        BackColor = Theme.Colors.Surface;
        NativeChrome.UseControlHeight(this, h => FontHeight = h);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        NativeChrome.UseControlHeight(this, h => FontHeight = h);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Controls[0] is the spin-button part of UpDownBase; it is repainted through a native hook.
        var spin = Controls.Count > 0 ? Controls[0] : null;
        if (spin != null && spin is not TextBox)
        {
            _buttons = new SpinButtonsPainter(spin);
        }

        // UpDownBase keeps its edit box at the top-left and resets any move, so the text is centred by
        // padding the edit box's own frame instead.
        var edit = Controls.OfType<TextBox>().FirstOrDefault();
        if (edit != null)
        {
            _editPadding = new EditPadding(edit, this.Px(Theme.Space.S) - this.Px(4));
        }

        foreach (Control child in Controls)
        {
            child.MouseEnter += (_, _) => SetHover(true);
            child.MouseLeave += (_, _) => SetHover(ClientRectangle.Contains(PointToClient(MousePosition)));
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _buttons?.ReleaseHandle();
        _buttons = null;
        _editPadding?.ReleaseHandle();
        _editPadding = null;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is NativeChrome.WmPaint or NativeChrome.WmNcPaint)
        {
            NativeChrome.PaintOnWindow(this, (g, _) =>
                NativeChrome.PaintBorder(g, this, NativeChrome.BorderColor(this, ContainsFocus, _hover)));
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        NativeChrome.Redraw(this);
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        NativeChrome.Redraw(this);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        SetHover(true);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(ClientRectangle.Contains(PointToClient(MousePosition)));
    }

    private void SetHover(bool hover)
    {
        if (_hover != hover)
        {
            _hover = hover;
            NativeChrome.Redraw(this);
        }
    }

    /// <summary>Gives the inner edit box left padding and centres its single line of text vertically.</summary>
    private sealed class EditPadding : NativeWindow
    {
        private readonly TextBox _edit;
        private readonly int _left;
        private Rectangle _clientInWindow;

        public EditPadding(TextBox edit, int left)
        {
            _edit = edit;
            _left = left;
            AssignHandle(edit.Handle);
            NativeChrome.RecalculateFrame(edit);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            switch (m.Msg)
            {
                case NativeChrome.WmNcCalcSize:
                    _clientInWindow = NativeChrome.InsetClient(m.LParam, _edit, _left, 0);
                    break;
                case NativeChrome.WmNcHitTest:
                    m.Result = NativeChrome.HtClient;
                    break;
                case NativeChrome.WmNcPaint:
                    NativeChrome.PaintOnWindow(_edit, (g, _) => NativeChrome.FillFrame(g, _edit, _clientInWindow, _edit.BackColor));
                    break;
            }
        }
    }

    /// <summary>Paints flat chevron buttons over the native spin buttons after each WM_PAINT.</summary>
    private sealed class SpinButtonsPainter : NativeWindow
    {
        private readonly Control _spin;

        public SpinButtonsPainter(Control spin)
        {
            _spin = spin;
            AssignHandle(spin.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            switch (m.Msg)
            {
                case NativeChrome.WmPaint:
                    Paint();
                    break;
                case NativeChrome.WmMouseMove:
                case NativeChrome.WmMouseLeave:
                case NativeChrome.WmLButtonUp:
                    _spin.Invalidate();
                    break;
            }
        }

        private void Paint()
        {
            using var g = _spin.CreateGraphics();
            var size = _spin.ClientSize;
            var enabled = _spin.Enabled;
            var mouse = _spin.PointToClient(Control.MousePosition);
            var pressed = Control.MouseButtons == MouseButtons.Left;
            var half = size.Height / 2;
            var up = new Rectangle(0, 0, size.Width, half);
            var down = new Rectangle(0, half, size.Width, size.Height - half);

            using (var surface = new SolidBrush(enabled ? Theme.Colors.Surface : Theme.Colors.SurfaceSunken))
            {
                g.FillRectangle(surface, 0, 0, size.Width, size.Height);
            }

            foreach (var (rect, glyph) in new[] { (up, Icons.ChevronUp), (down, Icons.ChevronDown) })
            {
                if (enabled && rect.Contains(mouse))
                {
                    using var hover = new SolidBrush(pressed ? Theme.Colors.BorderStrong : Theme.Colors.SurfaceSunken);
                    g.FillRectangle(hover, rect);
                }

                Draw.Glyph(g, glyph, 6.5f, enabled ? Theme.Colors.TextMuted : Theme.Colors.TextDisabled, rect);
            }
        }
    }
}

/// <summary>Helpers for repainting the frame of native Windows controls in the theme colours.</summary>
internal static class NativeChrome
{
    public const int WmPaint = 0x000F;
    public const int WmNcCalcSize = 0x0083;
    public const int WmNcHitTest = 0x0084;
    public const int WmNcPaint = 0x0085;
    public const int HtClient = 1;
    public const int WmMouseMove = 0x0200;
    public const int WmLButtonUp = 0x0202;
    public const int WmMouseLeave = 0x02A3;

    private const uint RdwInvalidate = 0x0001;
    private const uint RdwFrame = 0x0400;
    private const uint RdwAllChildren = 0x0080;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoMoveSizeZOrderActivate = 0x0002 | 0x0001 | 0x0004 | 0x0010;

    /// <summary>
    /// DateTimePicker and NumericUpDown force their height to PreferredHeight, which is FontHeight plus the
    /// frame. Setting the (protected) FontHeight through <paramref name="setFontHeight"/> makes them as tall
    /// as the other controls; the font itself does not change.
    /// </summary>
    public static void UseControlHeight(Control control, Action<int> setFontHeight)
    {
        var frame = SystemInformation.BorderSize.Height * 4 + 3;
        setFontHeight(control.Px(Theme.Size.ControlHeight) - frame);
        control.Height = control.Px(Theme.Size.ControlHeight);
    }

    public static Color BorderColor(Control control, bool focused, bool hover) =>
        !control.Enabled ? Theme.Colors.Border
        : focused ? Theme.Colors.Primary
        : hover ? Theme.Colors.InputBorderHover
        : Theme.Colors.InputBorder;

    /// <summary>
    /// Runs <paramref name="paint"/> on the whole window (frame included). The second argument is the width of
    /// the native frame, i.e. the offset of the client area inside the window.
    /// </summary>
    public static void PaintOnWindow(Control control, Action<Graphics, int> paint)
    {
        if (!control.IsHandleCreated)
        {
            return;
        }

        var dc = GetWindowDC(control.Handle);
        if (dc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            using var g = Graphics.FromHdc(dc);
            paint(g, Math.Max(0, (control.Width - control.ClientSize.Width) / 2));
        }
        finally
        {
            ReleaseDC(control.Handle, dc);
        }
    }

    /// <summary>A 1-pixel border, with the classic 3-D frame underneath covered in the surface colour.</summary>
    public static void PaintBorder(Graphics g, Control control, Color color)
    {
        var w = control.Width;
        var h = control.Height;
        using (var fill = new Pen(control.Enabled ? Theme.Colors.Surface : Theme.Colors.SurfaceSunken))
        {
            g.DrawRectangle(fill, 1, 1, w - 3, h - 3);
        }

        using var pen = new Pen(color);
        g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
    }

    /// <summary>
    /// Handles WM_NCCALCSIZE for padding: shrinks the proposed client rectangle (the first RECT at
    /// <paramref name="rectPointer"/>) by the given left/right padding and centres one line of text vertically
    /// (multi-line boxes get a fixed top/bottom padding). Returns the client area in window coordinates.
    /// </summary>
    public static Rectangle InsetClient(IntPtr rectPointer, TextBoxBase box, int left, int right)
    {
        var rect = Marshal.PtrToStructure<NativeRect>(rectPointer);
        var height = rect.Bottom - rect.Top;
        var line = TextRenderer.MeasureText("Ag", box.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
        var vertical = box.Multiline ? box.Px(6) : Math.Max(0, (height - line) / 2);

        var originX = rect.Left;
        var originY = rect.Top;
        rect.Left += left;
        rect.Right = Math.Max(rect.Left + 1, rect.Right - right);
        rect.Top += vertical;
        rect.Bottom = Math.Max(rect.Top + 1, rect.Bottom - vertical);
        Marshal.StructureToPtr(rect, rectPointer, false);
        return new Rectangle(rect.Left - originX, rect.Top - originY, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>Fills the padding around <paramref name="clientInWindow"/> (window coordinates).</summary>
    public static void FillFrame(Graphics g, Control control, Rectangle clientInWindow, Color color)
    {
        using var region = new Region(new Rectangle(0, 0, control.Width, control.Height));
        region.Exclude(clientInWindow);
        using var fill = new SolidBrush(color);
        g.FillRegion(fill, region);
    }

    /// <summary>Asks Windows to recalculate the frame (sends WM_NCCALCSIZE again).</summary>
    public static void RecalculateFrame(Control control)
    {
        if (control.IsHandleCreated)
        {
            SetWindowPos(control.Handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoMoveSizeZOrderActivate | SwpFrameChanged);
        }
    }

    public static void Redraw(Control control)
    {
        if (control.IsHandleCreated)
        {
            RedrawWindow(control.Handle, IntPtr.Zero, IntPtr.Zero, RdwFrame | RdwInvalidate | RdwAllChildren);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rcUpdate, IntPtr hrgnUpdate, uint flags);
}
