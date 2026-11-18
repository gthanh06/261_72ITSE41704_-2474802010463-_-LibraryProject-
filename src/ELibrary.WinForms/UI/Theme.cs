using System.Drawing.Text;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Design tokens of the "Warm library" theme. This is the only place where colours, spacing, corner
/// radii, sizes, fonts and motion timings are defined; every control and screen reads them from here.
/// Sizes are logical pixels at 96 DPI — convert them with <see cref="ControlScaling.Px"/>.
/// </summary>
internal static class Theme
{
    internal static class Colors
    {
        // Surfaces
        public static readonly Color Background = Hex(0xFAF7F2);      // paper
        public static readonly Color Surface = Hex(0xFFFFFF);         // cards, tables, inputs
        public static readonly Color SurfaceSunken = Hex(0xF3EEE4);   // table header, hover
        public static readonly Color RowAlternate = Hex(0xFCFAF6);
        public static readonly Color Border = Hex(0xE8E1D5);
        public static readonly Color BorderStrong = Hex(0xD3C8B6);
        public static readonly Color InputBorder = Hex(0x9A8B78);       // 3.3:1 on white (WCAG 1.4.11)
        public static readonly Color InputBorderHover = Hex(0x6B5E4F);

        // Text
        public static readonly Color Text = Hex(0x2B2118);
        public static readonly Color TextMuted = Hex(0x6B5E4F);
        public static readonly Color TextDisabled = Hex(0xA3968A);

        // Brand
        public static readonly Color Primary = Hex(0x1F5F4A);         // deep moss green
        public static readonly Color PrimaryHover = Hex(0x174A3A);
        public static readonly Color PrimaryPressed = Hex(0x113A2D);
        public static readonly Color PrimarySoft = Hex(0xE3EFEA);
        public static readonly Color OnPrimary = Hex(0xFFFFFF);
        public static readonly Color Accent = Hex(0xB7791F);          // brass
        public static readonly Color AccentBright = Hex(0xD69E2E);

        // Semantic (text colour on its soft background passes WCAG AA)
        public static readonly Color Success = Hex(0x256B42);
        public static readonly Color SuccessSoft = Hex(0xE6F2EA);
        public static readonly Color Info = Hex(0x2C4A6E);            // library ink blue
        public static readonly Color InfoSoft = Hex(0xE5ECF4);
        public static readonly Color Warning = Hex(0x7A4F0F);
        public static readonly Color WarningSoft = Hex(0xFBF1DE);
        public static readonly Color Danger = Hex(0xB42318);
        public static readonly Color DangerHover = Hex(0x912018);
        public static readonly Color DangerSoft = Hex(0xFBE9E7);
        public static readonly Color Neutral = Hex(0x5E5245);
        public static readonly Color NeutralSoft = Hex(0xEFEAE2);

        // Sidebar
        public static readonly Color Sidebar = Hex(0x173F33);
        public static readonly Color SidebarHover = Hex(0x1E4C3F);
        public static readonly Color SidebarActive = Hex(0x22574A);
        public static readonly Color SidebarText = Hex(0xE6DDC9);
        public static readonly Color SidebarTextMuted = Hex(0xA8BBAF);
        public static readonly Color SidebarActiveText = Hex(0xFFFFFF);
        public static readonly Color SidebarDivider = Hex(0x2A5A4C);

        // Effects
        public static readonly Color FocusRing = Accent;
        public static readonly Color Shadow = Hex(0x2B2118);
        public static readonly Color Overlay = Hex(0x10261F);

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
        public const int Card = 10;
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
        public static readonly string HeadingFamily = FirstInstalled("Georgia", "Cambria", "Times New Roman");
        public static readonly string IconFamily = FirstInstalled("Segoe Fluent Icons", "Segoe MDL2 Assets");

        // Type scale (points): 20 page title · 16 section · 13 card heading · 10 body · 9 small · 8.5 caption
        public static readonly Font PageTitle = new(HeadingFamily, 20f, FontStyle.Bold);
        public static readonly Font Section = new(HeadingFamily, 16f, FontStyle.Bold);
        public static readonly Font CardTitle = new(SemiboldFamily, 13f);
        public static readonly Font Body = new(TextFamily, 10f);
        public static readonly Font BodySemibold = new(SemiboldFamily, 10f);
        public static readonly Font Small = new(TextFamily, 9f);
        public static readonly Font SmallSemibold = new(SemiboldFamily, 9f);
        public static readonly Font Caption = new(SemiboldFamily, 8.5f);
        public static readonly Font Brand = new(HeadingFamily, 14f, FontStyle.Bold);

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
