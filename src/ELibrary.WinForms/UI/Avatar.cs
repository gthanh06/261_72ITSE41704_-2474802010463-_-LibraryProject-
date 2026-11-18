namespace ELibrary.WinForms.UI;

/// <summary>
/// A round avatar with the person's initials on a colour chosen from their user name, so the same
/// person always gets the same colour. (Accounts have no photo column, so no image is stored.)
/// </summary>
internal sealed class Avatar : Control
{
    // Dark, warm tones: white initials on each of them pass WCAG AA.
    private static readonly Color[] Palette =
    {
        Color.FromArgb(0x1F, 0x5F, 0x4A), Color.FromArgb(0x8A, 0x5A, 0x12), Color.FromArgb(0x2C, 0x4A, 0x6E),
        Color.FromArgb(0x7A, 0x3B, 0x2E), Color.FromArgb(0x4F, 0x5D, 0x2F), Color.FromArgb(0x5E, 0x4B, 0x6B)
    };

    private string _name = string.Empty;
    private string _key = string.Empty;

    public Avatar(int diameter = 40)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        Size = new Size(this.Px(diameter), this.Px(diameter));
        AccessibleRole = AccessibleRole.Graphic;
    }

    /// <summary>Sets the full name (for the initials) and a stable key such as the user name (for the colour).</summary>
    public void SetPerson(string fullName, string key)
    {
        _name = fullName;
        _key = key;
        AccessibleName = $"Avatar of {fullName}";
        Invalidate();
    }

    /// <summary>"Trần Thị Thu" → "TT"; "admin" → "A".</summary>
    public static string Initials(string? fullName)
    {
        var words = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length switch
        {
            0 => "?",
            1 => char.ToUpperInvariant(words[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[^1][0]))
        };
    }

    public static Color ColorFor(string? key)
    {
        var hash = 17;
        foreach (var ch in key ?? string.Empty)
        {
            hash = unchecked(hash * 31 + char.ToLowerInvariant(ch));
        }

        return Palette[(hash & 0x7FFFFFFF) % Palette.Length];
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.EffectiveBackColor(Parent));
        Draw.HighQuality(g);
        var d = Math.Min(Width, Height) - 1;
        using (var brush = new SolidBrush(ColorFor(_key.Length > 0 ? _key : _name)))
        {
            g.FillEllipse(brush, 0, 0, d, d);
        }

        var size = Math.Max(8f, d * 0.36f * 72f / DeviceDpi);
        using var font = new Font(Theme.Fonts.SemiboldFamily, size);
        TextRenderer.DrawText(g, Initials(_name), font, new Rectangle(0, 0, d, d), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}
