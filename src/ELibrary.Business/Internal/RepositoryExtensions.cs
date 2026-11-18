using ELibrary.Business.Abstractions;
using ELibrary.Domain.Common;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Internal;

internal static class RepositoryExtensions
{
    public static T GetRequired<T>(this IRepository<T> repository, int id, string entityName) where T : Entity =>
        repository.GetById(id) ?? throw new NotFoundException(entityName, id);

    public static Member GetMemberByCode(this IUnitOfWork uow, string? memberCode)
    {
        var code = Guard.Required(memberCode, "Member code", 20);
        return uow.Members.Query().FirstOrDefault(m => m.MemberCode == code)
               ?? throw new NotFoundException("Member", code);
    }

    public static Copy GetCopyByBarcode(this IUnitOfWork uow, string? barcode)
    {
        var code = Guard.Required(barcode, "Barcode", 30);
        return uow.Copies.Query().FirstOrDefault(c => c.Barcode == code)
               ?? throw new NotFoundException("Copy with barcode", code);
    }
}
