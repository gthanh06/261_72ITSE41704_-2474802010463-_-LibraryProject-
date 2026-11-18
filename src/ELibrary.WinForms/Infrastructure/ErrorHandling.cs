using System.Data.Common;
using ELibrary.Domain.Exceptions;

namespace ELibrary.WinForms.Infrastructure;

/// <summary>
/// One place that turns exceptions into messages. Expected errors (<see cref="LibraryException"/>)
/// are shown as they are; database and unexpected errors are logged and shown in plain words.
/// </summary>
internal static class ErrorHandler
{
    public static string Describe(Exception ex)
    {
        if (ex is LibraryException expected)
        {
            return expected.Message;
        }

        return FindDatabaseError(ex) != null
            ? "The database cannot be reached. Check that SQL Server is running, then try again."
            : "An unexpected error occurred. The details were written to the error log.";
    }

    public static void Show(IWin32Window? owner, Exception ex)
    {
        var isUserError = ex is LibraryException and not DataAccessException;
        if (!isUserError)
        {
            ErrorLog.Write(ex);
        }

        MessageBox.Show(owner, Describe(ex), isUserError ? "E-Library" : "E-Library – Error",
            MessageBoxButtons.OK, isUserError ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
    }

    private static DbException? FindDatabaseError(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is DbException db)
            {
                return db;
            }

            ex = ex.InnerException;
        }

        return null;
    }
}

/// <summary>Appends unexpected errors to %LOCALAPPDATA%\ELibrary\logs. Never logs passwords (NFR-03).</summary>
internal static class ErrorLog
{
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELibrary", "logs");

    public static void Write(Exception? ex)
    {
        if (ex == null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Folder);
            var file = Path.Combine(Folder, $"error-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never crash the application.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
