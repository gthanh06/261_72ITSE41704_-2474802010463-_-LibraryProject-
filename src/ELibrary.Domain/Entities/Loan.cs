using ELibrary.Domain.Common;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

public sealed class Loan : Entity
{
    private Loan() { }

    public Loan(int copyId, int memberId, DateTime loanDate, DateOnly dueDate, int issuedById)
    {
        CopyId = copyId;
        MemberId = memberId;
        LoanDate = loanDate;
        DueDate = dueDate;
        IssuedById = issuedById;
    }

    public int CopyId { get; private set; }
    public int MemberId { get; private set; }
    public DateTime LoanDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateTime? ReturnedAt { get; private set; }
    public int RenewalCount { get; private set; }
    public int IssuedById { get; private set; }
    public int? ReturnedById { get; private set; }
    public bool IsOpen => !ReturnedAt.HasValue;

    public bool IsOverdue(DateOnly today) => IsOpen && DueDate < today;

    public void EnsureCanBeRenewed(int maxRenewals, DateOnly today)
    {
        if (!IsOpen)
        {
            throw new BusinessRuleException("BR7", "This loan has already been returned.");
        }

        if (IsOverdue(today))
        {
            throw new BusinessRuleException("BR7", "Overdue items cannot be renewed. Please return the item.");
        }

        if (RenewalCount >= maxRenewals)
        {
            throw new BusinessRuleException("BR7", $"Renewal limit reached ({RenewalCount}/{maxRenewals}).");
        }
    }

    public void Renew(DateOnly newDueDate, int maxRenewals, DateOnly today)
    {
        EnsureCanBeRenewed(maxRenewals, today);
        if (newDueDate <= DueDate)
        {
            throw new ValidationException("Due date", "The new due date must be after the current due date.");
        }

        DueDate = newDueDate;
        RenewalCount++;
    }

    public void Close(DateTime returnedAt, int staffId)
    {
        if (!IsOpen || returnedAt < LoanDate)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule, "This loan cannot be closed on the selected date.");
        }

        ReturnedAt = returnedAt;
        ReturnedById = staffId;
    }
}
