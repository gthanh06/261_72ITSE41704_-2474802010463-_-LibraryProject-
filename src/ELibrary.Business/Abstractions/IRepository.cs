using ELibrary.Domain.Common;

namespace ELibrary.Business.Abstractions;

/// <summary>
/// Repository of one entity type. Implemented with EF Core in the data-access layer and with an
/// in-memory list in the unit tests. Entities returned by <see cref="Query"/> are tracked: changes
/// made through their methods are stored by <see cref="IUnitOfWork.SaveChanges"/>.
/// </summary>
public interface IRepository<T> where T : Entity
{
    IQueryable<T> Query();

    T? GetById(int id);

    void Add(T entity);

    void Remove(T entity);
}
