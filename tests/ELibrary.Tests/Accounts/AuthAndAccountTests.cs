using ELibrary.Business.Abstractions;
using ELibrary.Business.Services;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;
using ELibrary.Tests.Support;

namespace ELibrary.Tests.Accounts;

/// <summary>UC01 log-in (NFR-03, NFR-05) and UC02 staff accounts (NFR-04).</summary>
public sealed class AuthAndAccountTests
{
    private const string AdminPassword = "Admin@123";
    private const string StaffPassword = "Librarian@123";
    private MemoryLibrary _db = null!;
    private FixedClock _clock = null!;
    private UserSession _session = null!;
    private AuthService _auth = null!;
    private AccountService _accounts = null!;
    private UserAccount _admin = null!;
    private readonly FastHasher _hasher = new();

    [SetUp]
    public void SetUp()
    {
        _db = new MemoryLibrary();
        _clock = new FixedClock();
        _session = new UserSession();
        _auth = new AuthService(_db, _hasher, _clock, _session);
        _accounts = new AccountService(_db, _hasher, _clock, _session);
        _admin = new UserAccount("admin", _hasher.Hash(AdminPassword), "Nguyễn Văn Quản", RoleCode.Administrator, _clock.Now);
        _db.UserAccounts.Add(_admin);
    }

    // ---- UC01 Login ----------------------------------------------------------------------------

    [Test]
    public void LoginIsCaseInsensitiveForTheUsernameAndSignsIn()
    {
        var user = _auth.Login("  ADMIN ", AdminPassword);
        Assert.Multiple(() =>
        {
            Assert.That(user.Role, Is.EqualTo(RoleCode.Administrator));
            Assert.That(_session.User, Is.EqualTo(user));
            Assert.That(_admin.LastLoginAt, Is.EqualTo(_clock.Now));
        });
    }

    [TestCase("admin", "wrong-password")]
    [TestCase("nobody", AdminPassword)]
    public void WrongUsernameOrPasswordGiveTheSameMessage(string username, string password)
    {
        var error = Assert.Throws<AuthenticationFailedException>(() => _auth.Login(username, password))!;
        Assert.That(error.Message, Is.EqualTo(AuthService.WrongCredentials));
        Assert.That(_session.User, Is.Null);
    }

    [TestCase("", AdminPassword)]
    [TestCase("admin", "")]
    public void EmptyCredentialsAreAValidationError(string username, string password) =>
        Assert.Throws<ValidationException>(() => _auth.Login(username, password));

    [Test]
    public void FiveFailedAttemptsLockTheAccountForFiveMinutes()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.Throws<AuthenticationFailedException>(() => _auth.Login("admin", "wrong-1"));
        }

        Assert.That(Assert.Throws<AuthenticationFailedException>(() => _auth.Login("admin", AdminPassword))!.Message,
            Does.Contain("Try again after 10:05"));

        _clock.Now = _clock.Now.AddMinutes(5).AddSeconds(1);
        Assert.That(_auth.Login("admin", AdminPassword).Username, Is.EqualTo("admin"));
        Assert.That(_admin.FailedLoginAttempts, Is.Zero);
    }

    [Test]
    public void ASuccessfulLoginResetsTheFailureCount()
    {
        for (var i = 0; i < 4; i++)
        {
            Assert.Throws<AuthenticationFailedException>(() => _auth.Login("admin", "wrong-1"));
        }

        _auth.Login("admin", AdminPassword);
        Assert.Throws<AuthenticationFailedException>(() => _auth.Login("admin", "wrong-1"));
        Assert.That(_admin.IsLockedOut(_clock.Now), Is.False);
    }

    [Test]
    public void LockedAccountCannotLogInEvenWithTheRightPassword()
    {
        _admin.Lock();
        Assert.That(Assert.Throws<AuthenticationFailedException>(() => _auth.Login("admin", AdminPassword))!.Message,
            Does.Contain("locked"));
    }

    [Test]
    public void MemberLoginCarriesTheMemberIdUnlessTheMemberIsInactive()
    {
        var type = new MembershipType("Student", 3, 14, 1);
        _db.MembershipTypes.Add(type);
        var account = new UserAccount("M0001", _hasher.Hash("Member@123"), "Reader", RoleCode.Member, _clock.Now);
        _db.UserAccounts.Add(account);
        var member = new Member("M0001", "Reader", null, null, type.Id, _clock.Today, _clock.Today.AddYears(1));
        member.LinkAccount(account.Id);
        _db.Members.Add(member);

        Assert.That(_auth.Login("m0001", "Member@123").MemberId, Is.EqualTo(member.Id));
        _auth.Logout();
        Assert.That(_session.User, Is.Null);

        member.Deactivate();
        Assert.Throws<AuthenticationFailedException>(() => _auth.Login("M0001", "Member@123"));
    }

    [Test]
    public void ChangeOwnPasswordChecksTheCurrentPasswordAndThePolicy()
    {
        _auth.Login("admin", AdminPassword);
        Assert.Throws<ValidationException>(() => _auth.ChangeOwnPassword("not-it-1", "NewPass123"));
        Assert.Throws<ValidationException>(() => _auth.ChangeOwnPassword(AdminPassword, "short1"));
        Assert.Throws<ValidationException>(() => _auth.ChangeOwnPassword(AdminPassword, AdminPassword));

        _auth.ChangeOwnPassword(AdminPassword, "NewPass123");
        _auth.Logout();
        Assert.Throws<AuthenticationFailedException>(() => _auth.Login("admin", AdminPassword));
        Assert.That(_auth.Login("admin", "NewPass123").Username, Is.EqualTo("admin"));
    }

    [Test]
    public void ChangeOwnPasswordRequiresALogin() =>
        Assert.Throws<AccessDeniedException>(() => _auth.ChangeOwnPassword(AdminPassword, "NewPass123"));

    // ---- UC02 Staff accounts -------------------------------------------------------------------

    [Test]
    public void AdministratorCreatesAStaffAccountThatCanLogIn()
    {
        SignInAdmin();
        var id = _accounts.CreateStaffAccount("librarian1", "Trần Thị Thu", RoleCode.Librarian, StaffPassword);
        Assert.That(_accounts.GetAccounts().Select(a => a.Id), Does.Contain(id));

        Assert.That(_auth.Login("librarian1", StaffPassword).Role, Is.EqualTo(RoleCode.Librarian));
    }

    [TestCase("ADMIN", RoleCode.Librarian, StaffPassword, TestName = "Duplicate username, other case")]
    [TestCase("reader", RoleCode.Member, StaffPassword, TestName = "Member role is not a staff role")]
    [TestCase("librarian1", RoleCode.Librarian, "librarian1", TestName = "Password equal to the username")]
    [TestCase("bad name", RoleCode.Librarian, StaffPassword, TestName = "Username with a space")]
    public void InvalidStaffAccountsAreRejected(string username, RoleCode role, string password)
    {
        SignInAdmin();
        Assert.Throws<ValidationException>(() => _accounts.CreateStaffAccount(username, "Someone", role, password));
        Assert.That(_db.UserAccounts.Query().Count(), Is.EqualTo(1));
    }

    [Test]
    public void OnlyTheAdministratorManagesAccounts()
    {
        var librarianId = CreateLibrarian();
        _session.SignIn(new CurrentUser(librarianId, "librarian1", "Thu", RoleCode.Librarian, null));
        Assert.Multiple(() =>
        {
            Assert.Throws<AccessDeniedException>(() => _accounts.GetAccounts());
            Assert.Throws<AccessDeniedException>(() => _accounts.CreateStaffAccount("x1", "X", RoleCode.Librarian, StaffPassword));
            Assert.Throws<AccessDeniedException>(() => _accounts.SetLocked(_admin.Id, true));
            Assert.Throws<AccessDeniedException>(() => _accounts.ResetPassword(_admin.Id, "NewPass123"));
        });
    }

    [Test]
    public void TheLastAdministratorCannotBeLockedOrDemoted()
    {
        SignInAdmin();
        Assert.Throws<BusinessRuleException>(() => _accounts.SetLocked(_admin.Id, true));

        var second = _accounts.CreateStaffAccount("admin2", "Second admin", RoleCode.Administrator, StaffPassword);
        _accounts.SetLocked(second, true);
        Assert.Throws<BusinessRuleException>(() => _accounts.UpdateStaffAccount(_admin.Id, "Me", RoleCode.Librarian),
            "own role");
        _session.SignIn(new CurrentUser(second, "admin2", "Second admin", RoleCode.Administrator, null));
        Assert.Throws<BusinessRuleException>(() => _accounts.UpdateStaffAccount(_admin.Id, "Quản", RoleCode.Librarian),
            "the only unlocked administrator");
    }

    [Test]
    public void UnlockingClearsATemporaryLockout()
    {
        var librarianId = CreateLibrarian();
        for (var i = 0; i < 5; i++)
        {
            Assert.Throws<AuthenticationFailedException>(() => _auth.Login("librarian1", "wrong-1"));
        }

        SignInAdmin();
        Assert.That(_accounts.GetAccounts().Single(a => a.Id == librarianId).IsTemporarilyLockedOut, Is.True);
        _accounts.SetLocked(librarianId, false);
        Assert.That(_auth.Login("librarian1", StaffPassword).AccountId, Is.EqualTo(librarianId));
    }

    [Test]
    public void ResetPasswordAndRoleChangeApplyToStaffOnly()
    {
        var librarianId = CreateLibrarian();
        var member = new UserAccount("M0001", _hasher.Hash("Member@123"), "Reader", RoleCode.Member, _clock.Now);
        _db.UserAccounts.Add(member);

        _accounts.ResetPassword(librarianId, "Fresh2026x");
        var updated = _accounts.UpdateStaffAccount(librarianId, "Trần Thu", RoleCode.Administrator);
        Assert.That(updated.Role, Is.EqualTo(RoleCode.Administrator));
        Assert.That(_auth.Login("librarian1", "Fresh2026x").FullName, Is.EqualTo("Trần Thu"));

        SignInAdmin();
        Assert.Throws<BusinessRuleException>(() => _accounts.ResetPassword(member.Id, "Fresh2026x"));
        Assert.That(_accounts.GetAccounts().Any(a => a.Role == RoleCode.Member), Is.False);
        Assert.That(_accounts.GetAccounts(includeMembers: true).Any(a => a.Role == RoleCode.Member), Is.True);
    }

    private void SignInAdmin() => _session.SignIn(new CurrentUser(_admin.Id, "admin", _admin.FullName, RoleCode.Administrator, null));

    private int CreateLibrarian()
    {
        SignInAdmin();
        return _accounts.CreateStaffAccount("librarian1", "Trần Thị Thu", RoleCode.Librarian, StaffPassword);
    }
}
