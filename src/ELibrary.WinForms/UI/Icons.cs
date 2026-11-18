namespace ELibrary.WinForms.UI;

/// <summary>
/// Icon glyphs from the Windows icon font (Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on
/// Windows 10 — both ship with Windows, so no icon library is needed). Draw them with
/// <see cref="Theme.Fonts.Icon"/>.
/// </summary>
internal static class Icons
{
    // Navigation
    public const string Library = "";
    public const string CheckOut = "";
    public const string Return = "";
    public const string Loans = "";
    public const string Holds = "";
    public const string Fines = "";
    public const string Members = "";
    public const string Book = "";
    public const string Reservations = "";
    public const string Reports = "";
    public const string Accounts = "";
    public const string Settings = "";
    public const string Account = "";
    public const string Password = "";
    public const string SignOut = "";
    public const string Menu = "";
    public const string Calendar = "";
    public const string Backup = "";

    // Actions
    public const string Search = "";
    public const string Add = "";
    public const string Edit = "";
    public const string Delete = "";
    public const string Close = "";
    public const string Check = "";
    public const string Refresh = "";
    public const string Filter = "";
    public const string Save = "";
    public const string Download = "";
    public const string Lock = "";
    public const string Unlock = "";
    public const string Show = "";
    public const string Hide = "";
    public const string ChevronLeft = "";
    public const string ChevronRight = "";
    public const string ChevronUp = "";
    public const string ChevronDown = "";

    // Feedback
    public const string Info = "";
    public const string Warning = "";
    public const string Error = "";
    public const string Success = "";

    // Written as escapes so the private-use characters stay readable in diffs.
    public const string DarkMode = "\uE708";   // moon
    public const string LightMode = "\uE706";  // sun
    public const string Home = "\uE80F";
    public const string GridView = "\uF0E2";
    public const string ListView = "\uEA37";
}
