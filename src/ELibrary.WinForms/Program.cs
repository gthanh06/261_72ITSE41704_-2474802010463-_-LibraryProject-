using ELibrary.Business.Security;
using ELibrary.DataAccess.Seeding;
using ELibrary.WinForms.Forms;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms;

internal static class Program
{
    /// <summary>
    /// Start-up: read the configuration, make sure the database exists (creating and seeding it on the
    /// first run), expire uncollected holds (UC11), then alternate between the login and main windows.
    /// Command line: <c>ELibrary.exe --export-schema [file]</c> writes the SQL CREATE script and exits.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(Theme.Fonts.Body);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ErrorHandler.Show(null, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ErrorLog.Write(e.ExceptionObject as Exception);

        AppSettings settings;
        try
        {
            settings = AppSettings.Load();
        }
        catch (Exception ex)
        {
            MessageDialog.Inform(null, "Configuration error", ex.Message, Tone.Danger);
            return;
        }

        if (args.Length > 0 && args[0] == "--export-schema")
        {
            ExportSchema(settings.ConnectionString, args.Length > 1 ? args[1] : "create_database.sql");
            return;
        }

        if (!DatabaseInitializer.CanConnect(settings.ConnectionString))
        {
            MessageDialog.Inform(null, "Database not available",
                "SQL Server cannot be reached.\n\nCheck that SQL Server (Express) is running and that the connection string in " +
                $"{AppSettings.FileName} is correct:\n\n{settings.ConnectionString}", Tone.Danger);
            return;
        }

        AppServices app;
        try
        {
            using (new WaitCursorScope())
            {
                DatabaseInitializer.EnsureCreatedAndSeeded(settings.ConnectionString, new Pbkdf2PasswordHasher());
            }

            app = new AppServices(settings.ConnectionString);
            app.Reservations.ExpireHoldsAtStartup();
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            MessageDialog.Inform(null, "Start-up error",
                $"The database could not be prepared: {ex.Message}\n\nDetails are in {ErrorLog.Folder}.", Tone.Danger);
            return;
        }

        while (true)
        {
            using (var login = new LoginForm(app))
            {
                if (login.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
            }

            using var main = new MainForm(app);
            Application.Run(main);
            if (!main.LoggedOut)
            {
                return;
            }
        }
    }

    private static void ExportSchema(string connectionString, string path)
    {
        var script = DatabaseInitializer.GenerateCreateScript(connectionString);
        File.WriteAllText(path, script);
        MessageDialog.Inform(null, "Script exported", $"The database script was written to {Path.GetFullPath(path)}.", Tone.Success);
    }

    private sealed class WaitCursorScope : IDisposable
    {
        public WaitCursorScope() => Cursor.Current = Cursors.WaitCursor;

        public void Dispose() => Cursor.Current = Cursors.Default;
    }
}
