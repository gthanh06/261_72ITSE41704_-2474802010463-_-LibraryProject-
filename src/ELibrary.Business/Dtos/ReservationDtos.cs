using ELibrary.Domain.Enums;

namespace ELibrary.Business.Dtos;

/// <summary>
/// A reservation as shown to members and librarians. <see cref="QueuePosition"/> is 1 for the next
/// member to be served and 0 when the reservation is not waiting; <see cref="HeldBarcode"/> and
/// <see cref="HoldExpiresOn"/> are set while a copy waits on the hold shelf.
/// </summary>
public sealed record ReservationDto(
    int Id, int TitleId, string TitleName, int MemberId, string MemberCode, string MemberName,
    DateTime ReservedAt, ReservationStatus Status, int QueuePosition,
    string? HeldBarcode, DateOnly? HoldExpiresOn, DateTime? ClosedAt)
{
    public bool IsActive => Status is ReservationStatus.Waiting or ReservationStatus.OnHold;
}

/// <summary>What processing the hold shelf did: holds that expired and copies passed to the next member.</summary>
public sealed record HoldProcessingResultDto(int ExpiredHolds, int CopiesReassigned, int CopiesReleased);
