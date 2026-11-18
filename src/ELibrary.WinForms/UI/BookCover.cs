using System.Drawing.Drawing2D;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Draws a generated book cover (the catalogue has no cover images): a two-tone cloth colour picked from
/// the title, a spine shadow, the title in the heading serif and the authors at the foot. The same title
/// always gets the same cover.
/// </summary>
internal static class BookCover
{
    private static readonly (int Top, int Bottom)[] Cloths =
    {
        (0x1F5F4A, 0x0F3B2D), // library green
        (0x2C4A6E, 0x1A2E47), // ink blue
        (0x7A3B2E, 0x4E2219), // brick
        (0x8A5A12, 0x5C3A08), // ochre
        (0x5E4B6B, 0x3B2E45), // plum
        (0x2F5D62, 0x1B3A3E), // teal
        (0x6B2D4A, 0x441B2F), // wine
        (0x3E4A59, 0x262E38)  // slate
    };

    /// <summary>
    /// Renders a cover into a new opaque bitmap whose rounded corners show <paramref name="page"/> (GDI text
    /// cannot be drawn onto transparent pixels). The caller disposes it.
    /// </summary>
    public static Bitmap Render(Size size, string title, string authors, float dpiScale, Color page)
    {
        var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
        using var g = Graphics.FromImage(bitmap);
        g.Clear(page);
        Draw.HighQuality(g);
        Paint(g, new RectangleF(0, 0, size.Width, size.Height), title, authors, dpiScale);
        return bitmap;
    }

    public static void Paint(Graphics g, RectangleF r, string title, string authors, float dpiScale)
    {
        var (topHex, bottomHex) = Cloths[(int)(StableHash(title) % (uint)Cloths.Length)];
        var top = Hex(topHex);
        var bottom = Hex(bottomHex);
        var radius = 4f * dpiScale;

        using var shape = Draw.RoundedRect(r, radius);
        using (var cloth = new LinearGradientBrush(r, top, bottom, LinearGradientMode.Vertical))
        {
            g.FillPath(cloth, shape);
        }

        var state = g.Save();
        g.SetClip(shape);

        // Spine: a darker band with a thin highlight, as on a bound book.
        var spine = r.Width * 0.07f;
        using (var spineBrush = new LinearGradientBrush(new RectangleF(r.X, r.Y, spine + 1, r.Height),
                   Color.FromArgb(90, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Horizontal))
        {
            g.FillRectangle(spineBrush, r.X, r.Y, spine, r.Height);
        }

        using (var highlight = new Pen(Color.FromArgb(50, 255, 255, 255), Math.Max(1f, dpiScale)))
        {
            g.DrawLine(highlight, r.X + spine, r.Y, r.X + spine, r.Bottom);
        }

        // Gilt rules above the title and above the authors.
        var gilt = Color.FromArgb(150, 0xE8, 0xC9, 0x7A);
        var left = r.X + spine + 10f * dpiScale;
        var right = r.Right - 10f * dpiScale;
        using (var rule = new Pen(gilt, Math.Max(1f, dpiScale)))
        {
            g.DrawLine(rule, left, r.Y + r.Height * 0.14f, right, r.Y + r.Height * 0.14f);
            g.DrawLine(rule, left, r.Y + r.Height * 0.78f, right, r.Y + r.Height * 0.78f);
        }

        var ink = Draw.Blend(bottom, Color.White, 0.92f);
        var textRect = Rectangle.Round(new RectangleF(left, r.Y + r.Height * 0.19f, right - left, r.Height * 0.55f));
        var points = title.Length switch { <= 18 => 12.5f, <= 40 => 11f, _ => 9.5f };
        using (var titleFont = new Font(Theme.Fonts.HeadingFamily, points, FontStyle.Bold))
        {
            TextRenderer.DrawText(g, title, titleFont, textRect, ink,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
        }

        var authorRect = Rectangle.Round(new RectangleF(left, r.Y + r.Height * 0.81f, right - left, r.Height * 0.15f));
        TextRenderer.DrawText(g, authors, Theme.Fonts.Small, authorRect, Draw.Blend(bottom, Color.White, 0.7f),
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);

        g.Restore(state);
    }

    private static uint StableHash(string text)
    {
        // FNV-1a: string.GetHashCode changes between runs, this does not.
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash = unchecked((hash ^ c) * 16777619u);
        }

        return hash;
    }

    private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
