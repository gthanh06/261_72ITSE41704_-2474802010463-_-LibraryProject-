using ELibrary.Domain.Enums;

namespace ELibrary.Business.Dtos;

public sealed record LoanDto(
    int Id, int MemberId, string MemberCode, string MemberName, string TitleName, string Barcode,
    DateTime LoanDate, DateOnly DueDate, DateTime? ReturnedAt, int RenewalCount,
    bool IsOverdue, int ChargeableOverdueDays, decimal FineToDate);

public sealed record MemberSummaryDto(
    MemberDto Member, IReadOnlyList<LoanDto> OpenLoans, int MaxLoans,
    decimal OutstandingFines, string? BorrowingBlock);

public sealed record ReturnReceiptDto(
    LoanDto Loan, CopyStatus CopyStatus, int OverdueDays, decimal Fine,
    string? HeldForMemberCode, DateOnly? HoldExpiresOn);

public sealed record FineLineDto(
    int LoanId, string TitleName, string Barcode, DateOnly DueDate, int OverdueDays,
    decimal Amount, decimal AmountPaid, bool IsAccruing)
{
    public decimal Balance => Amount - AmountPaid;
}

public sealed record MemberFinesDto(MemberDto Member, IReadOnlyList<FineLineDto> Fines, string? BorrowingBlock)
{
    public decimal RecordedBalance => Fines.Where(f => !f.IsAccruing).Sum(f => f.Balance);
    public decimal AccruingBalance => Fines.Where(f => f.IsAccruing).Sum(f => f.Balance);
    public decimal TotalBalance => RecordedBalance + AccruingBalance;
}

public sealed record PaymentReceiptDto(
    string MemberCode, string MemberName, DateTime PaidAt, string ReceivedBy,
    decimal Amount, decimal RecordedBalance, decimal TotalBalance, string? BorrowingBlock);
