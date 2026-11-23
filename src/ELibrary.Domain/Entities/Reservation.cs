using ELibrary.Domain.Common;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

public sealed class Reservation : Entity
{
    private Reservation() { }

    public Reservation(int titleId, int memberId, DateTime reservedAt)
    {
        TitleId = titleId;
        MemberId = memberId;
        ReservedAt = reservedAt;
        Status = ReservationStatus.Waiting;
    }

    public int TitleId { get; private set; }
    public int MemberId { get; private set; }
    public DateTime ReservedAt { get; private set; }
    public ReservationStatus Status { get; private set; }
    public int? HeldCopyId { get; private set; }
    public DateOnly? HoldExpiresOn { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public bool IsActive => Status is ReservationStatus.Waiting or ReservationStatus.OnHold;

    public void PlaceHold(int copyId, DateOnly expiresOn)
    {
        EnsureState(Status == ReservationStatus.Waiting);
        HeldCopyId = copyId;
        HoldExpiresOn = expiresOn;
        Status = ReservationStatus.OnHold;
    }

    public bool IsHeldFor(int copyId) => Status == ReservationStatus.OnHold && HeldCopyId == copyId;

    public bool IsHoldExpired(DateOnly today) => Status == ReservationStatus.OnHold && HoldExpiresOn < today;

    public void Fulfil(DateTime now)
    {
        EnsureState(Status == ReservationStatus.OnHold && !IsHoldExpired(DateOnly.FromDateTime(now)));
        Close(ReservationStatus.Fulfilled, now);
    }

    public void Cancel(DateTime now)
    {
        EnsureState(IsActive);
        Close(ReservationStatus.Cancelled, now);
    }

    public void Expire(DateTime now)
    {
        EnsureState(IsHoldExpired(DateOnly.FromDateTime(now)));
        Close(ReservationStatus.Expired, now);
    }

    private void Close(ReservationStatus status, DateTime now)
    {
        Status = status;
        ClosedAt = now;
    }

    private static void EnsureState(bool valid)
    {
        if (!valid)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule, "The reservation is no longer in the expected state.");
        }
    }
}
