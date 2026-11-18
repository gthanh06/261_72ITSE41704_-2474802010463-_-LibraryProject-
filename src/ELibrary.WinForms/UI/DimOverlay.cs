using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// A translucent layer over the main window while a modal dialog is open, so the dialog stands out.
/// It never takes focus or input and closes together with the dialog.
/// </summary>
internal sealed class DimOverlay : Form
{
    private const int GwOwner = 4;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const double TargetOpacity = 0.32;

    private DimOverlay(Form owner)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Colors.Overlay;
        Opacity = 0;
        Bounds = owner.Bounds;
        Enabled = false;
        AccessibleRole = AccessibleRole.None;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExNoActivate = 0x08000000;
            const int wsExToolWindow = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= wsExNoActivate | wsExToolWindow;
            return cp;
        }
    }

    /// <summary>Call from the dialog's OnShown. Does nothing when the dialog has no visible owner window.</summary>
    public static void AttachTo(Form dialog)
    {
        var owner = dialog.Owner ?? FromHandle(GetWindow(dialog.Handle, GwOwner)) as Form;
        if (owner is null or DimOverlay || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
        {
            return;
        }

        var overlay = new DimOverlay(owner);
        overlay.Show(owner);
        // Keep the overlay directly below the dialog in the z-order.
        SetWindowPos(overlay.Handle, dialog.Handle, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        Tween.Run(Theme.Motion.NormalMs, p => overlay.Opacity = TargetOpacity * p);
        dialog.FormClosed += (_, _) => overlay.Close();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, int cmd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
