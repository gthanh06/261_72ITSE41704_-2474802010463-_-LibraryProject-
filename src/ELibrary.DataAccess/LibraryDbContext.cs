using ELibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ELibrary.DataAccess;

/// <summary>EF Core context of the E-Library database. Mappings are in the Configurations folder.</summary>
public sealed class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<MembershipType> MembershipTypes => Set<MembershipType>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<TitleAuthor> TitleAuthors => Set<TitleAuthor>();
    public DbSet<EResource> EResources => Set<EResource>();
    public DbSet<Copy> Copies => Set<Copy>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Fine> Fines => Set<Fine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<FinePolicy> FinePolicies => Set<FinePolicy>();

    public static DbContextOptions<LibraryDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(30))
            .Options;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Amounts in VND.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);
}
