using ELibrary.Domain.Common;

namespace ELibrary.Domain.Entities;

public sealed class Payment : Entity
{
    private Payment() { }

    public Payment(int fineId, decimal amount, DateTime paidAt, int receivedById)
    {
        FineId = fineId;
        Amount = Guard.Positive(amount, "Payment");
        PaidAt = paidAt;
        ReceivedById = receivedById;
    }

    public int FineId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PaidAt { get; private set; }
    public int ReceivedById { get; private set; }
}
