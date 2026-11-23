using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Services;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;
using ELibrary.Tests.Support;

namespace ELibrary.Tests.Accounts;

/// <summary>UC03 settings and NFR-08 backup/restore.</summary>
public sealed class SettingsTests
{
    private readonly CurrentUser _admin = new(1, "admin", "Admin", RoleCode.Administrator, null);
    private readonly CurrentUser _librarian = new(2, "librarian1", "Thu", RoleCode.Librarian, null);
    private MemoryLibrary _db = null!;
    private UserSession _session = null!;
    private RecordingMaintenance _maintenance = null!;
    private SettingsService _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new MemoryLibrary();
        _db.FinePolicies.Add(FinePolicy.CreateDefault());
        _session = new UserSession();
        _session.SignIn(_admin);
        _maintenance = new RecordingMaintenance();
        _settings = new SettingsService(_db, _session, _maintenance);
    }

    [Test]
    public void MembershipTypesAreCreatedAndUpdated()
    {
        var id = _settings.SaveMembershipType(new MembershipTypeDto(0, "Student", 3, 14, 1, true));
        _settings.SaveMembershipType(new MembershipTypeDto(id, "Student", 4, 21, 2, false));

        var type = _settings.GetMembershipTypes().Single();
        Assert.That(type, Is.EqualTo(new MembershipTypeDto(id, "Student", 4, 21, 2, false)));
    }

    [TestCase("student", 3, 14, 1, TestName = "Duplicate name, other case")]
    [TestCase("Lecturer", 0, 14, 1, TestName = "Loan limit 0")]
    [TestCase("Lecturer", 3, 0, 1, TestName = "Loan period 0")]
    [TestCase("Lecturer", 3, 14, -1, TestName = "Negative renewals")]
    [TestCase(" ", 3, 14, 1, TestName = "Empty name")]
    public void InvalidMembershipTypesAreRejected(string name, int maxLoans, int period, int renewals)
    {
        _settings.SaveMembershipType(new MembershipTypeDto(0, "Student", 3, 14, 1, true));
        Assert.Throws<ValidationException>(() =>
            _settings.SaveMembershipType(new MembershipTypeDto(0, name, maxLoans, period, renewals, true)));
        Assert.That(_db.MembershipTypes.Query().Count(), Is.EqualTo(1));
    }

    [Test]
    public void FinePolicyUpdateIsValidatedAndStored()
    {
        Assert.Throws<ValidationException>(() =>
            _settings.UpdateFinePolicy(new FinePolicyDto(10_000, 5_000, 50_000, 3, true)), "cap below one day");
        Assert.Throws<ValidationException>(() =>
            _settings.UpdateFinePolicy(new FinePolicyDto(5_000, 100_000, 50_000, 0, true)), "no hold period");

        _settings.UpdateFinePolicy(new FinePolicyDto(2_000, 60_000, 30_000, 5, false));
        Assert.That(_settings.GetFinePolicy(), Is.EqualTo(new FinePolicyDto(2_000, 60_000, 30_000, 5, false)));
    }

    [Test]
    public void HolidaysAreUniqueAndListedByYear()
    {
        _settings.AddHoliday(new DateOnly(2026, 9, 2), "Quốc khánh");
        var id = _settings.AddHoliday(new DateOnly(2027, 1, 1), "Tết Dương lịch");
        Assert.Throws<ValidationException>(() => _settings.AddHoliday(new DateOnly(2026, 9, 2), "Again"));
        Assert.Throws<ValidationException>(() => _settings.AddHoliday(new DateOnly(2026, 12, 24), " "));

        Assert.That(_settings.GetHolidays(2026).Select(h => h.Description), Is.EqualTo(new[] { "Quốc khánh" }));
        _settings.RemoveHoliday(id);
        Assert.That(_settings.GetHolidays(), Has.Count.EqualTo(1));
    }

    [Test]
    public void LibrariansReadSettingsButCannotChangeThem()
    {
        _session.SignIn(_librarian);
        Assert.That(_settings.GetFinePolicy().DailyRate, Is.EqualTo(5_000m));
        Assert.That(_settings.GetHolidays(), Is.Empty);
        Assert.Multiple(() =>
        {
            Assert.Throws<AccessDeniedException>(() => _settings.SaveMembershipType(new MembershipTypeDto(0, "X", 1, 1, 0, true)));
            Assert.Throws<AccessDeniedException>(() => _settings.UpdateFinePolicy(new FinePolicyDto(1_000, 1_000, 0, 1, true)));
            Assert.Throws<AccessDeniedException>(() => _settings.AddHoliday(new DateOnly(2026, 1, 2), "X"));
            Assert.Throws<AccessDeniedException>(() => _settings.Backup(@"C:\Backups\x.bak"));
        });
        Assert.That(_maintenance.Calls, Is.Empty);
    }

    [TestCase("", TestName = "No file")]
    [TestCase(@"C:\Backups\elibrary.zip", TestName = "Not a .bak file")]
    [TestCase(@"backups\elibrary.bak", TestName = "Relative path")]
    public void BackupNeedsAFullBakPath(string path)
    {
        Assert.Throws<ValidationException>(() => _settings.Backup(path));
        Assert.That(_maintenance.Calls, Is.Empty);
    }

    [Test]
    public void BackupAndRestoreReachTheDatabase()
    {
        _settings.Backup(@"C:\Backups\ELibrary_20261123.bak");
        _settings.Restore(@"C:\Backups\ELibrary_20261123.bak");
        Assert.That(_maintenance.Calls, Is.EqualTo(new[]
        {
            @"backup C:\Backups\ELibrary_20261123.bak",
            @"restore C:\Backups\ELibrary_20261123.bak"
        }));
    }
}
