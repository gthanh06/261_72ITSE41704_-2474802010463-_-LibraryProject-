using System.Drawing.Text;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Design tokens of the theme (light, Windows 11 style). This is the only place where colours, spacing, corner
/// radii, sizes, fonts and motion timings are defined; every control and screen reads them from here.
/// Sizes are logical pixels at 96 DPI — convert them with <see cref="ControlScaling.Px"/>.
/// </summary>
internal static class Theme
{
    internal static class Colors
    {
        // Surfaces (cool neutral greys, as in Windows 11)
        public static readonly Color Background = Hex(0xF5F6F8);      // window background
        public static readonly Color Surface = Hex(0xFFFFFF);         // cards, tables, inputs
        public static readonly Color SurfaceSunken = Hex(0xF1F3F5);   // table header, hover
        public static readonly Color RowAlternate = Hex(0xFAFBFC);
        public static readonly Color Border = Hex(0xE3E6EA);
        public static readonly Color BorderStrong = Hex(0xCDD2D8);
        public static readonly Color InputBorder = Hex(0x8B929C);       // 3.2:1 on white (WCAG 1.4.11)
        public static readonly Color InputBorderHover = Hex(0x5F6670);

        // Text
        public static readonly Color Text = Hex(0x1F2328);
        public static readonly Color TextMuted = Hex(0x5B636E);         // 6:1 on white
        public static readonly Color TextDisabled = Hex(0xA2A8B0);

        // Brand
        public static readonly Color Primary = Hex(0x0F7B5F);         // library green, 5.2:1 on white
        public static readonly Color PrimaryHover = Hex(0x0C6A52);
        public static readonly Color PrimaryPressed = Hex(0x095943);
        public static readonly Color PrimarySoft = Hex(0xE3F2EC);
        public static readonly Color OnPrimary = Hex(0xFFFFFF);
        public static readonly Color Accent = Primary;                 // brand mark, selection indicator
        public static readonly Color AccentBright = Hex(0x12936F);

        // Semantic (text colour on its soft background passes WCAG AA)
        public static readonly Color Success = Hex(0x1E6B41);
        public static readonly Color SuccessSoft = Hex(0xE5F4EA);
        public static readonly Color Info = Hex(0x1F57A4);
        public static readonly Color InfoSoft = Hex(0xE8EFFB);
        public static readonly Color Warning = Hex(0x8A5300);
        public static readonly Color WarningSoft = Hex(0xFDF2DC);
        public static readonly Color Danger = Hex(0xC42B1C);            // Windows 11 critical red
        public static readonly Color DangerHover = Hex(0xA32317);
        public static readonly Color DangerSoft = Hex(0xFDE7E5);
        public static readonly Color Neutral = Hex(0x4B5563);
        public static readonly Color NeutralSoft = Hex(0xEEF0F3);

        // Sidebar (light navigation pane; the selected item is tinted with the brand colour)
        public static readonly Color Sidebar = Hex(0xEEF1F4);
        public static readonly Color SidebarHover = Hex(0xE3E7EC);
        public static readonly Color SidebarActive = Hex(0xDDEDE6);
        public static readonly Color SidebarText = Hex(0x2B3036);
        public static readonly Color SidebarTextMuted = Hex(0x5F6670);  // 5:1 on the sidebar
        public static readonly Color SidebarActiveText = Hex(0x1F2328);
        public static readonly Color SidebarDivider = Hex(0xDDE1E6);

        // Effects
        public static readonly Color FocusRing = Hex(0x1F2328);        // dark ring, as Windows 11 draws keyboard focus
        public static readonly Color Shadow = Hex(0x1F2328);
        public static readonly Color Overlay = Hex(0x0B0F14);

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
