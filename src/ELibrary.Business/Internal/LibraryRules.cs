using ELibrary.Business.Abstractions;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Exceptions;
using ELibrary.Domain.Services;

namespace ELibrary.Business.Internal;

internal sealed record LibraryRules(FinePolicy Policy, LibraryCalendar Calendar)
{
    public static LibraryRules Load(IUnitOfWork uow)
    {
        var policy = uow.FinePolicies.Query().SingleOrDefault()
            ?? throw new BusinessRuleException(BusinessRuleException.StateRule, "The fine policy has not been configured.");
        var dates = uow.Holidays.Query().Select(h => h.Date).ToList();
        return new LibraryRules(policy, new LibraryCalendar(dates, policy.ClosedOnSundays));
    }
}
