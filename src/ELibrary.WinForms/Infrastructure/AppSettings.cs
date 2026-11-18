using System.Text.Json;

namespace ELibrary.WinForms.Infrastructure;

/// <summary>Reads appsettings.json next to the executable.</summary>
internal sealed class AppSettings
{
    public const string FileName = "appsettings.json";

    private AppSettings(string connectionString) => ConnectionString = connectionString;

    public string ConnectionString { get; }

    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The configuration file {path} is missing.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var connectionString = document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("ELibrary")
            .GetString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"ConnectionStrings:ELibrary is empty in {path}.");
        }

        return new AppSettings(connectionString);
    }
}
