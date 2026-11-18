using System.Reflection;
using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Helpers that stop controls from flashing while they are resized or swapped. Display only.
/// </summary>
internal static class AntiFlicker
{
    private const int GwlExStyle = -20;
    private const int WsExComposited = 0x02000000;
    private const int WmSetRedraw = 0x000B;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwErase = 0x0004;
    private const uint RdwAllChildren = 0x0080;
    private const uint RdwUpdateNow = 0x0100;
    private const uint RdwFrame = 0x0400;

    private static readonly PropertyInfo? DoubleBufferedProperty =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Turns on the protected DoubleBuffered flag of a stock control (e.g. DataGridView).</summary>
    public static void EnableDoubleBuffering(Control control) => DoubleBufferedProperty?.SetValue(control, true);

    /// <summary>
    /// Paints the whole window (with its children) off-screen while the user drags its border, so the
    /// layout does not flash. Composited painting is only on during the drag: left on, it would make every
    /// small repaint (hover, animation) redraw the whole window.
    /// </summary>
    public static void CompositeWhileResizing(Form form)
    {
        form.ResizeBegin += (_, _) => SetComposited(form, true);
        form.ResizeEnd += (_, _) => SetComposited(form, false);
    }

    /// <summary>
    /// Stops <paramref name="control"/> from painting until the returned object is disposed, then repaints it
    /// (and its children) in one go. Use around swapping or re-laying out child controls.
    /// </summary>
    public static IDisposable SuspendDrawing(Control control)
    {
        if (!control.IsHandleCreated)
        {
            return new Resume(null);
        }

        SendMessage(control.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
        return new Resume(control);
    }

    private static void SetComposited(Form form, bool on)
    {
        if (!form.IsHandleCreated)
        {
            return;
        }

        var style = GetWindowLongPtr(form.Handle, GwlExStyle).ToInt64();
        var updated = on ? style | WsExComposited : style & ~WsExComposited;
        if (updated != style)
        {
            SetWindowLongPtr(form.Handle, GwlExStyle, new IntPtr(updated));
            if (!on)
            {
                form.Invalidate(true);
            }
        }
    }

    private sealed class Resume : IDisposable
    {
        private Control? _control;

        public Resume(Control? control) => _control = control;

        public void Dispose()
        {
            var control = _control;
            _control = null;
            if (control == null || control.IsDisposed || !control.IsHandleCreated)
            {
                return;
            }

            SendMessage(control.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
            RedrawWindow(control.Handle, IntPtr.Zero, IntPtr.Zero,
                RdwInvalidate | RdwErase | RdwFrame | RdwAllChildren | RdwUpdateNow);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr update, IntPtr region, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
}
