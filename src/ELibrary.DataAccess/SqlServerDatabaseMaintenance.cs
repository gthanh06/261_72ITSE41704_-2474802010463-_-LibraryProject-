using ELibrary.Business.Abstractions;
using ELibrary.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace ELibrary.DataAccess;

/// <summary>
/// NFR-08: full backup (.bak) and restore of the SQL Server database. The file path is a path on the
/// SQL Server machine, and the SQL Server service account must be allowed to write to that folder.
/// </summary>
public sealed class SqlServerDatabaseMaintenance : IDatabaseMaintenance
{
    private const int TimeoutSeconds = 600;

    private readonly string _connectionString;

    public SqlServerDatabaseMaintenance(string connectionString) => _connectionString = connectionString;

    private string DatabaseName => new SqlConnectionStringBuilder(_connectionString).InitialCatalog;

    public void Backup(string backupFilePath)
    {
        Execute(_connectionString,
            "BACKUP DATABASE @database TO DISK = @path WITH INIT, FORMAT, NAME = N'ELibrary full backup'",
            backupFilePath, "Backup failed");
    }

    public void Restore(string backupFilePath)
    {
        var master = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" }.ConnectionString;
        var quoted = "[" + DatabaseName.Replace("]", "]]") + "]";
        SqlConnection.ClearAllPools(); // close the application's own pooled connections
        Execute(master,
            $"ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            "RESTORE DATABASE @database FROM DISK = @path WITH REPLACE; " +
            $"ALTER DATABASE {quoted} SET MULTI_USER;",
            backupFilePath, "Restore failed");
    }

    private void Execute(string connectionString, string sql, string path, string failure)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection) { CommandTimeout = TimeoutSeconds };
            command.Parameters.AddWithValue("@database", DatabaseName);
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }
        catch (SqlException ex)
        {
            throw new DataAccessException($"{failure}: {ex.Message}", ex);
        }
    }
}
