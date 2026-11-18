using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Gives a window's title bar the theme colours and rounded corners on Windows 11 (build 22000 and later).
/// On older Windows the call does nothing and the standard title bar is kept.
/// </summary>
internal static class WindowChrome
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwcpRound = 2;

    /// <summary>Applies the caption colour now (if the handle exists) and every time the handle is re-created.</summary>
    public static void Apply(Form form, Color caption)
    {
        form.HandleCreated += (_, _) => Set(form.Handle, caption);
        if (form.IsHandleCreated)
        {
            Set(form.Handle, caption);
        }
    }

    private static void Set(IntPtr hwnd, Color caption)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var corner = DwmwcpRound;
        var captionColor = ToColorRef(caption);
        var textColor = ToColorRef(Theme.Colors.Text);
        var borderColor = ToColorRef(Theme.Colors.BorderStrong);
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref textColor, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(int));
    }

    // COLORREF is 0x00BBGGRR.
    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
