using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Security;
using ELibrary.Business.Services;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;
using ELibrary.Tests.Support;

namespace ELibrary.Tests.Circulation;

public sealed class MemberCirculationFineTests
{
    private const string Password = "Member@123";
    private readonly CurrentUser _librarian = new(90, "staff", "Library Staff", RoleCode.Librarian, null);
    private MemoryLibrary _db = null!;
    private FixedClock _clock = null!;
    private UserSession _session = null!;
    private MemberService _members = null!;
    private CirculationService _circulation = null!;
    private FineService _fines = null!;
    private MemberDto _first = null!;
    private MemberDto _second = null!;
    private MembershipType _type = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new MemoryLibrary();
        _clock = new FixedClock();
        _session = new UserSession();
        _session.SignIn(_librarian);
        _members = new MemberService(_db, new TestHasher(), _clock, _session);
        _circulation = new CirculationService(_db, _clock, _session);
        _fines = new FineService(_db, _clock, _session);
        _type = new MembershipType("Student", 3, 14, 1);
        _db.MembershipTypes.Add(_type);
        _db.FinePolicies.Add(FinePolicy.CreateDefault());
        _first = Register("Nguyễn Minh Anh");
        _second = Register("Trần Bảo");
    }

    [Test]
    public void RegisterCreatesLinkedHashedLoginAndUniqueCode()
    {
        Assert.That(_first.MemberCode, Is.EqualTo("M0001"));
        Assert.That(_second.MemberCode, Is.EqualTo("M0002"));
        var stored = _db.Members.GetById(_first.Id)!;
        var account = _db.UserAccounts.GetById(stored.UserAccountId!.Value)!;
        Assert.Multiple(() =>
        {
            Assert.That(account.Username, Is.EqualTo(stored.MemberCode));
            Assert.That(account.PasswordHash, Is.EqualTo(new TestHasher().Hash(Password)));
            Assert.That(account.Role, Is.EqualTo(RoleCode.Member));
        });
    }

    [TestCase("", null, null)]
    [TestCase("Valid name", "invalid-email", null)]
    [TestCase("Valid name", null, "call me")]
    public void InvalidMemberInputCreatesNeitherMemberNorLogin(string name, string? email, string? phone)
    {
        Assert.Throws<ValidationException>(() =>
            _members.Register(new MemberInput(name, email, phone, _type.Id, _clock.Today), Password));
        Assert.That(_db.Members.Query().Count(), Is.EqualTo(2));
        Assert.That(_db.UserAccounts.Query().Count(), Is.EqualTo(2));
    }

    [Test]
    public void MemberEditExtensionAndDeactivationKeepLinkedAccountAndHistory()
    {
        var longer = _first.CardExpiryDate.AddMonths(1);
        _members.ExtendCard(_first.Id, longer);
        var input = new MemberInput("Minh Anh", "ma@example.edu", "0901234567", _type.Id, longer);
        var updated = _members.Update(_first.Id, input);
        _members.SetActive(_first.Id, false);
        var account = _db.UserAccounts.GetById(_db.Members.GetById(updated.Id)!.UserAccountId!.Value)!;

        Assert.That(_members.Search("MA@EXAMPLE").Single().FullName, Is.EqualTo(account.FullName));
        Assert.That(updated.CardExpiryDate, Is.EqualTo(longer));
        Assert.That(account.IsLocked, Is.True);
        Assert.That(_members.GetMember(_first.Id).IsActive, Is.False);
        _members.SetActive(_first.Id, true);
        Assert.That(account.IsLocked, Is.False);
    }

    [Test]
    public void CheckoutMovesDueDatePastSundayAndHoliday()
    {
        _clock.Now = new DateTime(2026, 11, 1, 10, 0, 0);
        _db.Holidays.Add(new Holiday(new DateOnly(2026, 11, 16), "Library closure"));
        var copy = AddCopy("BOOK-1");
        var loan = _circulation.CheckOut(_first.MemberCode.ToLowerInvariant(), "book-1");
        Assert.That(loan.DueDate, Is.EqualTo(new DateOnly(2026, 11, 17)));
        Assert.That(copy.Status, Is.EqualTo(CopyStatus.OnLoan));
    }

    [Test]
    public void CheckoutAllowsLimitAndRejectsNextCopy()
    {
        for (var i = 1; i <= 3; i++)
        {
            _circulation.CheckOut(_first.MemberCode, AddCopy($"BOOK-{i}").Barcode);
        }

        var extra = AddCopy("BOOK-4");
        Assert.That(Assert.Throws<BusinessRuleException>(() => _circulation.CheckOut(_first.MemberCode, extra.Barcode))!.Rule,
            Is.EqualTo("BR1"));
        Assert.That(extra.Status, Is.EqualTo(CopyStatus.Available));
        Assert.That(_circulation.GetOpenLoans(_first.MemberCode), Has.Count.EqualTo(3));
    }

    [TestCase(50_000, true)]
    [TestCase(50_001, false)]
    public void CheckoutUsesStrictDebtThreshold(int debt, bool allowed)
    {
        AddRecordedFine(_first.Id, debt);
        var copy = AddCopy("BOOK-1");
        if (allowed)
        {
            Assert.That(_circulation.CheckOut(_first.MemberCode, copy.Barcode).Id, Is.Positive);
        }
        else
        {
            Assert.That(Assert.Throws<BusinessRuleException>(() => _circulation.CheckOut(_first.MemberCode, copy.Barcode))!.Rule,
                Is.EqualTo("BR2"));
        }
    }

    [TestCase(90, false)]
    [TestCase(91, true)]
    public void ExpiryDateIsInclusive(int daysAfterRegistration, bool blocked)
    {
        _clock.Now = _clock.Now.AddDays(daysAfterRegistration);
        var summary = _circulation.GetMemberSummary(_first.MemberCode);
        Assert.That(summary.BorrowingBlock != null, Is.EqualTo(blocked));
    }

    [Test]
    public void AccruingFineCanBlockCheckout()
    {
        _circulation.CheckOut(_first.MemberCode, AddCopy("BOOK-1").Barcode);
        _clock.Now = _clock.Now.AddDays(40);
        Assert.That(_fines.GetMemberFines(_first.MemberCode).AccruingBalance, Is.EqualTo(100_000m));
        Assert.That(Assert.Throws<BusinessRuleException>(() =>
            _circulation.CheckOut(_first.MemberCode, AddCopy("BOOK-2").Barcode))!.Rule, Is.EqualTo("BR2"));
    }

    [TestCase(14, 0, 0)]
    [TestCase(22, 6, 30_000)]
    [TestCase(42, 23, 100_000)]
    public void ReturnCountsOpeningDaysAndAppliesCap(int elapsed, int days, int charge)
    {
        var copy = AddCopy("BOOK-1");
        var loan = _circulation.CheckOut(_first.MemberCode, copy.Barcode);
        _db.Holidays.Add(new Holiday(new DateOnly(2026, 11, 23), "Library closure"));
        _clock.Now = _clock.Now.AddDays(elapsed);
        var receipt = _circulation.Return(copy.Barcode, loan.Id);
        Assert.Multiple(() =>
        {
            Assert.That(receipt.OverdueDays, Is.EqualTo(days));
            Assert.That(receipt.Fine, Is.EqualTo(charge));
            Assert.That(copy.Status, Is.EqualTo(CopyStatus.Available));
            Assert.That(_db.Fines.Query().Count(), Is.EqualTo(charge > 0 ? 1 : 0));
        });
        Assert.Throws<NotFoundException>(() => _circulation.Return(copy.Barcode));
    }

    [Test]
    public void ReturnRefusesAStaleConfirmation()
    {
        var copy = AddCopy("BOOK-1");
        var loan = _circulation.CheckOut(_first.MemberCode, copy.Barcode);
        _circulation.Return(copy.Barcode, loan.Id);
        _circulation.CheckOut(_second.MemberCode, copy.Barcode);

        Assert.Throws<BusinessRuleException>(() => _circulation.Return(copy.Barcode, loan.Id));
        Assert.That(_circulation.GetOpenLoans().Single().MemberId, Is.EqualTo(_second.Id));
    }

    [Test]
    public void ReturnAssignsFirstWaitingMemberAndProtectsTheirHold()
    {
        var third = Register("Third reader");
        var copy = AddCopy("BOOK-1");
        _circulation.CheckOut(_first.MemberCode, copy.Barcode);
        var later = new Reservation(copy.TitleId, third.Id, _clock.Now.AddMinutes(2));
        var first = new Reservation(copy.TitleId, _second.Id, _clock.Now.AddMinutes(1));
        _db.Reservations.Add(later);
        _db.Reservations.Add(first);
        _clock.Now = new DateTime(2026, 11, 6, 10, 0, 0);
        _db.Holidays.Add(new Holiday(new DateOnly(2026, 11, 9), "Library closure"));

        var receipt = _circulation.Return(copy.Barcode);
        Assert.That(receipt.HeldForMemberCode, Is.EqualTo(_second.MemberCode));
        Assert.That(receipt.HoldExpiresOn, Is.EqualTo(new DateOnly(2026, 11, 11)));
        Assert.That(later.Status, Is.EqualTo(ReservationStatus.Waiting));
        Assert.That(Assert.Throws<BusinessRuleException>(() => _circulation.CheckOut(third.MemberCode, copy.Barcode))!.Rule, Is.EqualTo("BR6"));
        _clock.Now = new DateTime(2026, 11, 11, 17, 0, 0);
        _circulation.CheckOut(_second.MemberCode, copy.Barcode);
        Assert.That(first.Status, Is.EqualTo(ReservationStatus.Fulfilled));
    }

    [Test]
    public void CheckoutHandsAnExpiredHoldToNextWaitingMember()
    {
        var third = Register("Third reader");
        var copy = AddCopy("BOOK-1");
        var first = new Reservation(copy.TitleId, _second.Id, _clock.Now.AddDays(-4));
        first.PlaceHold(copy.Id, _clock.Today.AddDays(-1));
        copy.PlaceOnHold();
        var next = new Reservation(copy.TitleId, third.Id, _clock.Now.AddDays(-2));
        _db.Reservations.Add(first);
        _db.Reservations.Add(next);
        _circulation.CheckOut(third.MemberCode, copy.Barcode);
        Assert.That(first.Status, Is.EqualTo(ReservationStatus.Expired));
        Assert.That(next.Status, Is.EqualTo(ReservationStatus.Fulfilled));
    }

    [Test]
    public void MemberCanRenewOwnLoanOnceFromExistingDueDate()
    {
        var loan = _circulation.CheckOut(_first.MemberCode, AddCopy("BOOK-1").Barcode);
        SignInAs(_first);
        var renewed = _circulation.Renew(loan.Id);
        Assert.That(renewed.DueDate, Is.EqualTo(loan.DueDate.AddDays(14)));
        Assert.That(renewed.RenewalCount, Is.EqualTo(1));
        Assert.That(Assert.Throws<BusinessRuleException>(() => _circulation.Renew(loan.Id))!.Rule, Is.EqualTo("BR7"));
    }

    [TestCase("overdue", "BR7")]
    [TestCase("overdue with a fine above the limit", "BR7")]
    [TestCase("another member waiting", "BR7")]
    [TestCase("debt", "BR2")]
    [TestCase("returned", "BR7")]
    public void RenewalRejectsIneligibleLoan(string reason, string expectedRule)
    {
        var copy = AddCopy("BOOK-1");
        var loan = _circulation.CheckOut(_first.MemberCode, copy.Barcode);
        switch (reason)
        {
            case "overdue":
                _clock.Now = _clock.Now.AddDays(15);
                break;
            case "overdue with a fine above the limit":
                // Bug found in manual testing: the accruing fine (BR2) was reported instead of "overdue" (BR7).
                _clock.Now = _clock.Now.AddDays(40);
                break;
            case "another member waiting":
                _db.Reservations.Add(new Reservation(copy.TitleId, _second.Id, _clock.Now));
                break;
            case "debt":
                AddRecordedFine(_first.Id, 60_000);
                break;
            case "returned":
                _circulation.Return(copy.Barcode);
                break;
        }

        var exception = Assert.Throws<BusinessRuleException>(() => _circulation.Renew(loan.Id));
        Assert.That(exception!.Rule, Is.EqualTo(expectedRule));
        Assert.That(_db.Loans.GetById(loan.Id)!.RenewalCount, Is.Zero);
    }

    [Test]
    public void MemberCanOnlyReadAndRenewOwnRecords()
    {
        _circulation.CheckOut(_first.MemberCode, AddCopy("BOOK-1").Barcode);
        var other = _circulation.CheckOut(_second.MemberCode, AddCopy("BOOK-2").Barcode);
        AddRecordedFine(_first.Id, 10_000);
        AddRecordedFine(_second.Id, 20_000);
        SignInAs(_first);

        Assert.That(_circulation.GetMyLoans().Single().MemberId, Is.EqualTo(_first.Id));
        Assert.That(_fines.GetMyFines().RecordedBalance, Is.EqualTo(10_000));
        Assert.Throws<AccessDeniedException>(() => _circulation.Renew(other.Id));
        Assert.Throws<AccessDeniedException>(() => _circulation.CheckOut(_first.MemberCode, "BOOK-3"));
        Assert.Throws<AccessDeniedException>(() => _circulation.Return("BOOK-1"));
        Assert.Throws<AccessDeniedException>(() => _fines.GetMemberFines(_second.MemberCode));
        Assert.Throws<AccessDeniedException>(() => _fines.RecordPayment(_first.MemberCode, 1_000m));
        Assert.Throws<AccessDeniedException>(() => _members.GetMember(_second.Id));
    }

    [Test]
    public void PartialPaymentPaysOldestFineFirstAndUnblocksMember()
    {
        var newest = AddRecordedFine(_first.Id, 40_000, _clock.Now.AddDays(-1));
        var oldest = AddRecordedFine(_first.Id, 30_000, _clock.Now.AddDays(-2));
        var payment = _fines.RecordPayment(_first.MemberCode, 35_000);
        Assert.Multiple(() =>
        {
            Assert.That(oldest.AmountPaid, Is.EqualTo(30_000));
            Assert.That(newest.AmountPaid, Is.EqualTo(5_000));
            Assert.That(payment.RecordedBalance, Is.EqualTo(35_000));
            Assert.That(payment.BorrowingBlock, Is.Null);
            Assert.That(_db.Payments.Query().Select(p => p.ReceivedById).Distinct().Single(), Is.EqualTo(_librarian.AccountId));
        });
        Assert.That(_fines.RecordPayment(_first.MemberCode, 35_000).RecordedBalance, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(-100)]
    [TestCase(10_001)]
    public void InvalidPaymentDoesNotChangeBalance(int amount)
    {
        var fine = AddRecordedFine(_first.Id, 10_000);
        Assert.Throws<ValidationException>(() => _fines.RecordPayment(_first.MemberCode, amount));
        Assert.That(fine.AmountPaid, Is.Zero);
        Assert.That(_db.Payments.Query(), Is.Empty);
    }

    [Test]
    public void AccruingFineIsVisibleButPayableAfterReturn()
    {
        var copy = AddCopy("BOOK-1");
        _circulation.CheckOut(_first.MemberCode, copy.Barcode);
        _clock.Now = _clock.Now.AddDays(20);
        Assert.That(_fines.GetMemberFines(_first.MemberCode).AccruingBalance, Is.Positive);
        Assert.Throws<ValidationException>(() => _fines.RecordPayment(_first.MemberCode, 5_000m));
        var receipt = _circulation.Return(copy.Barcode);
        Assert.That(_fines.GetMemberFines(_first.MemberCode).RecordedBalance, Is.EqualTo(receipt.Fine));
        Assert.That(_fines.RecordPayment(_first.MemberCode, receipt.Fine).TotalBalance, Is.Zero);
    }

    private MemberDto Register(string name) =>
        _members.Register(new MemberInput(name, null, null, _type.Id, _clock.Today.AddDays(90)), Password);

    private Copy AddCopy(string barcode)
    {
        var title = new Title(null, barcode, null, null, null, null);
        _db.Titles.Add(title);
        var copy = new Copy(title.Id, barcode, "Shelf 1");
        _db.Copies.Add(copy);
        return copy;
    }

    private Fine AddRecordedFine(int memberId, decimal amount, DateTime? createdAt = null)
    {
        var copy = AddCopy($"PAST-{_db.Fines.Query().Count()}");
        var loan = new Loan(copy.Id, memberId, _clock.Now.AddDays(-30), _clock.Today.AddDays(-16), _librarian.AccountId);
        loan.Close(_clock.Now.AddDays(-2), _librarian.AccountId);
        _db.Loans.Add(loan);
        var fine = new Fine(loan.Id, memberId, 10, amount, createdAt ?? _clock.Now);
        _db.Fines.Add(fine);
        return fine;
    }

    private void SignInAs(MemberDto member)
    {
        var accountId = _db.Members.GetById(member.Id)!.UserAccountId!.Value;
        _session.SignIn(new CurrentUser(accountId, member.MemberCode, member.FullName, RoleCode.Member, member.Id));
    }

    private sealed class TestHasher : IPasswordHasher
    {
        public string Hash(string password) =>
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)));

        public bool Verify(string password, string storedHash) => Hash(password) == storedHash;
    }
}
