using ELibrary.Business.Abstractions;
using ELibrary.Business.Reports;
using ELibrary.Business.Security;
using ELibrary.Business.Services;
using ELibrary.DataAccess;
using ELibrary.WinForms.Export;

namespace ELibrary.WinForms.Infrastructure;

/// <summary>
/// Composition root: the only place that knows which implementation of each abstraction is used
/// (EF Core + SQL Server, PBKDF2, the system clock). Screens receive this object and call services.
/// </summary>
internal sealed class AppServices
{
    public AppServices(string connectionString)
    {
        Clock = new SystemClock();
        Session = new UserSession();
        var hasher = new Pbkdf2PasswordHasher();
        var factory = new EfUnitOfWorkFactory(connectionString);

        Auth = new AuthService(factory, hasher, Clock, Session);
        Accounts = new AccountService(factory, hasher, Clock, Session);
        Settings = new SettingsService(factory, Session, new SqlServerDatabaseMaintenance(connectionString));
        Catalogue = new CatalogueService(factory, Session, Clock);
        Members = new MemberService(factory, hasher, Clock, Session);
        Circulation = new CirculationService(factory, Clock, Session);
        Reservations = new ReservationService(factory, Clock, Session);
        Fines = new FineService(factory, Clock, Session);
        Reports = new ReportService(factory, Clock, Session, ReportService.DefaultReports());
        Exporters = new IReportExporter[] { new ExcelReportExporter(), new CsvReportExporter() };
    }

    public IClock Clock { get; }
    public IUserSession Session { get; }
    public AuthService Auth { get; }
    public AccountService Accounts { get; }
    public SettingsService Settings { get; }
    public CatalogueService Catalogue { get; }
    public MemberService Members { get; }
    public CirculationService Circulation { get; }
    public ReservationService Reservations { get; }
    public FineService Fines { get; }
    public ReportService Reports { get; }
    public IReadOnlyList<IReportExporter> Exporters { get; }

    public CurrentUser User => Session.User ?? throw new InvalidOperationException("No user is logged in.");
}
