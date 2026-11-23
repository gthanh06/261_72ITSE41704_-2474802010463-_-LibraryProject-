using ELibrary.Business.Abstractions;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;

namespace ELibrary.Business.Internal;

/// <summary>Shared by returns, catalogue and reservation workflows. The caller owns the transaction.</summary>
internal sealed class HoldAllocator(IUnitOfWork uow, LibraryRules rules)
{
    public Reservation? AssignCopy(Copy copy, DateOnly today)
    {
        var next = uow.Reservations.Query()
            .Where(r => r.TitleId == copy.TitleId && r.Status == ReservationStatus.Waiting)
            .OrderBy(r => r.ReservedAt).ThenBy(r => r.Id)
            .AsEnumerable().FirstOrDefault(r => r.Status == ReservationStatus.Waiting);
        if (next != null)
        {
            next.PlaceHold(copy.Id, rules.Calendar.AddOpeningDays(today, rules.Policy.HoldPeriodDays));
            copy.PlaceOnHold();
        }

        return next;
    }
}
