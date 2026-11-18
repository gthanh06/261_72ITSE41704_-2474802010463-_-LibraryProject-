using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Services;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Services;

namespace ELibrary.DataAccess.Seeding;

/// <summary>
/// Fills a new database with realistic sample data (FR seed requirement: 50–200 records).
/// Except for the very first rows (roles, policy, administrator), all data is created by calling the
/// real business services with a clock that is moved through the past year — so every sample loan,
/// fine, reservation and hold obeys BR1–BR7 exactly as if librarians had entered it.
///
/// Sample accounts: admin / Admin@123 · librarian1, librarian2 / Librarian@123 · M0001…M0020 / Member@123
/// </summary>
public sealed class SampleDataSeeder
{
    public const string AdminPassword = "Admin@123";
    public const string LibrarianPassword = "Librarian@123";
    public const string MemberPassword = "Member@123";

    private readonly IUnitOfWorkFactory _factory;
    private readonly IPasswordHasher _hasher;
    private readonly IDatabaseMaintenance _maintenance;
    private readonly SeedClock _clock;
    private readonly UserSession _session = new();
    private readonly DateOnly _today;
    private readonly Dictionary<string, (int MemberId, int AccountId, string Name)> _members = new();
    private readonly Dictionary<int, int> _titleIdByNumber = new();
    private LibraryCalendar _calendar = new(Array.Empty<DateOnly>());
    private CurrentUser? _admin;
    private CurrentUser? _librarian;

    /// <param name="now">"Today" for the sample data; all sample dates are relative to it.</param>
    public SampleDataSeeder(IUnitOfWorkFactory factory, IPasswordHasher hasher, IDatabaseMaintenance maintenance, DateTime now)
    {
        _factory = factory;
        _hasher = hasher;
        _maintenance = maintenance;
        _clock = new SeedClock(now);
        _today = DateOnly.FromDateTime(now);
    }

    private SettingsService Settings => new(_factory, _session, _maintenance);
    private AccountService Accounts => new(_factory, _hasher, _clock, _session);
    private CatalogueService Catalogue => new(_factory, _session, _clock);
    private MemberService MemberService => new(_factory, _hasher, _clock, _session);
    private CirculationService Circulation => new(_factory, _clock, _session);
    private ReservationService Reservations => new(_factory, _clock, _session);
    private FineService Fines => new(_factory, _clock, _session);

    public void Seed()
    {
        SeedFoundation();
        SeedSettingsAndStaff();
        SeedCatalogue();
        SeedMembers();
        SeedHistory();
        SeedCurrentSituation();
        _session.SignOut();
    }

    // ---- 1. Rows that must exist before anyone can log in --------------------------------------

    private void SeedFoundation()
    {
        using var uow = _factory.Create();
        uow.Roles.Add(new Role(RoleCode.Administrator, "Administrator"));
        uow.Roles.Add(new Role(RoleCode.Librarian, "Librarian"));
        uow.Roles.Add(new Role(RoleCode.Member, "Member"));
        uow.FinePolicies.Add(FinePolicy.CreateDefault());
        var admin = new UserAccount("admin", _hasher.Hash(AdminPassword), "Nguyễn Văn Quản", RoleCode.Administrator, _clock.Now);
        uow.UserAccounts.Add(admin);
        uow.SaveChanges();
        _admin = new CurrentUser(admin.Id, admin.Username, admin.FullName, RoleCode.Administrator, null);
    }

    // ---- 2. Administrator: membership types, holidays, librarians -------------------------------

    private void SeedSettingsAndStaff()
    {
        _session.SignIn(_admin!);
        var settings = Settings;
        settings.SaveMembershipType(new MembershipTypeDto(0, "Student", 3, 14, 1, true));
        settings.SaveMembershipType(new MembershipTypeDto(0, "Lecturer / Staff", 5, 28, 2, true));
        settings.SaveMembershipType(new MembershipTypeDto(0, "Community (public)", 2, 7, 0, true));

        // Public holidays (the Administrator should check them against the official announcement each year).
        var holidays = new (DateOnly Date, string Name)[]
        {
            (new(2026, 1, 1), "Tết Dương lịch"),
            (new(2026, 2, 16), "Tết Nguyên Đán"), (new(2026, 2, 17), "Tết Nguyên Đán"), (new(2026, 2, 18), "Tết Nguyên Đán"),
            (new(2026, 2, 19), "Tết Nguyên Đán"), (new(2026, 2, 20), "Tết Nguyên Đán"),
            (new(2026, 4, 30), "Ngày Giải phóng miền Nam"), (new(2026, 5, 1), "Quốc tế Lao động"),
            (new(2026, 9, 1), "Quốc khánh"), (new(2026, 9, 2), "Quốc khánh"),
            (new(2027, 1, 1), "Tết Dương lịch"),
            (new(2027, 2, 5), "Tết Nguyên Đán"), (new(2027, 2, 6), "Tết Nguyên Đán"), (new(2027, 2, 8), "Tết Nguyên Đán"),
            (new(2027, 2, 9), "Tết Nguyên Đán"), (new(2027, 2, 10), "Tết Nguyên Đán"),
            (new(2027, 4, 30), "Ngày Giải phóng miền Nam"), (new(2027, 5, 1), "Quốc tế Lao động"),
            (new(2027, 9, 2), "Quốc khánh")
        };
        foreach (var (date, name) in holidays)
        {
            settings.AddHoliday(date, name);
        }

        _calendar = new LibraryCalendar(holidays.Select(h => h.Date), closedOnSundays: true);

        var librarianId = Accounts.CreateStaffAccount("librarian1", "Trần Thị Thu", RoleCode.Librarian, LibrarianPassword);
        Accounts.CreateStaffAccount("librarian2", "Lê Hoàng Nam", RoleCode.Librarian, LibrarianPassword);
        _librarian = new CurrentUser(librarianId, "librarian1", "Trần Thị Thu", RoleCode.Librarian, null);
    }

    // ---- 3. Librarian: catalogue (30 titles, 50 copies, e-resource links) ------------------------

    private sealed record SampleTitle(int Number, string? Isbn, string Name, string[] Authors, string Category,
        string Publisher, int Year, int Copies, string Shelf);

    private static readonly SampleTitle[] SampleTitles =
    {
        new(1, "9780132350884", "Clean Code: A Handbook of Agile Software Craftsmanship", new[] { "Robert C. Martin" }, "Software Engineering", "Prentice Hall", 2008, 3, "SE-01"),
        new(2, "9780135957059", "The Pragmatic Programmer (20th Anniversary Edition)", new[] { "David Thomas", "Andrew Hunt" }, "Software Engineering", "Addison-Wesley", 2019, 2, "SE-01"),
        new(3, "9780201633610", "Design Patterns: Elements of Reusable Object-Oriented Software", new[] { "Erich Gamma", "Richard Helm", "Ralph Johnson", "John Vlissides" }, "Software Engineering", "Addison-Wesley", 1994, 2, "SE-02"),
        new(4, "9780134757599", "Refactoring: Improving the Design of Existing Code (2nd Edition)", new[] { "Martin Fowler" }, "Software Engineering", "Addison-Wesley", 2018, 1, "SE-02"),
        new(5, "9780262033848", "Introduction to Algorithms (3rd Edition)", new[] { "Thomas H. Cormen", "Charles E. Leiserson", "Ronald L. Rivest", "Clifford Stein" }, "Computer Science", "MIT Press", 2009, 2, "CS-01"),
        new(6, "9780262046305", "Introduction to Algorithms (4th Edition)", new[] { "Thomas H. Cormen", "Charles E. Leiserson", "Ronald L. Rivest", "Clifford Stein" }, "Computer Science", "MIT Press", 2022, 1, "CS-01"),
        new(7, "9780735619678", "Code Complete (2nd Edition)", new[] { "Steve McConnell" }, "Software Engineering", "Microsoft Press", 2004, 2, "SE-03"),
        new(8, "9780596007126", "Head First Design Patterns", new[] { "Eric Freeman", "Elisabeth Robson" }, "Software Engineering", "O'Reilly Media", 2004, 1, "SE-03"),
        new(9, "9780321125217", "Domain-Driven Design: Tackling Complexity in the Heart of Software", new[] { "Eric Evans" }, "Software Engineering", "Addison-Wesley", 2003, 1, "SE-03"),
        new(10, "9780201835953", "The Mythical Man-Month (Anniversary Edition)", new[] { "Frederick P. Brooks Jr." }, "Software Engineering", "Addison-Wesley", 1995, 1, "SE-04"),
        new(11, "9780133943030", "Software Engineering (10th Edition)", new[] { "Ian Sommerville" }, "Software Engineering", "Pearson", 2015, 2, "SE-04"),
        new(12, "9780131489066", "Applying UML and Patterns (3rd Edition)", new[] { "Craig Larman" }, "Software Engineering", "Prentice Hall", 2004, 1, "SE-04"),
        new(13, null, "Dế Mèn phiêu lưu ký", new[] { "Tô Hoài" }, "Văn học Việt Nam", "NXB Kim Đồng", 2020, 3, "VN-01"),
        new(14, null, "Số đỏ", new[] { "Vũ Trọng Phụng" }, "Văn học Việt Nam", "NXB Văn học", 2016, 2, "VN-01"),
        new(15, null, "Tắt đèn", new[] { "Ngô Tất Tố" }, "Văn học Việt Nam", "NXB Văn học", 2015, 2, "VN-01"),
        new(16, null, "Truyện Kiều", new[] { "Nguyễn Du" }, "Văn học Việt Nam", "NXB Giáo dục Việt Nam", 2018, 2, "VN-02"),
        new(17, null, "Cho tôi xin một vé đi tuổi thơ", new[] { "Nguyễn Nhật Ánh" }, "Văn học Việt Nam", "NXB Trẻ", 2018, 2, "VN-02"),
        new(18, null, "Mắt biếc", new[] { "Nguyễn Nhật Ánh" }, "Văn học Việt Nam", "NXB Trẻ", 2019, 2, "VN-02"),
        new(19, null, "Nỗi buồn chiến tranh", new[] { "Bảo Ninh" }, "Văn học Việt Nam", "NXB Trẻ", 2011, 1, "VN-03"),
        new(20, "9780062316097", "Sapiens: A Brief History of Humankind", new[] { "Yuval Noah Harari" }, "History", "Harper", 2015, 2, "HI-01"),
        new(21, "9780374275631", "Thinking, Fast and Slow", new[] { "Daniel Kahneman" }, "Psychology", "Farrar, Straus and Giroux", 2011, 1, "PS-01"),
        new(22, null, "Principles of Economics", new[] { "N. Gregory Mankiw" }, "Economics", "Cengage Learning", 2020, 2, "EC-01"),
        new(23, null, "Calculus: Early Transcendentals", new[] { "James Stewart" }, "Mathematics", "Cengage Learning", 2015, 2, "MA-01"),
        new(24, null, "Linear Algebra and Its Applications", new[] { "David C. Lay" }, "Mathematics", "Pearson", 2015, 1, "MA-01"),
        new(25, "9780078022159", "Database System Concepts (7th Edition)", new[] { "Abraham Silberschatz", "Henry F. Korth", "S. Sudarshan" }, "Computer Science", "McGraw-Hill", 2019, 2, "CS-02"),
        new(26, null, "Computer Networking: A Top-Down Approach", new[] { "James F. Kurose", "Keith W. Ross" }, "Computer Science", "Pearson", 2016, 1, "CS-02"),
        new(27, null, "Operating System Concepts", new[] { "Abraham Silberschatz", "Peter B. Galvin", "Greg Gagne" }, "Computer Science", "Wiley", 2018, 1, "CS-03"),
        new(28, null, "C# 12 and .NET 8 – Modern Cross-Platform Development", new[] { "Mark J. Price" }, "Computer Science", "Packt Publishing", 2023, 1, "CS-03"),
        new(29, null, "English Grammar in Use", new[] { "Raymond Murphy" }, "Languages", "Cambridge University Press", 2019, 3, "LA-01"),
        new(30, null, "Đắc nhân tâm", new[] { "Dale Carnegie" }, "Kỹ năng sống", "NXB Tổng hợp TP.HCM", 2016, 2, "KN-01")
    };

    /// <summary>Barcode of copy <paramref name="copy"/> (1-based) of sample title <paramref name="title"/>, e.g. LIB0011.</summary>
    private static string Barcode(int title, int copy) => $"LIB{title:D3}{copy}";

    private void SeedCatalogue()
    {
        _session.SignIn(_librarian!);
        var catalogue = Catalogue;
        foreach (var t in SampleTitles)
        {
            var id = catalogue.SaveTitle(new TitleInput(null, t.Isbn, t.Name, t.Authors, t.Publisher, t.Category, t.Year, null));
            _titleIdByNumber[t.Number] = id;
            for (var c = 1; c <= t.Copies; c++)
            {
                catalogue.AddCopy(id, Barcode(t.Number, c), "Shelf " + t.Shelf);
            }
        }

        catalogue.AddEResource(_titleIdByNumber[5], "MIT OpenCourseWare – 6.006 Introduction to Algorithms",
            "https://ocw.mit.edu/courses/6-006-introduction-to-algorithms-spring-2020/");
        catalogue.AddEResource(_titleIdByNumber[25], "Book website (slides and exercises)", "https://www.db-book.com/");
        catalogue.AddEResource(_titleIdByNumber[28], "C# documentation – Microsoft Learn", "https://learn.microsoft.com/dotnet/csharp/");
        catalogue.AddEResource(_titleIdByNumber[16], "Truyện Kiều – Wikisource", "https://vi.wikisource.org/wiki/Truy%E1%BB%87n_Ki%E1%BB%81u");
    }

    // ---- 4. Members (registered about 14 months ago) --------------------------------------------

    private static readonly (string Name, string Type)[] SampleMembers =
    {
        ("Nguyễn Minh Anh", "Student"), ("Trần Quốc Bảo", "Lecturer / Staff"), ("Lê Thị Cẩm Tú", "Student"),
        ("Phạm Hoàng Long", "Student"), ("Võ Thị Mỹ Duyên", "Student"), ("Đặng Văn Hùng", "Community (public)"),
        ("Bùi Thị Hạnh", "Student"), ("Huỳnh Gia Huy", "Student"), ("Ngô Thanh Hương", "Lecturer / Staff"),
        ("Đỗ Minh Khoa", "Student"), ("Phan Thị Lan", "Lecturer / Staff"), ("Vũ Đức Mạnh", "Student"),
        ("Trương Ngọc Nhi", "Student"), ("Lý Quang Phúc", "Community (public)"), ("Hồ Thị Quỳnh", "Student"),
        ("Mai Xuân Sơn", "Student"), ("Đinh Thu Trang", "Student"), ("Cao Minh Tuấn", "Lecturer / Staff"),
        ("Châu Ngọc Vy", "Student"), ("La Văn Xuân", "Community (public)")
    };

    private void SeedMembers()
    {
        _session.SignIn(_librarian!);
        var types = Settings.GetMembershipTypes().ToDictionary(t => t.Name, t => t.Id);
        At(420, 9);
        var service = MemberService;
        for (var i = 0; i < SampleMembers.Length; i++)
        {
            var (name, type) = SampleMembers[i];
            var number = i + 1;
            // M0007's card has expired (BR2 demo); the others are valid for about ten more months.
            var expiry = number == 7 ? _today.AddDays(-10) : _today.AddDays(300);
            var phone = "09" + (12345600 + number * 7).ToString("D8");
            var dto = service.Register(
                new MemberInput(name, $"m{number:D4}@example.edu.vn", phone, types[type], expiry), MemberPassword);
            using var uow = _factory.Create();
            var accountId = uow.UserAccounts.Query().Where(a => a.Username == dto.MemberCode).Select(a => a.Id).Single();
            _members[dto.MemberCode] = (dto.Id, accountId, dto.FullName);
        }
    }

    // ---- 5. A year of on-time loans (for the "most-borrowed titles" report) ---------------------

    private void SeedHistory()
    {
        _session.SignIn(_librarian!);
        var popular = new[]
        {
            Barcode(1, 1), Barcode(13, 1), Barcode(18, 1), Barcode(3, 1), Barcode(1, 2), Barcode(20, 1), Barcode(4, 1),
            Barcode(5, 1), Barcode(29, 1), Barcode(17, 1), Barcode(1, 3), Barcode(13, 2), Barcode(16, 1), Barcode(18, 2),
            Barcode(25, 1), Barcode(30, 1), Barcode(2, 1), Barcode(8, 1), Barcode(20, 2), Barcode(7, 1)
        };

        for (var i = 0; i < 40; i++)
        {
            var daysAgo = 400 - (7 * i);
            var member = $"M{(i % 20) + 1:D4}";
            var barcode = popular[i % popular.Length];
            At(daysAgo, 9, i % 50);
            Circulation.CheckOut(member, barcode);
            At(daysAgo - 5, 16, i % 50);
            Circulation.Return(barcode);
        }
    }

    // ---- 6. The situation "today": loans, overdue items, fines, queues and holds ----------------

    private void SeedCurrentSituation()
    {
        // Recorded fines.
        LendAt(100, "M0010", Barcode(6, 1));  // returned very late → fine above 50,000 → blocked (BR2)
        LendAt(80, "M0009", Barcode(10, 1));  // returned late → fine, then partly paid
        LendAt(60, "M0008", Barcode(24, 1));  // returned late → unpaid fine below the threshold
        ReturnAt(60, Barcode(6, 1));
        ReturnAt(44, Barcode(10, 1));
        ReturnAt(40, Barcode(24, 1));
        At(40, 15);
        _session.SignIn(_librarian!);
        // Pay about half of M0009's fine (the exact amount depends on Sundays and holidays).
        var balance = Fines.GetMemberFines("M0009").RecordedBalance;
        if (balance >= 2_000)
        {
            Fines.RecordPayment("M0009", Math.Max(1_000, Math.Floor(balance / 2_000) * 1_000));
        }

        // Open loans (some overdue).
        LendAt(40, "M0005", Barcode(20, 1));  // overdue for ~3 weeks → accrued fine blocks borrowing
        LendAt(20, "M0011", Barcode(5, 1));
        LendAt(20, "M0018", Barcode(27, 1));  // Operating System Concepts — only copy
        ReserveAt(15, "M0019", 27);
        LendAt(15, "M0013", Barcode(18, 1));  // due yesterday → 1 day overdue
        LendAt(14, "M0016", Barcode(8, 1));   // Head First Design Patterns — only copy
        ReserveAt(12, "M0020", 27);
        LendAt(12, "M0012", Barcode(25, 1));
        LendAt(10, "M0002", Barcode(4, 1));   // Refactoring — only copy, reserved by two members below
        ReturnAt(10, Barcode(27, 1));         // hold for M0019; it will have expired by today (UC11 demo)
        LendAt(9, "M0006", Barcode(29, 1));   // Community (7 days) → a few days overdue
        LendAt(7, "M0015", Barcode(7, 1));
        ReserveAt(6, "M0003", 4);
        LendAt(5, "M0001", Barcode(1, 1));
        ReserveAt(5, "M0017", 8);
        LendAt(4, "M0014", Barcode(13, 1));
        LendAt(3, "M0001", Barcode(3, 1));
        ReserveAt(2, "M0004", 4);
        ReturnAt(1, Barcode(8, 1));           // hold for M0017, ready for pickup
    }

    private void LendAt(int daysAgo, string memberCode, string barcode)
    {
        _session.SignIn(_librarian!);
        At(daysAgo, 10);
        Circulation.CheckOut(memberCode, barcode);
    }

    private void ReturnAt(int daysAgo, string barcode)
    {
        _session.SignIn(_librarian!);
        At(daysAgo, 14);
        Circulation.Return(barcode);
    }

    private void ReserveAt(int daysAgo, string memberCode, int titleNumber)
    {
        var (memberId, accountId, name) = _members[memberCode];
        _session.SignIn(new CurrentUser(accountId, memberCode, name, RoleCode.Member, memberId));
        At(daysAgo, 11);
        Reservations.Place(_titleIdByNumber[titleNumber]);
    }

    /// <summary>
    /// Moves the clock to <paramref name="daysAgo"/> days before today, or to the opening day before
    /// that if the library was closed (no circulation happens on Sundays or holidays).
    /// </summary>
    private void At(int daysAgo, int hour, int minute = 0)
    {
        var day = _today.AddDays(-daysAgo);
        while (!_calendar.IsOpeningDay(day))
        {
            day = day.AddDays(-1);
        }

        _clock.Now = day.ToDateTime(new TimeOnly(hour, minute));
    }

    private sealed class SeedClock : IClock
    {
        public SeedClock(DateTime now) => Now = now;

        public DateTime Now { get; set; }
    }
}
