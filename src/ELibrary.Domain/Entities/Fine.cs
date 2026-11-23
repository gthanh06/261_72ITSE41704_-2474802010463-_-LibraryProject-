using ELibrary.Domain.Common;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

public sealed class Fine : Entity
{
    private Fine() { }

    public Fine(int loanId, int memberId, int overdueDays, decimal amount, DateTime createdAt)
    {
        LoanId = loanId;
        MemberId = memberId;
        OverdueDays = Guard.InRange(overdueDays, 1, int.MaxValue, "Overdue days");
        Amount = Guard.Positive(amount, "Fine amount");
        CreatedAt = createdAt;
    }

    public int LoanId { get; private set; }
    public int MemberId { get; private set; }
    public int OverdueDays { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal Balance => Amount - AmountPaid;
    public bool IsSettled => Balance == 0;

    public Payment ApplyPayment(decimal amount, DateTime now, int staffId)
    {
        var payment = new Payment(Id, amount, now, staffId);
        if (amount > Balance)
        {
            throw new ValidationException("Payment", $"The payment exceeds the remaining fine of {Money.Format(Balance)}.");
        }

        AmountPaid += amount;
        return payment;
    }
}
