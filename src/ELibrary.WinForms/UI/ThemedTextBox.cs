using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// A single-line <see cref="TextBox"/> whose 1-pixel border is drawn in the theme colours
/// (border → stronger on hover → primary when focused). Everything else is the standard TextBox,
/// so existing code that reads <c>Text</c> or handles key events keeps working.
/// </summary>
internal sealed class ThemedTextBox : TextBox
{
    private const int WmNcPaint = 0x0085;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwFrame = 0x0400;
    private bool _hover;

    public ThemedTextBox()
    {
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Theme.Colors.Surface;
        ForeColor = Theme.Colors.Text;
        Font = Theme.Fonts.Body;
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmNcPaint && BorderStyle == BorderStyle.FixedSingle)
        {
            PaintBorder();
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        RedrawFrame();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        RedrawFrame();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        RedrawFrame();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        RedrawFrame();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        BackColor = Enabled ? Theme.Colors.Surface : Theme.Colors.SurfaceSunken;
        RedrawFrame();
    }

    private void PaintBorder()
    {
        var dc = GetWindowDC(Handle);
        if (dc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            using var g = Graphics.FromHdc(dc);
            var color = !Enabled ? Theme.Colors.Border
                : Focused ? Theme.Colors.Primary
                : _hover ? Theme.Colors.InputBorderHover
                : Theme.Colors.InputBorder;
            using var pen = new Pen(color);
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
        finally
        {
            ReleaseDC(Handle, dc);
        }
    }

    private void RedrawFrame()
    {
        if (IsHandleCreated)
        {
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RdwFrame | RdwInvalidate);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rcUpdate, IntPtr hrgnUpdate, uint flags);
}
