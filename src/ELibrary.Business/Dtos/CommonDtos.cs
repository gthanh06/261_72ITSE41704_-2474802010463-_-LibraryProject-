using ELibrary.Domain.Enums;

namespace ELibrary.Business.Dtos;

public sealed record LookupDto(int Id, string Name);

public sealed record AccountDto(
    int Id, string Username, string FullName, RoleCode Role,
    bool IsLocked, bool IsTemporarilyLockedOut, DateTime? LastLoginAt);

public sealed record MembershipTypeDto(int Id, string Name, int MaxLoans, int LoanPeriodDays, int MaxRenewals, bool IsActive);

public sealed record FinePolicyDto(decimal DailyRate, decimal MaxFinePerLoan, decimal DebtThreshold, int HoldPeriodDays, bool ClosedOnSundays);

public sealed record HolidayDto(int Id, DateOnly Date, string Description);
