using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Internal;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>UC07–UC09: checkout, return and renewal.</summary>
public sealed class CirculationService(IUnitOfWorkFactory factory, IClock clock, IUserSession session)
{
    public MemberSummaryDto GetMemberSummary(string memberCode)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        var member = uow.GetMemberByCode(memberCode);
        var type = uow.MembershipTypes.GetRequired(member.MembershipTypeId, "Membership type");
        var today = clock.Today;
        var rules = LibraryRules.Load(uow);
        var loans = ProjectLoans(uow, rules, today, OpenLoans(uow).Where(l => l.MemberId == member.Id));
        var balance = new FineLedger(uow, rules).GetOutstanding(member.Id, today);
        return new MemberSummaryDto(DtoProjector.ToMemberDto(member, type), loans, type.MaxLoans,
            balance, member.GetBorrowingBlock(today, balance, rules.Policy));
    }

    public LoanDto CheckOut(string memberCode, string barcode)
    {
        var user = session.Require(RoleCode.Librarian);
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var member = uow.GetMemberByCode(memberCode);
            var rules = LibraryRules.Load(uow);
            EnsureEligible(uow, rules, member, today);
            var type = uow.MembershipTypes.GetRequired(member.MembershipTypeId, "Membership type");
            type.EnsureBelowLoanLimit(OpenLoans(uow).Count(l => l.MemberId == member.Id));

            var copy = uow.GetCopyByBarcode(barcode);
            var title = uow.Titles.GetRequired(copy.TitleId, "Title");
            if (!title.IsActive)
            {
                throw new BusinessRuleException(BusinessRuleException.StateRule, "This title is inactive.");
            }

            var dueDate = type.CalculateDueDate(today, rules.Calendar);
            LendCopy(uow, rules, copy, member.Id, now, today);
            var loan = new Loan(copy.Id, member.Id, now, dueDate, user.AccountId);
            uow.Loans.Add(loan);
            uow.SaveChanges();
            return new DtoProjector(uow, rules, today).ToLoanDtos([loan]).Single();
        });
    }

    public LoanDto GetReturnPreview(string barcode)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        var copy = uow.GetCopyByBarcode(barcode);
        var loan = RequireOpenLoan(uow, copy);
        return new DtoProjector(uow, LibraryRules.Load(uow), clock.Today).ToLoanDtos([loan]).Single();
    }

    public ReturnReceiptDto Return(string barcode, int? expectedLoanId = null)
    {
        var user = session.Require(RoleCode.Librarian);
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var copy = uow.GetCopyByBarcode(barcode);
            var loan = RequireOpenLoan(uow, copy);
            if (expectedLoanId.HasValue && expectedLoanId.Value != loan.Id)
            {
                throw new BusinessRuleException(BusinessRuleException.StateRule, "The loan has changed. Look up the barcode again.");
            }

            var rules = LibraryRules.Load(uow);
            var (days, amount) = new FineLedger(uow, rules).Calculate(loan, today);

            loan.Close(now, user.AccountId);
            if (amount > 0)
            {
                uow.Fines.Add(new Fine(loan.Id, loan.MemberId, days, amount, now));
            }

            copy.CheckIn();
            var hold = new HoldAllocator(uow, rules).AssignCopy(copy, today);
            var holderCode = hold == null ? null : uow.Members.GetRequired(hold.MemberId, "Member").MemberCode;
            uow.SaveChanges();
            var receiptLoan = new DtoProjector(uow, rules, today).ToLoanDtos([loan]).Single();
            return new ReturnReceiptDto(receiptLoan, copy.Status, days, amount, holderCode, hold?.HoldExpiresOn);
        });
    }

    public LoanDto Renew(int loanId)
    {
        var user = session.Require(RoleCode.Librarian, RoleCode.Member);
        var today = clock.Today;
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var loan = uow.Loans.GetRequired(loanId, "Loan");
            user.EnsureCanActForMember(loan.MemberId);
            var member = uow.Members.GetRequired(loan.MemberId, "Member");
            var type = uow.MembershipTypes.GetRequired(member.MembershipTypeId, "Membership type");
            var rules = LibraryRules.Load(uow);

            EnsureEligible(uow, rules, member, today);
            loan.EnsureCanBeRenewed(type.MaxRenewals, today);
            var copy = uow.Copies.GetRequired(loan.CopyId, "Copy");
            if (uow.Reservations.Query().Any(r => r.TitleId == copy.TitleId
                && r.MemberId != member.Id && r.Status == ReservationStatus.Waiting))
            {
                throw new BusinessRuleException("BR7", "This title is reserved by another member.");
            }

            loan.Renew(type.CalculateDueDate(loan.DueDate, rules.Calendar), type.MaxRenewals, today);
            uow.SaveChanges();
            return new DtoProjector(uow, rules, today).ToLoanDtos([loan]).Single();
        });
    }

    public IReadOnlyList<LoanDto> GetOpenLoans(string? memberCode = null, bool overdueOnly = false)
    {
        session.Require(RoleCode.Librarian);
        var today = clock.Today;
        using var uow = factory.Create();
        var loans = OpenLoans(uow);
        if (!string.IsNullOrWhiteSpace(memberCode))
        {
            var memberId = uow.GetMemberByCode(memberCode).Id;
            loans = loans.Where(l => l.MemberId == memberId);
        }

        if (overdueOnly)
        {
            loans = loans.Where(l => l.DueDate < today);
        }

        return ProjectLoans(uow, LibraryRules.Load(uow), today, loans);
    }

    public IReadOnlyList<LoanDto> GetMyLoans()
    {
        var memberId = session.Require(RoleCode.Member).RequireMemberId();
        using var uow = factory.Create();
        return ProjectLoans(uow, LibraryRules.Load(uow), clock.Today, OpenLoans(uow).Where(l => l.MemberId == memberId));
    }

    private static IQueryable<Loan> OpenLoans(IUnitOfWork uow) =>
        uow.Loans.Query().Where(l => l.ReturnedAt == null);

    private static Loan RequireOpenLoan(IUnitOfWork uow, Copy copy) =>
        OpenLoans(uow).SingleOrDefault(l => l.CopyId == copy.Id) ?? throw new NotFoundException("Open loan for barcode", copy.Barcode);

    private static IReadOnlyList<LoanDto> ProjectLoans(IUnitOfWork uow, LibraryRules rules, DateOnly today, IQueryable<Loan> loans) =>
        new DtoProjector(uow, rules, today).ToLoanDtos(loans.OrderBy(l => l.DueDate).ThenBy(l => l.Id).ToList());

    private static void EnsureEligible(IUnitOfWork uow, LibraryRules rules, Member member, DateOnly today) =>
        member.EnsureCanBorrow(today, new FineLedger(uow, rules).GetOutstanding(member.Id, today), rules.Policy);

    private static void LendCopy(IUnitOfWork uow, LibraryRules rules, Copy copy, int memberId, DateTime now, DateOnly today)
    {
        Reservation? hold = null;
        if (copy.Status == CopyStatus.OnHold)
        {
            hold = uow.Reservations.Query().SingleOrDefault(r => r.HeldCopyId == copy.Id && r.Status == ReservationStatus.OnHold)
                ?? throw new BusinessRuleException("BR6", $"Copy {copy.Barcode} has no matching active hold.");
            if (hold.IsHoldExpired(today))
            {
                hold.Expire(now);
                copy.ReleaseHold();
                hold = new HoldAllocator(uow, rules).AssignCopy(copy, today);
            }
        }

        if (hold == null)
        {
            copy.CheckOut();
            return;
        }

        if (hold.MemberId != memberId)
        {
            throw new BusinessRuleException("BR6", $"Copy {copy.Barcode} is on hold for another member.");
        }

        hold.Fulfil(now);
        copy.CheckOutHeldCopy();
    }
}
