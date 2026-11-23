using ELibrary.Business.Abstractions;
using ELibrary.Business.Services;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;
using ELibrary.Tests.Support;

namespace ELibrary.Tests.Reservations;

/// <summary>UC10 reservations and UC11 holds: BR2, BR4 (FIFO queue), BR5 (hold expiry) and BR6.</summary>
public sealed class ReservationTests
{
    private readonly CurrentUser _librarian = new(90, "librarian1", "Thu", RoleCode.Librarian, null);
    private MemoryLibrary _db = null!;
    private FixedClock _clock = null!;
    private UserSession _session = null!;
    private ReservationService _reservations = null!;
    private CirculationService _circulation = null!;
    private MembershipType _type = null!;
    private Title _title = null!;
    private Copy _copy = null!;
    private Member _anh = null!;
    private Member _bao = null!;
    private Member _chi = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new MemoryLibrary();
        _clock = new FixedClock(); // Monday 2 November 2026, 10:00
        _session = new UserSession();
        _reservations = new ReservationService(_db, _clock, _session);
        _circulation = new CirculationService(_db, _clock, _session);
        _db.FinePolicies.Add(FinePolicy.CreateDefault());
        _type = new MembershipType("Student", 3, 14, 1);
        _db.MembershipTypes.Add(_type);
        _anh = AddMember("M0001", "Nguyễn Minh Anh");
        _bao = AddMember("M0002", "Trần Quốc Bảo");
        _chi = AddMember("M0003", "Lê Thị Chi");
        _title = new Title(null, "Refactoring", null, null, 2018, null);
        _db.Titles.Add(_title);
        _copy = new Copy(_title.Id, "LIB0041", "Shelf SE-02");
        _db.Copies.Add(_copy);
    }

    [Test]
    public void ATitleWithACopyOnTheShelfCannotBeReserved()
    {
        SignIn(_anh);
        Assert.That(Assert.Throws<BusinessRuleException>(() => _reservations.Place(_title.Id))!.Rule, Is.EqualTo("BR4"));
        Assert.That(_db.Reservations.Query(), Is.Empty);
    }

    [Test]
    public void MembersJoinTheQueueInOrderAndOnlyOnce()
    {
        Lend(_copy, _chi);
        var first = Reserve(_anh);
        _clock.Now = _clock.Now.AddMinutes(5);
        var second = Reserve(_bao);

        Assert.Multiple(() =>
        {
            Assert.That(first.QueuePosition, Is.EqualTo(1));
            Assert.That(second.QueuePosition, Is.EqualTo(2));
            Assert.That(second.Status, Is.EqualTo(ReservationStatus.Waiting));
        });
        SignIn(_bao);
        Assert.That(Assert.Throws<BusinessRuleException>(() => _reservations.Place(_title.Id))!.Rule, Is.EqualTo("BR4"));
    }

    [Test]
    public void AMemberWhoCannotBorrowCannotReserve()
    {
        Lend(_copy, _chi);
        _db.Fines.Add(new Fine(999, _anh.Id, 12, 60_000, _clock.Now.AddDays(-3)));
        SignIn(_anh);
        Assert.That(Assert.Throws<BusinessRuleException>(() => _reservations.Place(_title.Id))!.Rule, Is.EqualTo("BR2"));
    }

    [Test]
    public void ATitleWithOnlyLostCopiesCannotBeReserved()
    {
        _copy.ChangeStatus(CopyStatus.Lost);
        SignIn(_anh);
        Assert.That(Assert.Throws<BusinessRuleException>(() => _reservations.Place(_title.Id))!.Message,
            Does.Contain("no copy that can be reserved"));
    }

    [Test]
    public void OnlyMembersPlaceReservations()
    {
        Lend(_copy, _chi);
        _session.SignIn(_librarian);
        Assert.Throws<AccessDeniedException>(() => _reservations.Place(_title.Id));
        _session.SignOut();
        Assert.Throws<AccessDeniedException>(() => _reservations.Place(_title.Id));
    }

    [Test]
    public void AReturnedCopyIsHeldForTheFirstMemberForThreeOpeningDays()
    {
        Lend(_copy, _chi);
        Reserve(_anh);
        Reserve(_bao);

        _clock.Now = new DateTime(2026, 11, 6, 15, 0, 0); // Friday; Sunday is closed
        _session.SignIn(_librarian);
        _circulation.Return(_copy.Barcode);

        var holds = _reservations.GetActiveHolds(includeWaiting: true);
        Assert.Multiple(() =>
        {
            Assert.That(holds[0].MemberCode, Is.EqualTo("M0001"));
            Assert.That(holds[0].HeldBarcode, Is.EqualTo("LIB0041"));
            Assert.That(holds[0].HoldExpiresOn, Is.EqualTo(new DateOnly(2026, 11, 10)));
            Assert.That(holds[1].MemberCode, Is.EqualTo("M0002"));
            Assert.That(holds[1].QueuePosition, Is.EqualTo(1));
            Assert.That(_copy.Status, Is.EqualTo(CopyStatus.OnHold));
        });
    }

    [Test]
    public void AnUncollectedHoldExpiresAndTheCopyGoesToTheNextMember()
    {
        Lend(_copy, _chi);
        Reserve(_anh);
        Reserve(_bao);
        _session.SignIn(_librarian);
        _circulation.Return(_copy.Barcode);

        _clock.Now = new DateTime(2026, 11, 6, 9, 0, 0); // last collection day was Thursday 5 November
        var result = _reservations.ProcessHolds();

        Assert.That(result, Is.EqualTo(new ELibrary.Business.Dtos.HoldProcessingResultDto(1, 1, 0)));
        var hold = _reservations.GetActiveHolds().Single();
        Assert.That(hold.MemberCode, Is.EqualTo("M0002"));
        Assert.That(_db.Reservations.Query().Single(r => r.MemberId == _anh.Id).Status, Is.EqualTo(ReservationStatus.Expired));
        Assert.That(Assert.Throws<BusinessRuleException>(() => _circulation.CheckOut("M0001", _copy.Barcode))!.Rule,
            Is.EqualTo("BR6"));
    }

    [Test]
    public void StartupExpiryReturnsTheCopyToTheShelfWhenNobodyElseWaits()
    {
        Lend(_copy, _chi);
        Reserve(_anh);
        _session.SignIn(_librarian);
        _circulation.Return(_copy.Barcode);
        _session.SignOut();

        _clock.Now = _clock.Now.AddDays(10);
        Assert.That(_reservations.ExpireHoldsAtStartup(), Is.EqualTo(new ELibrary.Business.Dtos.HoldProcessingResultDto(1, 0, 1)));
        Assert.That(_copy.Status, Is.EqualTo(CopyStatus.Available));
        Assert.That(_reservations.ExpireHoldsAtStartup().ExpiredHolds, Is.Zero, "running it again changes nothing");
    }

    [Test]
    public void CancellingAHoldPassesTheCopyOn()
    {
        Lend(_copy, _chi);
        var anh = Reserve(_anh);
        Reserve(_bao);
        _session.SignIn(_librarian);
        _circulation.Return(_copy.Barcode);

        SignIn(_anh);
        _reservations.Cancel(anh.Id);
        var mine = _reservations.GetMyReservations().Single();
        Assert.That(mine.Status, Is.EqualTo(ReservationStatus.Cancelled));
        Assert.That(mine.ClosedAt, Is.EqualTo(_clock.Now));

        SignIn(_bao);
        var bao = _reservations.GetMyReservations().Single();
        Assert.That(bao.Status, Is.EqualTo(ReservationStatus.OnHold));
        Assert.That(bao.HeldBarcode, Is.EqualTo("LIB0041"));
    }

    [Test]
    public void AMemberCannotCancelSomeoneElsesReservation()
    {
        Lend(_copy, _chi);
        var anh = Reserve(_anh);
        SignIn(_bao);
        Assert.Throws<AccessDeniedException>(() => _reservations.Cancel(anh.Id));

        _session.SignIn(_librarian);
        _reservations.Cancel(anh.Id);
        Assert.Throws<BusinessRuleException>(() => _reservations.Cancel(anh.Id), "already closed");
    }

    [Test]
    public void TheHolderCollectsTheCopyAndTheReservationIsFulfilled()
    {
        Lend(_copy, _chi);
        Reserve(_anh);
        _session.SignIn(_librarian);
        _circulation.Return(_copy.Barcode);
        _circulation.CheckOut("M0001", _copy.Barcode);

        SignIn(_anh);
        var mine = _reservations.GetMyReservations().Single();
        Assert.That(mine.Status, Is.EqualTo(ReservationStatus.Fulfilled));
        Assert.That(mine.IsActive, Is.False);
        Assert.That(_copy.Status, Is.EqualTo(CopyStatus.OnLoan));
    }

    [Test]
    public void MyReservationsListActiveOnesFirst()
    {
        var other = new Title(null, "Clean Code", null, null, 2008, null);
        _db.Titles.Add(other);
        var otherCopy = new Copy(other.Id, "LIB0011", "Shelf SE-01");
        _db.Copies.Add(otherCopy);
        Lend(_copy, _chi);
        Lend(otherCopy, _chi);
        var old = Reserve(_anh, _title.Id);
        _clock.Now = _clock.Now.AddHours(1);
        Reserve(_anh, other.Id);
        _reservations.Cancel(old.Id);

        Assert.That(_reservations.GetMyReservations().Select(r => r.TitleName), Is.EqualTo(new[] { "Clean Code", "Refactoring" }));
        _session.SignIn(_librarian);
        Assert.Throws<AccessDeniedException>(() => _reservations.GetMyReservations());
    }

    private Member AddMember(string code, string name)
    {
        var account = new UserAccount(code, "hash", name, RoleCode.Member, _clock.Now);
        _db.UserAccounts.Add(account);
        var member = new Member(code, name, null, null, _type.Id, _clock.Today.AddMonths(-1), _clock.Today.AddYears(1));
        member.LinkAccount(account.Id);
        _db.Members.Add(member);
        return member;
    }

    private void SignIn(Member member) =>
        _session.SignIn(new CurrentUser(member.UserAccountId!.Value, member.MemberCode, member.FullName, RoleCode.Member, member.Id));

    private void Lend(Copy copy, Member member)
    {
        _session.SignIn(_librarian);
        _circulation.CheckOut(member.MemberCode, copy.Barcode);
    }

    private ELibrary.Business.Dtos.ReservationDto Reserve(Member member, int? titleId = null)
    {
        SignIn(member);
        return _reservations.Place(titleId ?? _title.Id);
    }
}
