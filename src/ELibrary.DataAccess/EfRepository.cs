using ELibrary.Business.Abstractions;
using ELibrary.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ELibrary.DataAccess;

/// <summary>Repository over a DbSet. Queries are translated to parameterised SQL by EF Core (NFR-06).</summary>
public sealed class EfRepository<T> : IRepository<T> where T : Entity
{
    private readonly DbSet<T> _set;

    public EfRepository(LibraryDbContext context) => _set = context.Set<T>();

    public IQueryable<T> Query() => _set;

    public T? GetById(int id) => _set.Find(id);

    public void Add(T entity) => _set.Add(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
