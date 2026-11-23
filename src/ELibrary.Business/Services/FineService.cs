using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Internal;
using ELibrary.Domain.Common;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>UC12: show outstanding and accruing fines, and accept payments for recorded fines.</summary>
public sealed class FineService(IUnitOfWorkFactory factory, IClock clock, IUserSession session)
{
    public MemberFinesDto GetMemberFines(string memberCode)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        return Describe(uow, LibraryRules.Load(uow), uow.GetMemberByCode(memberCode), clock.Today);
    }

    public MemberFinesDto GetMyFines()
    {
        var memberId = session.Require(RoleCode.Member).RequireMemberId();
        using var uow = factory.Create();
        return Describe(uow, LibraryRules.Load(uow), uow.Members.GetRequired(memberId, "Member"), clock.Today);
    }

    public PaymentReceiptDto RecordPayment(string memberCode, decimal amount)
    {
        var user = session.Require(RoleCode.Librarian);
        Guard.Positive(amount, "Payment");
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ValidationException("Payment", "Enter a payment amount with at most two decimal places.");
        }

        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var member = uow.GetMemberByCode(memberCode);
            var rules = LibraryRules.Load(uow);
            var ledger = new FineLedger(uow, rules);
            var fines = ledger.UnpaidFines(member.Id).OrderBy(f => f.CreatedAt).ThenBy(f => f.Id).ToList();
            var recorded = fines.Sum(f => f.Balance);
            if (amount > recorded)
            {
                throw new ValidationException("Payment", $"The payment exceeds the recorded balance of {Money.Format(recorded)}.");
            }

            var total = ledger.GetOutstanding(member.Id, today);
            var remaining = amount;
            foreach (var fine in fines)
            {
                var applied = Math.Min(remaining, fine.Balance);
                uow.Payments.Add(fine.ApplyPayment(applied, now, user.AccountId));
                remaining -= applied;
                if (remaining == 0)
                {
                    break;
                }
            }

            uow.SaveChanges();
            return new PaymentReceiptDto(member.MemberCode, member.FullName, now, user.FullName, amount,
                recorded - amount, total - amount, member.GetBorrowingBlock(today, total - amount, rules.Policy));
        });
    }

    private static MemberFinesDto Describe(IUnitOfWork uow, LibraryRules rules, Member member, DateOnly today)
    {
        var type = uow.MembershipTypes.GetRequired(member.MembershipTypeId, "Membership type");
        var fines = uow.Fines.Query().Where(f => f.MemberId == member.Id).ToDictionary(f => f.LoanId);
        var fineLoanIds = fines.Keys.ToArray();
        var loans = uow.Loans.Query().Where(l => l.MemberId == member.Id &&
            (fineLoanIds.Contains(l.Id) || (l.ReturnedAt == null && l.DueDate < today)))
            .OrderBy(l => l.DueDate).ThenBy(l => l.Id).ToList();

        var lines = new List<FineLineDto>();
        foreach (var loan in new DtoProjector(uow, rules, today).ToLoanDtos(loans))
        {
            if (fines.TryGetValue(loan.Id, out var fine))
            {
                lines.Add(new FineLineDto(loan.Id, loan.TitleName, loan.Barcode, loan.DueDate,
                    fine.OverdueDays, fine.Amount, fine.AmountPaid, IsAccruing: false));
            }
            else if (loan.FineToDate > 0)
            {
                lines.Add(new FineLineDto(loan.Id, loan.TitleName, loan.Barcode, loan.DueDate,
                    loan.ChargeableOverdueDays, loan.FineToDate, 0, IsAccruing: true));
            }
        }

        var total = lines.Sum(f => f.Balance);
        return new MemberFinesDto(DtoProjector.ToMemberDto(member, type), lines, member.GetBorrowingBlock(today, total, rules.Policy));
    }
}
