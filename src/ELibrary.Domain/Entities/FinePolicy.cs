using ELibrary.Domain.Common;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

public sealed class FinePolicy : Entity
{
    private FinePolicy() { }

    public FinePolicy(decimal dailyRate, decimal maxFinePerLoan, decimal debtThreshold,
        int holdPeriodDays, bool closedOnSundays) =>
        Update(dailyRate, maxFinePerLoan, debtThreshold, holdPeriodDays, closedOnSundays);

    public decimal DailyRate { get; private set; }
    public decimal MaxFinePerLoan { get; private set; }
    public decimal DebtThreshold { get; private set; }
    public int HoldPeriodDays { get; private set; }
    public bool ClosedOnSundays { get; private set; }

    public static FinePolicy CreateDefault() => new(5_000m, 100_000m, 50_000m, 3, true);

    public void Update(decimal dailyRate, decimal maxFinePerLoan, decimal debtThreshold,
        int holdPeriodDays, bool closedOnSundays)
    {
        Guard.Positive(dailyRate, "Daily fine");
        Guard.NotNegative(debtThreshold, "Debt threshold");
        if (maxFinePerLoan < dailyRate)
        {
            throw new ValidationException("Fine cap", "The fine cap must be at least one day's fine.");
        }

        DailyRate = dailyRate;
        MaxFinePerLoan = maxFinePerLoan;
        DebtThreshold = debtThreshold;
        HoldPeriodDays = Guard.InRange(holdPeriodDays, 1, 30, "Hold period");
        ClosedOnSundays = closedOnSundays;
    }

    public decimal CalculateFine(int chargeableDays) => Math.Min(Math.Max(0, chargeableDays) * DailyRate, MaxFinePerLoan);

    public bool IsAboveDebtThreshold(decimal balance) => balance > DebtThreshold;
}
