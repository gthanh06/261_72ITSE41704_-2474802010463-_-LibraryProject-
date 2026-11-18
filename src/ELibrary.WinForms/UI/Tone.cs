namespace ELibrary.WinForms.UI;

/// <summary>The meaning of a badge, banner, toast or dialog. Colour is never the only signal: each tone also has an icon.</summary>
internal enum Tone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger,
    Accent
}

internal static class Tones
{
    /// <summary>Text/icon colour (passes WCAG AA on <see cref="Soft"/> and on white).</summary>
    public static Color Strong(Tone tone) => tone switch
    {
        Tone.Info => Theme.Colors.Info,
        Tone.Success => Theme.Colors.Success,
        Tone.Warning => Theme.Colors.Warning,
        Tone.Danger => Theme.Colors.Danger,
        Tone.Accent => Theme.Colors.Primary,
        _ => Theme.Colors.Neutral
    };

    /// <summary>Background colour for badges, banners and icon circles.</summary>
    public static Color Soft(Tone tone) => tone switch
    {
        Tone.Info => Theme.Colors.InfoSoft,
        Tone.Success => Theme.Colors.SuccessSoft,
        Tone.Warning => Theme.Colors.WarningSoft,
        Tone.Danger => Theme.Colors.DangerSoft,
        Tone.Accent => Theme.Colors.PrimarySoft,
        _ => Theme.Colors.NeutralSoft
    };

    public static string Glyph(Tone tone) => tone switch
    {
        Tone.Success => Icons.Success,
        Tone.Warning => Icons.Warning,
        Tone.Danger => Icons.Error,
        _ => Icons.Info
    };
}
