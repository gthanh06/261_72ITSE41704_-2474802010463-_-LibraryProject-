using ELibrary.Business.Abstractions;
using ELibrary.Domain.Entities;

namespace ELibrary.Business.Internal;

internal sealed class FineLedger(IUnitOfWork uow, LibraryRules rules)
{
    public (int Days, decimal Amount) Calculate(Loan loan, DateOnly end)
    {
        var days = rules.Calendar.CountChargeableDays(loan.DueDate, end);
        return (days, rules.Policy.CalculateFine(days));
    }

    public decimal GetFineToDate(Loan loan, DateOnly today) =>
        Calculate(loan, loan.ReturnedAt.HasValue ? DateOnly.FromDateTime(loan.ReturnedAt.Value) : today).Amount;

    public IQueryable<Fine> UnpaidFines(int memberId) =>
        uow.Fines.Query().Where(f => f.MemberId == memberId && f.AmountPaid < f.Amount);

    public IQueryable<Loan> OpenOverdueLoans(int memberId, DateOnly today) =>
        uow.Loans.Query().Where(l => l.MemberId == memberId && l.ReturnedAt == null && l.DueDate < today);

    public decimal GetOutstanding(int memberId, DateOnly today) =>
        (UnpaidFines(memberId).Sum(f => (decimal?)(f.Amount - f.AmountPaid)) ?? 0)
        + OpenOverdueLoans(memberId, today).AsEnumerable().Sum(l => GetFineToDate(l, today));
}
