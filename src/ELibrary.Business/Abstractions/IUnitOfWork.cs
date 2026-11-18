using ELibrary.Domain.Entities;

namespace ELibrary.Business.Abstractions;

/// <summary>
/// One business transaction. A service creates a unit of work per use case, makes its changes and
/// calls <see cref="SaveChanges"/> once, so that the whole use case is stored atomically (NFR-07).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<Role> Roles { get; }
    IRepository<UserAccount> UserAccounts { get; }
    IRepository<MembershipType> MembershipTypes { get; }
    IRepository<Member> Members { get; }
    IRepository<Publisher> Publishers { get; }
    IRepository<Category> Categories { get; }
    IRepository<Author> Authors { get; }
    IRepository<Title> Titles { get; }
    IRepository<TitleAuthor> TitleAuthors { get; }
    IRepository<EResource> EResources { get; }
    IRepository<Copy> Copies { get; }
    IRepository<Loan> Loans { get; }
    IRepository<Reservation> Reservations { get; }
    IRepository<Fine> Fines { get; }
    IRepository<Payment> Payments { get; }
    IRepository<Holiday> Holidays { get; }
    IRepository<FinePolicy> FinePolicies { get; }

    /// <summary>Stores all pending changes in one database transaction.</summary>
    void SaveChanges();

    /// <summary>
    /// Runs several <see cref="SaveChanges"/> calls in one transaction (needed when a new row's
    /// generated key is used by the next row, e.g. account → member).
    /// </summary>
    void ExecuteInTransaction(Action action);
}

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
