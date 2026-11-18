using ELibrary.Business.Abstractions;
using ELibrary.Business.Internal;
using ELibrary.Business.Reports;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

public sealed record ReportInfoDto(string Key, string Name, bool UsesDateRange);

/// <summary>UC13 View reports (FR-17). Administrator and Librarian.</summary>
public sealed class ReportService
{
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IClock _clock;
    private readonly IUserSession _session;
    private readonly IReadOnlyList<IReport> _reports;

    public ReportService(IUnitOfWorkFactory uowFactory, IClock clock, IUserSession session, IEnumerable<IReport> reports)
    {
        _uowFactory = uowFactory;
        _clock = clock;
        _session = session;
        _reports = reports.ToList();
    }

    public static IReadOnlyList<IReport> DefaultReports() => new IReport[]
    {
        new CurrentLoansReport(),
        new OverdueItemsReport(),
        new OutstandingFinesReport(),
        new TopBorrowedTitlesReport()
    };

    public IReadOnlyList<ReportInfoDto> GetAvailableReports()
    {
        _session.Require(RoleCode.Administrator, RoleCode.Librarian);
        return _reports.Select(r => new ReportInfoDto(r.Key, r.Name, r.UsesDateRange)).ToList();
    }

    public ReportTable Run(string reportKey, ReportRequest request)
    {
        _session.Require(RoleCode.Administrator, RoleCode.Librarian);
        var report = _reports.FirstOrDefault(r => r.Key == reportKey) ?? throw new NotFoundException("Report", reportKey);
        using var uow = _uowFactory.Create();
        return report.Run(new ReportContext(uow, LibraryRules.Load(uow), _clock.Now), request);
    }
}
