using ELibrary.Business.Abstractions;
using ELibrary.Domain.Common;
using ELibrary.Domain.Entities;

namespace ELibrary.Tests.Support;

internal sealed class MemoryLibrary : IUnitOfWork, IUnitOfWorkFactory
{
    public IRepository<Role> Roles { get; } = new MemoryRepository<Role>();
    public IRepository<UserAccount> UserAccounts { get; } = new MemoryRepository<UserAccount>();
    public IRepository<MembershipType> MembershipTypes { get; } = new MemoryRepository<MembershipType>();
    public IRepository<Member> Members { get; } = new MemoryRepository<Member>();
    public IRepository<Publisher> Publishers { get; } = new MemoryRepository<Publisher>();
    public IRepository<Category> Categories { get; } = new MemoryRepository<Category>();
    public IRepository<Author> Authors { get; } = new MemoryRepository<Author>();
    public IRepository<Title> Titles { get; } = new MemoryRepository<Title>();
    public IRepository<TitleAuthor> TitleAuthors { get; } = new MemoryRepository<TitleAuthor>();
    public IRepository<EResource> EResources { get; } = new MemoryRepository<EResource>();
    public IRepository<Copy> Copies { get; } = new MemoryRepository<Copy>();
    public IRepository<Loan> Loans { get; } = new MemoryRepository<Loan>();
    public IRepository<Reservation> Reservations { get; } = new MemoryRepository<Reservation>();
    public IRepository<Fine> Fines { get; } = new MemoryRepository<Fine>();
    public IRepository<Payment> Payments { get; } = new MemoryRepository<Payment>();
    public IRepository<Holiday> Holidays { get; } = new MemoryRepository<Holiday>();
    public IRepository<FinePolicy> FinePolicies { get; } = new MemoryRepository<FinePolicy>();

    public IUnitOfWork Create() => this;
    public void ExecuteInTransaction(Action action) => action();
    public void SaveChanges() { }
    public void Dispose() { }

    private sealed class MemoryRepository<T> : IRepository<T> where T : Entity
    {
        private readonly List<T> _rows = [];
        private int _nextId = 1;

        public IQueryable<T> Query() => _rows.AsQueryable();
        public T? GetById(int id) => _rows.SingleOrDefault(row => row.Id == id);

        public void Add(T entity)
        {
            if (entity.Id == 0)
            {
                typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, _nextId++);
            }

            _rows.Add(entity);
        }

        public void Remove(T entity) => _rows.Remove(entity);
    }
}

internal sealed class FixedClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 11, 2, 10, 0, 0);

    public DateOnly Today => DateOnly.FromDateTime(Now);
}
