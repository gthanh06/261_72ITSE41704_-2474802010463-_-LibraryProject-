using ELibrary.Business.Abstractions;
using ELibrary.Domain.Common;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Internal;

internal static class RepositoryExtensions
{
    public static T InTransaction<T>(this IUnitOfWork uow, Func<T> action)
    {
        T result = default!;
        uow.ExecuteInTransaction(() => { result = action(); });
        return result;
    }

    public static T GetRequired<T>(this IRepository<T> repository, int id, string entityName) where T : Entity =>
        repository.GetById(id) ?? throw new NotFoundException(entityName, id);

    public static Member GetMemberByCode(this IUnitOfWork uow, string? memberCode)
    {
        var code = Guard.Code(memberCode, "Member code", 20).ToUpperInvariant();
        return uow.Members.Query().FirstOrDefault(m => m.MemberCode.ToUpper() == code)
               ?? throw new NotFoundException("Member", code);
    }

    public static Copy GetCopyByBarcode(this IUnitOfWork uow, string? barcode)
    {
        var code = Guard.Code(barcode, "Barcode", 30).ToUpperInvariant();
        return uow.Copies.Query().FirstOrDefault(c => c.Barcode.ToUpper() == code)
               ?? throw new NotFoundException("Copy with barcode", code);
    }
}
