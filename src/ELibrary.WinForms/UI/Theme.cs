using System.Drawing.Text;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Design tokens of the theme (light, Windows 11 style). This is the only place where colours, spacing, corner
/// radii, sizes, fonts and motion timings are defined; every control and screen reads them from here.
/// Sizes are logical pixels at 96 DPI — convert them with <see cref="ControlScaling.Px"/>.
/// </summary>
internal static class Theme
{
    /// <summary>
    /// Colour tokens. Each has a light and a dark value; the palette is chosen once at start-up from
    /// <see cref="ThemePreference"/> (switching restarts the app). Dark values keep the same WCAG contrast goals.
    /// </summary>
    internal static class Colors
    {
        /// <summary>True when the dark palette is in use.</summary>
        public static readonly bool IsDark = ThemePreference.IsDark;

        // Surfaces (cool neutral greys, as in Windows 11)
        public static readonly Color Background = Pick(0xF5F6F8, 0x1B1D20);      // window background
        public static readonly Color Surface = Pick(0xFFFFFF, 0x25282C);         // cards, tables, inputs
        public static readonly Color SurfaceSunken = Pick(0xF1F3F5, 0x2D3035);   // table header, hover
        public static readonly Color RowAlternate = Pick(0xFAFBFC, 0x282B2F);
        public static readonly Color Border = Pick(0xE3E6EA, 0x3A3E44);
        public static readonly Color BorderStrong = Pick(0xCDD2D8, 0x4A4F56);
        public static readonly Color InputBorder = Pick(0x8B929C, 0x7D848E);       // 3.2:1 on white (WCAG 1.4.11)
        public static readonly Color InputBorderHover = Pick(0x5F6670, 0xA3AAB3);

        // Text
        public static readonly Color Text = Pick(0x1F2328, 0xE8EAED);
        public static readonly Color TextMuted = Pick(0x5B636E, 0xA7AEB7);         // 6:1 on white
        public static readonly Color TextDisabled = Pick(0xA2A8B0, 0x6B727B);

        // Brand
        public static readonly Color Primary = Pick(0x0F7B5F, 0x4CC29A);         // library green, 5.2:1 on white
        public static readonly Color PrimaryHover = Pick(0x0C6A52, 0x5ED0A8);
        public static readonly Color PrimaryPressed = Pick(0x095943, 0x3BAE88);
        public static readonly Color PrimarySoft = Pick(0xE3F2EC, 0x1E3A31);
        public static readonly Color OnPrimary = Pick(0xFFFFFF, 0x0B1D16);
        public static readonly Color Accent = Primary;                 // brand mark, selection indicator
        public static readonly Color AccentBright = Pick(0x12936F, 0x6FD9B4);

        // Semantic (text colour on its soft background passes WCAG AA)
        public static readonly Color Success = Pick(0x1E6B41, 0x6CCB8F);
        public static readonly Color SuccessSoft = Pick(0xE5F4EA, 0x1D3326);
        public static readonly Color Info = Pick(0x1F57A4, 0x7FB0F0);
        public static readonly Color InfoSoft = Pick(0xE8EFFB, 0x1D2A3D);
        public static readonly Color Warning = Pick(0x8A5300, 0xF0B45A);
        public static readonly Color WarningSoft = Pick(0xFDF2DC, 0x3A2E19);
        public static readonly Color Danger = Pick(0xC42B1C, 0xF07067);            // Windows 11 critical red
        public static readonly Color DangerHover = Pick(0xA32317, 0xF48A82);
        public static readonly Color DangerSoft = Pick(0xFDE7E5, 0x3D2220);
        public static readonly Color Neutral = Pick(0x4B5563, 0xB4BAC2);
        public static readonly Color NeutralSoft = Pick(0xEEF0F3, 0x30343A);

        // Sidebar (light navigation pane; the selected item is tinted with the brand colour)
        public static readonly Color Sidebar = Pick(0xEEF1F4, 0x202326);
        public static readonly Color SidebarHover = Pick(0xE3E7EC, 0x2A2E33);
        public static readonly Color SidebarActive = Pick(0xDDEDE6, 0x1F3A31);
        public static readonly Color SidebarText = Pick(0x2B3036, 0xDADDE1);
        public static readonly Color SidebarTextMuted = Pick(0x5F6670, 0x9AA1AA);  // 5:1 on the sidebar
        public static readonly Color SidebarActiveText = Pick(0x1F2328, 0xFFFFFF);
        public static readonly Color SidebarDivider = Pick(0xDDE1E6, 0x33373C);

        // Effects
        public static readonly Color FocusRing = Pick(0x1F2328, 0xE8EAED);        // dark ring, as Windows 11 draws keyboard focus
        public static readonly Color Shadow = Pick(0x1F2328, 0x000000);
        public static readonly Color Overlay = Pick(0x0B0F14, 0x000000);

        private static Color Pick(int light, int dark) => Hex(IsDark ? dark : light);

        private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }

    /// <summary>Spacing scale (logical px): 4 · 8 · 12 · 16 · 24 · 32 · 48.</summary>
    internal static class Space
    {
        public const int Xxs = 4;
        public const int Xs = 8;
        public const int S = 12;
        public const int M = 16;
        public const int L = 24;
        public const int Xl = 32;
        public const int Xxl = 48;
    }

    internal static class Radius
    {
        public const int Control = 6;
        public const int Card = 8;
    }

    internal static class Size
    {
        public const int ControlHeight = 34;
        public const int CompactControlHeight = 28;
        public const int GridRowHeight = 38;
        public const int GridHeaderHeight = 38;
        public const int SidebarWidth = 248;
        public const int SidebarCollapsedWidth = 64;
        public const int FocusRingWidth = 2;
    }

    internal static class Fonts
    {
        private static readonly HashSet<string> Installed = LoadInstalledFamilies();

        public static readonly string TextFamily = FirstInstalled("Segoe UI Variable Text", "Segoe UI");
        public static readonly string SemiboldFamily = FirstInstalled("Segoe UI Variable Text Semibold", "Segoe UI Semibold", "Segoe UI");
        // GDI truncates family names to 31 characters, hence "…Display Semib".
        public static readonly string HeadingFamily = FirstInstalled("Segoe UI Variable Display Semib", "Segoe UI Semibold", "Segoe UI");
        public static readonly string IconFamily = FirstInstalled("Segoe Fluent Icons", "Segoe MDL2 Assets");

        // Type scale (points): 20 page title · 15 section · 13 card heading · 10 body · 9 small · 8.5 caption
        public static readonly Font PageTitle = new(HeadingFamily, 20f);
        public static readonly Font Section = new(HeadingFamily, 15f);
        public static readonly Font CardTitle = new(SemiboldFamily, 13f);
        public static readonly Font Body = new(TextFamily, 10f);
        public static readonly Font BodySemibold = new(SemiboldFamily, 10f);
        public static readonly Font Small = new(TextFamily, 9f);
        public static readonly Font SmallSemibold = new(SemiboldFamily, 9f);
        public static readonly Font Caption = new(SemiboldFamily, 8.5f);
        public static readonly Font Brand = new(HeadingFamily, 13f);

        /// <summary>Body text for native controls that measure Segoe UI Variable wrongly (the date picker).</summary>
        public static readonly Font NativeBody = new(FirstInstalled("Segoe UI"), 10f);

        private static readonly Dictionary<float, Font> IconFonts = new();

        /// <summary>The icon font at the given size (cached; never dispose it).</summary>
        public static Font Icon(float points = 12f)
        {
            lock (IconFonts)
            {
                if (!IconFonts.TryGetValue(points, out var font))
                {
                    font = new Font(IconFamily, points);
                    IconFonts[points] = font;
                }

                return font;
            }
        }

        private static string FirstInstalled(params string[] families) =>
            families.FirstOrDefault(Installed.Contains) ?? families[^1];

        private static HashSet<string> LoadInstalledFamilies()
        {
            using var collection = new InstalledFontCollection();
            return collection.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static class Motion
    {
        public const int FastMs = 120;
        public const int NormalMs = 180;
        public const int ToastMs = 4500;
        public const int ToastLongMs = 7000;

        /// <summary>Set to false to switch every animation off (for example from a user preference).</summary>
        public static bool AllowAnimations { get; set; } = true;

        /// <summary>Animations run only when allowed here and when Windows' "Animation effects" setting is on.</summary>
        public static bool Enabled => AllowAnimations && SystemInformation.UIEffectsEnabled;
    }
}

/// <summary>Converts logical (96 DPI) sizes to device pixels for the control's current DPI.</summary>
internal static class ControlScaling
{
    public static int Px(this Control control, int logical) => control.LogicalToDeviceUnits(logical);

    public static Padding Px(this Control control, int left, int top, int right, int bottom) =>
        new(control.Px(left), control.Px(top), control.Px(right), control.Px(bottom));

    public static Padding PxAll(this Control control, int all) => new(control.Px(all));
}
