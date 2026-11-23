using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Internal;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>
/// UC10 and UC11: members reserve titles that have no copy on the shelf and join a first-come,
/// first-served queue (BR4). A returned copy is held for the first member in the queue for a few
/// opening days (BR5); a hold that is not collected in time expires and the copy goes to the next member.
/// </summary>
public sealed class ReservationService(IUnitOfWorkFactory factory, IClock clock, IUserSession session)
{
    public ReservationDto Place(int titleId)
    {
        var memberId = session.Require(RoleCode.Member).RequireMemberId();
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var title = uow.Titles.GetRequired(titleId, "Title");
            if (!title.IsActive)
            {
                throw new BusinessRuleException(BusinessRuleException.StateRule, "This title is no longer available.");
            }

            var member = uow.Members.GetRequired(memberId, "Member");
            var rules = LibraryRules.Load(uow);
            member.EnsureCanBorrow(today, new FineLedger(uow, rules).GetOutstanding(member.Id, today), rules.Policy);

            var copies = uow.Copies.Query().Where(c => c.TitleId == titleId).Select(c => c.Status).ToList();
            if (copies.Contains(CopyStatus.Available))
            {
                throw new BusinessRuleException("BR4", "A copy of this title is on the shelf. Borrow it at the desk instead.");
            }

            if (!copies.Any(s => s is CopyStatus.OnLoan or CopyStatus.OnHold or CopyStatus.UnderRepair))
            {
                throw new BusinessRuleException("BR4", "This title has no copy that can be reserved.");
            }

            if (uow.Reservations.Query().Any(r => r.TitleId == titleId && r.MemberId == memberId
                    && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.OnHold)))
            {
                throw new BusinessRuleException("BR4", "You already have an active reservation for this title.");
            }

            var reservation = new Reservation(titleId, memberId, now);
            uow.Reservations.Add(reservation);
            uow.SaveChanges();
            return Project(uow, [reservation]).Single();
        });
    }

    /// <summary>A member cancels his/her own reservation; a librarian may cancel any. A held copy goes to the next member.</summary>
    public void Cancel(int reservationId)
    {
        var user = session.Require(RoleCode.Member, RoleCode.Librarian);
        var now = clock.Now;
        using var uow = factory.Create();
        uow.ExecuteInTransaction(() =>
        {
            var reservation = uow.Reservations.GetRequired(reservationId, "Reservation");
            user.EnsureCanActForMember(reservation.MemberId);
            if (!reservation.IsActive)
            {
                throw new BusinessRuleException(BusinessRuleException.StateRule, "This reservation is already closed.");
            }

            var heldCopyId = reservation.Status == ReservationStatus.OnHold ? reservation.HeldCopyId : null;
            reservation.Cancel(now);
            if (heldCopyId is int copyId)
            {
                PassOn(uow, LibraryRules.Load(uow), uow.Copies.GetRequired(copyId, "Copy"), DateOnly.FromDateTime(now));
            }

            uow.SaveChanges();
        });
    }

    /// <summary>The member's active reservations first (queue order), then the closed ones, newest first.</summary>
    public IReadOnlyList<ReservationDto> GetMyReservations()
    {
        var memberId = session.Require(RoleCode.Member).RequireMemberId();
        using var uow = factory.Create();
        var rows = uow.Reservations.Query().Where(r => r.MemberId == memberId).ToList();
        return Project(uow, rows)
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.IsActive ? r.ReservedAt : DateTime.MaxValue)
            .ThenByDescending(r => r.ClosedAt ?? r.ReservedAt)
            .ToList();
    }

    /// <summary>
    /// Copies waiting on the hold shelf (oldest expiry first). With <paramref name="includeWaiting"/>, the
    /// members still waiting in the queues are listed after them.
    /// </summary>
    public IReadOnlyList<ReservationDto> GetActiveHolds(bool includeWaiting = false)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        var rows = uow.Reservations.Query()
            .Where(r => r.Status == ReservationStatus.OnHold || (includeWaiting && r.Status == ReservationStatus.Waiting))
            .ToList();
        return Project(uow, rows)
            .OrderBy(r => r.Status == ReservationStatus.OnHold ? 0 : 1)
            .ThenBy(r => r.HoldExpiresOn ?? DateOnly.MaxValue)
            .ThenBy(r => r.TitleName).ThenBy(r => r.QueuePosition)
            .ToList();
    }

    /// <summary>UC11: expires the holds that were not collected in time and offers the copies to the next members.</summary>
    public HoldProcessingResultDto ProcessHolds()
    {
        session.Require(RoleCode.Librarian);
        return ExpireHolds();
    }

    /// <summary>Run by the application when it starts (BR5), before anyone has logged in.</summary>
    public HoldProcessingResultDto ExpireHoldsAtStartup() => ExpireHolds();

    private HoldProcessingResultDto ExpireHolds()
    {
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var expired = uow.Reservations.Query()
                .Where(r => r.Status == ReservationStatus.OnHold && r.HoldExpiresOn < today)
                .OrderBy(r => r.HoldExpiresOn).ThenBy(r => r.Id)
                .ToList();
            if (expired.Count == 0)
            {
                return new HoldProcessingResultDto(0, 0, 0);
            }

            var rules = LibraryRules.Load(uow);
            int reassigned = 0, released = 0;
            foreach (var hold in expired)
            {
                var copyId = hold.HeldCopyId;
                hold.Expire(now);
                if (copyId is int id)
                {
                    if (PassOn(uow, rules, uow.Copies.GetRequired(id, "Copy"), today))
                    {
                        reassigned++;
                    }
                    else
                    {
                        released++;
                    }
                }
            }

            uow.SaveChanges();
            return new HoldProcessingResultDto(expired.Count, reassigned, released);
        });
    }

    /// <summary>Takes a copy off the hold shelf and holds it for the next waiting member, if any. True when it was reassigned.</summary>
    private static bool PassOn(IUnitOfWork uow, LibraryRules rules, Copy copy, DateOnly today)
    {
        copy.ReleaseHold();
        return new HoldAllocator(uow, rules).AssignCopy(copy, today) != null;
    }

    private static IReadOnlyList<ReservationDto> Project(IUnitOfWork uow, IReadOnlyList<Reservation> reservations)
    {
        if (reservations.Count == 0)
        {
            return Array.Empty<ReservationDto>();
        }

        var titleIds = reservations.Select(r => r.TitleId).Distinct().ToArray();
        var titles = uow.Titles.Query().Where(t => titleIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Name);
        var memberIds = reservations.Select(r => r.MemberId).Distinct().ToArray();
        var members = uow.Members.Query().Where(m => memberIds.Contains(m.Id)).ToDictionary(m => m.Id);
        var copyIds = reservations.Where(r => r.HeldCopyId.HasValue).Select(r => r.HeldCopyId!.Value).Distinct().ToArray();
        var barcodes = uow.Copies.Query().Where(c => copyIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Barcode);

        // Queue position = place among the Waiting reservations of the same title, oldest first (BR4).
        var queues = uow.Reservations.Query()
            .Where(r => titleIds.Contains(r.TitleId) && r.Status == ReservationStatus.Waiting)
            .OrderBy(r => r.ReservedAt).ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.TitleId })
            .AsEnumerable()
            .GroupBy(r => r.TitleId)
            .SelectMany(g => g.Select((r, index) => (r.Id, Position: index + 1)))
            .ToDictionary(x => x.Id, x => x.Position);

        return reservations.Select(r =>
        {
            var member = members[r.MemberId];
            return new ReservationDto(r.Id, r.TitleId, titles[r.TitleId], member.Id, member.MemberCode, member.FullName,
                r.ReservedAt, r.Status, queues.GetValueOrDefault(r.Id),
                r.Status == ReservationStatus.OnHold && r.HeldCopyId is int copyId ? barcodes[copyId] : null,
                r.Status == ReservationStatus.OnHold ? r.HoldExpiresOn : null, r.ClosedAt);
        }).ToList();
    }
}
