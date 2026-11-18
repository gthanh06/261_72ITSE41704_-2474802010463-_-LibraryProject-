using Microsoft.Win32;

namespace ELibrary.WinForms.UI;

/// <summary>Light, dark, or whatever Windows uses for apps.</summary>
internal enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>
/// The user's choice of light or dark theme, kept per Windows user in %LOCALAPPDATA%\ELibrary\theme.txt.
/// It is read once at start-up (the theme's colours are fixed for the life of the process).
/// </summary>
internal static class ThemePreference
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELibrary", "theme.txt");

    /// <summary>True when this run of the app uses the dark palette.</summary>
    public static bool IsDark { get; } = Resolve(Load());

    /// <summary>
    /// The saved choice. The environment variable ELIBRARY_THEME (light, dark or system) overrides it for one
    /// run, e.g. to take screenshots of both themes without touching the user's setting.
    /// </summary>
    public static ThemeMode Load()
    {
        if (Enum.TryParse<ThemeMode>(Environment.GetEnvironmentVariable("ELIBRARY_THEME"), true, out var forced))
        {
            return forced;
        }

        try
        {
            return File.Exists(FilePath) && Enum.TryParse<ThemeMode>(File.ReadAllText(FilePath).Trim(), true, out var mode)
                ? mode
                : ThemeMode.System;
        }
        catch (Exception)
        {
            return ThemeMode.System; // unreadable preference: just follow Windows
        }
    }

    public static void Save(ThemeMode mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, mode.ToString());
    }

    private static bool Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => WindowsAppsUseDarkTheme()
    };

    /// <summary>Windows Settings › Personalization › Colors › "Choose your default app mode".</summary>
    private static bool WindowsAppsUseDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
