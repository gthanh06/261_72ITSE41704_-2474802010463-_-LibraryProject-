using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Internal;
using ELibrary.Domain.Common;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>
/// UC03 and NFR-08: membership types, the fine policy, holidays, and database backup and restore.
/// Librarians may read the settings; only the administrator changes them.
/// </summary>
public sealed class SettingsService(IUnitOfWorkFactory factory, IUserSession session, IDatabaseMaintenance maintenance)
{
    // ---- Membership types ----------------------------------------------------------------------

    public IReadOnlyList<MembershipTypeDto> GetMembershipTypes()
    {
        session.Require(RoleCode.Administrator, RoleCode.Librarian);
        using var uow = factory.Create();
        return uow.MembershipTypes.Query().OrderBy(t => t.Name).AsEnumerable().Select(ToDto).ToList();
    }

    /// <summary>Creates a type when <see cref="MembershipTypeDto.Id"/> is 0, otherwise updates it. Returns the id.</summary>
    public int SaveMembershipType(MembershipTypeDto input)
    {
        session.Require(RoleCode.Administrator);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var name = Guard.Required(input.Name, "Membership type", 50);
            var upper = name.ToUpperInvariant();
            if (uow.MembershipTypes.Query().Any(t => t.Id != input.Id && t.Name.ToUpper() == upper))
            {
                throw new ValidationException("Membership type", $"A membership type called '{name}' already exists.");
            }

            MembershipType type;
            if (input.Id == 0)
            {
                type = new MembershipType(name, input.MaxLoans, input.LoanPeriodDays, input.MaxRenewals, input.IsActive);
                uow.MembershipTypes.Add(type);
            }
            else
            {
                type = uow.MembershipTypes.GetRequired(input.Id, "Membership type");
                type.Update(name, input.MaxLoans, input.LoanPeriodDays, input.MaxRenewals, input.IsActive);
            }

            uow.SaveChanges();
            return type.Id;
        });
    }

    // ---- Fine policy ---------------------------------------------------------------------------

    public FinePolicyDto GetFinePolicy()
    {
        session.Require(RoleCode.Administrator, RoleCode.Librarian);
        using var uow = factory.Create();
        var policy = LibraryRules.Load(uow).Policy;
        return new FinePolicyDto(policy.DailyRate, policy.MaxFinePerLoan, policy.DebtThreshold,
            policy.HoldPeriodDays, policy.ClosedOnSundays);
    }

    /// <summary>Applies to fines recorded from now on; fines already recorded keep their amount.</summary>
    public void UpdateFinePolicy(FinePolicyDto input)
    {
        session.Require(RoleCode.Administrator);
        using var uow = factory.Create();
        var policy = LibraryRules.Load(uow).Policy;
        policy.Update(input.DailyRate, input.MaxFinePerLoan, input.DebtThreshold, input.HoldPeriodDays, input.ClosedOnSundays);
        uow.SaveChanges();
    }

    // ---- Holidays ------------------------------------------------------------------------------

    public IReadOnlyList<HolidayDto> GetHolidays(int? year = null)
    {
        session.Require(RoleCode.Administrator, RoleCode.Librarian);
        using var uow = factory.Create();
        var query = uow.Holidays.Query();
        if (year.HasValue)
        {
            var first = new DateOnly(year.Value, 1, 1);
            var last = new DateOnly(year.Value, 12, 31);
            query = query.Where(h => h.Date >= first && h.Date <= last);
        }

        return query.OrderBy(h => h.Date).Select(h => new HolidayDto(h.Id, h.Date, h.Description)).ToList();
    }

    public int AddHoliday(DateOnly date, string? description)
    {
        session.Require(RoleCode.Administrator);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            if (uow.Holidays.Query().Any(h => h.Date == date))
            {
                throw new ValidationException("Date", $"{date:dd/MM/yyyy} is already a holiday.");
            }

            var holiday = new Holiday(date, description ?? string.Empty);
            uow.Holidays.Add(holiday);
            uow.SaveChanges();
            return holiday.Id;
        });
    }

    /// <summary>Due dates and hold expiry dates that were already calculated are not changed.</summary>
    public void RemoveHoliday(int holidayId)
    {
        session.Require(RoleCode.Administrator);
        using var uow = factory.Create();
        uow.Holidays.Remove(uow.Holidays.GetRequired(holidayId, "Holiday"));
        uow.SaveChanges();
    }

    // ---- Backup and restore (NFR-08) -----------------------------------------------------------

    public void Backup(string? backupFilePath)
    {
        session.Require(RoleCode.Administrator);
        maintenance.Backup(RequireBackupPath(backupFilePath));
    }

    /// <summary>Replaces the whole database with the backup. Everyone must log in again afterwards.</summary>
    public void Restore(string? backupFilePath)
    {
        session.Require(RoleCode.Administrator);
        maintenance.Restore(RequireBackupPath(backupFilePath));
    }

    private static string RequireBackupPath(string? path)
    {
        var file = Guard.Required(path, "Backup file", 400);
        if (!file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Backup file", "Choose a backup file with the .bak extension.");
        }

        if (!Path.IsPathFullyQualified(file))
        {
            throw new ValidationException("Backup file", "Enter the full path of the backup file, e.g. C:\\Backups\\ELibrary.bak.");
        }

        return file;
    }

    private static MembershipTypeDto ToDto(MembershipType t) =>
        new(t.Id, t.Name, t.MaxLoans, t.LoanPeriodDays, t.MaxRenewals, t.IsActive);
}
