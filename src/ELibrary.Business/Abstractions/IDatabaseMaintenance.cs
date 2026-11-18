namespace ELibrary.Business.Abstractions;

/// <summary>Full database backup and restore (NFR-08). Implemented for SQL Server in the data-access layer.</summary>
public interface IDatabaseMaintenance
{
    void Backup(string backupFilePath);

    void Restore(string backupFilePath);
}
