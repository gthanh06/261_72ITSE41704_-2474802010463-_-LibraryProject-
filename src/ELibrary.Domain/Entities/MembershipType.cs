using ELibrary.Domain.Common;
using ELibrary.Domain.Exceptions;
using ELibrary.Domain.Services;

namespace ELibrary.Domain.Entities;

public sealed class MembershipType : Entity
{
    private MembershipType() => Name = string.Empty;

    public MembershipType(string name, int maxLoans, int loanPeriodDays, int maxRenewals, bool isActive = true)
        : this() => Update(name, maxLoans, loanPeriodDays, maxRenewals, isActive);

    public string Name { get; private set; }
    public int MaxLoans { get; private set; }
    public int LoanPeriodDays { get; private set; }
    public int MaxRenewals { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, int maxLoans, int loanPeriodDays, int maxRenewals, bool isActive)
    {
        Name = Guard.Required(name, "Membership type", 50);
        MaxLoans = Guard.InRange(maxLoans, 1, int.MaxValue, "Loan limit");
        LoanPeriodDays = Guard.InRange(loanPeriodDays, 1, 36_500, "Loan period");
        MaxRenewals = Guard.InRange(maxRenewals, 0, int.MaxValue, "Renewal limit");
        IsActive = isActive;
    }

    public void EnsureBelowLoanLimit(int openLoans)
    {
        if (openLoans >= MaxLoans)
        {
            throw new BusinessRuleException("BR1", $"Loan limit reached ({openLoans}/{MaxLoans}).");
        }
    }

    public DateOnly CalculateDueDate(DateOnly start, LibraryCalendar calendar) =>
        calendar.FirstOpeningDayOnOrAfter(start.AddDays(LoanPeriodDays));
}
