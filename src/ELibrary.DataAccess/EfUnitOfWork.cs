using System.Data;
using ELibrary.Business.Abstractions;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ELibrary.DataAccess;

/// <summary>
/// Unit of work over one short-lived <see cref="LibraryDbContext"/>. <see cref="SaveChanges"/> writes
/// all pending changes in one transaction (NFR-07) and turns database errors into
/// <see cref="DataAccessException"/> so that the upper layers do not depend on EF Core.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int ConstraintViolation = 547;
    private const int DeadlockVictim = 1205;

    private readonly LibraryDbContext _context;

    public EfUnitOfWork(LibraryDbContext context)
    {
        _context = context;
        Roles = new EfRepository<Role>(context);
        UserAccounts = new EfRepository<UserAccount>(context);
        MembershipTypes = new EfRepository<MembershipType>(context);
        Members = new EfRepository<Member>(context);
        Publishers = new EfRepository<Publisher>(context);
        Categories = new EfRepository<Category>(context);
        Authors = new EfRepository<Author>(context);
        Titles = new EfRepository<Title>(context);
        TitleAuthors = new EfRepository<TitleAuthor>(context);
        EResources = new EfRepository<EResource>(context);
        Copies = new EfRepository<Copy>(context);
        Loans = new EfRepository<Loan>(context);
        Reservations = new EfRepository<Reservation>(context);
        Fines = new EfRepository<Fine>(context);
        Payments = new EfRepository<Payment>(context);
        Holidays = new EfRepository<Holiday>(context);
        FinePolicies = new EfRepository<FinePolicy>(context);
    }

    public IRepository<Role> Roles { get; }
    public IRepository<UserAccount> UserAccounts { get; }
    public IRepository<MembershipType> MembershipTypes { get; }
    public IRepository<Member> Members { get; }
    public IRepository<Publisher> Publishers { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<Author> Authors { get; }
    public IRepository<Title> Titles { get; }
    public IRepository<TitleAuthor> TitleAuthors { get; }
    public IRepository<EResource> EResources { get; }
    public IRepository<Copy> Copies { get; }
    public IRepository<Loan> Loans { get; }
    public IRepository<Reservation> Reservations { get; }
    public IRepository<Fine> Fines { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<Holiday> Holidays { get; }
    public IRepository<FinePolicy> FinePolicies { get; }

    public void SaveChanges()
    {
        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            throw Translate(ex);
        }
    }

    public void ExecuteInTransaction(Action action)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            action();
            return;
        }

        try
        {
            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);
            action();
            transaction.Commit();
        }
        catch (SqlException ex) when (ex.Number == DeadlockVictim)
        {
            throw new DataAccessException("Another user changed the same data. Please refresh and try again.", ex);
        }
    }

    public void Dispose() => _context.Dispose();

    private static DataAccessException Translate(DbUpdateException ex)
    {
        var sqlError = ex.InnerException as SqlException;
        var message = sqlError?.Number switch
        {
            UniqueIndexViolation or UniqueConstraintViolation =>
                "The change conflicts with existing data (a duplicate value). Another user may have changed the same record — please refresh and try again.",
            DeadlockVictim =>
                "Another user changed the same data. Please refresh and try again.",
            ConstraintViolation =>
                "The change was rejected by a database rule (it refers to missing data or breaks a constraint).",
            _ => "The data could not be saved to the database."
        };
        return new DataAccessException(message, ex);
    }
}

public sealed class EfUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly DbContextOptions<LibraryDbContext> _options;

    public EfUnitOfWorkFactory(string connectionString) =>
        _options = LibraryDbContext.CreateOptions(connectionString);

    public IUnitOfWork Create() => new EfUnitOfWork(new LibraryDbContext(_options));
}
