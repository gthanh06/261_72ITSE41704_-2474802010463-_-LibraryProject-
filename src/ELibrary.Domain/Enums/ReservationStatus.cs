namespace ELibrary.Domain.Enums;

/// <summary>Waiting and OnHold are the "active" states (see the filtered unique index on Reservations).</summary>
public enum ReservationStatus
{
    Waiting = 1,
    OnHold = 2,
    Fulfilled = 3,
    Cancelled = 4,
    Expired = 5
}
