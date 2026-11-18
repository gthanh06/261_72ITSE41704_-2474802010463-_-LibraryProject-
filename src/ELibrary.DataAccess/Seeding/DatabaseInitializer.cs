using ELibrary.Business.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ELibrary.DataAccess.Seeding;

public static class DatabaseInitializer
{
    /// <summary>Returns false (instead of throwing) when SQL Server cannot be reached.</summary>
    public static bool CanConnect(string connectionString)
    {
        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", ConnectTimeout = 5 };
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the database and its tables if they do not exist, then fills it with sample data.
    /// Returns true when the database was created by this call.
    /// </summary>
    public static bool EnsureCreatedAndSeeded(string connectionString, IPasswordHasher hasher)
    {
        using (var context = new LibraryDbContext(LibraryDbContext.CreateOptions(connectionString)))
        {
            if (!context.Database.EnsureCreated())
            {
                // The database already existed: create the tables if it is empty, otherwise leave it alone.
                var creator = context.Database.GetService<IRelationalDatabaseCreator>();
                if (creator.HasTables())
                {
                    return false;
                }

                creator.CreateTables();
            }
        }

        try
        {
            new SampleDataSeeder(new EfUnitOfWorkFactory(connectionString), hasher,
                new SqlServerDatabaseMaintenance(connectionString), DateTime.Now).Seed();
        }
        catch
        {
            // Never leave a half-seeded database behind: drop it so that the next start tries again.
            using var context = new LibraryDbContext(LibraryDbContext.CreateOptions(connectionString));
            context.Database.EnsureDeleted();
            throw;
        }

        return true;
    }

    /// <summary>The CREATE TABLE script that EF Core generates from the mappings (for the report and for DBAs).</summary>
    public static string GenerateCreateScript(string connectionString)
    {
        using var context = new LibraryDbContext(LibraryDbContext.CreateOptions(connectionString));
        return context.Database.GenerateCreateScript();
    }
}
