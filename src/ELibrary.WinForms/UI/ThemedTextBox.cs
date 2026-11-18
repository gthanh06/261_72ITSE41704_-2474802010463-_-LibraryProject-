using System.Drawing.Drawing2D;

namespace ELibrary.WinForms.UI;

/// <summary>
/// A <see cref="TextBox"/> with the theme's control height (34 px), inner padding and a 1-pixel border in the
/// theme colours (border → stronger on hover → primary when focused). The padding is the native window
/// frame (WM_NCCALCSIZE), so this is still a plain TextBox: code that reads <c>Text</c> or handles key
/// events keeps working.
/// </summary>
internal sealed class ThemedTextBox : TextBox
{
    private bool _hover;
    private Rectangle _clientInWindow;

    public ThemedTextBox()
    {
        BorderStyle = BorderStyle.None;
        AutoSize = false;
        BackColor = Theme.Colors.Surface;
        ForeColor = Theme.Colors.Text;
        Font = Theme.Fonts.Body;
        Height = this.Px(Theme.Size.ControlHeight);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        switch (m.Msg)
        {
            case NativeChrome.WmNcCalcSize:
                var padding = this.Px(Theme.Space.S) - this.Px(2);
                _clientInWindow = NativeChrome.InsetClient(m.LParam, this, padding, padding);
                break;
            case NativeChrome.WmNcHitTest:
                // Clicks on the padding place the caret like clicks on the text.
                m.Result = NativeChrome.HtClient;
                break;
            case NativeChrome.WmNcPaint:
                PaintFrame();
                break;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeChrome.RecalculateFrame(this);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        NativeChrome.RecalculateFrame(this);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        NativeChrome.RecalculateFrame(this);
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        BackColor = Enabled ? Theme.Colors.Surface : Theme.Colors.SurfaceSunken;
        NativeChrome.Redraw(this);
    }

    private void PaintFrame()
    {
        NativeChrome.PaintOnWindow(this, (g, _) =>
        {
            NativeChrome.FillFrame(g, this, _clientInWindow, BackColor);
            g.SmoothingMode = SmoothingMode.None;
            using var pen = new Pen(NativeChrome.BorderColor(this, Focused, _hover));
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        });
    }
}
