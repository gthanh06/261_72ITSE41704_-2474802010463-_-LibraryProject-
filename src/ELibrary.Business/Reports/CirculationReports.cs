using ELibrary.Business.Internal;
using ELibrary.Domain.Enums;

namespace ELibrary.Business.Reports;

/// <summary>All items currently on loan.</summary>
public sealed class CurrentLoansReport : IReport
{
    public string Key => "current-loans";

    public string Name => "Current loans";

    public bool UsesDateRange => false;

    public ReportTable Run(ReportContext context, ReportRequest request)
    {
        var loans = context.Uow.Loans.Query().Where(l => l.ReturnedAt == null).OrderBy(l => l.DueDate).ToList();
        var dtos = new DtoProjector(context.Uow, context.Rules, context.Today).ToLoanDtos(loans);
        var rows = dtos.Select(l => new object?[]
        {
            l.MemberCode, l.MemberName, l.TitleName, l.Barcode, DateOnly.FromDateTime(l.LoanDate), l.DueDate,
            l.RenewalCount, l.IsOverdue ? "Overdue" : "On time"
        }).ToList();

        return new ReportTable(Name, $"{rows.Count} item(s) on loan on {context.Today:dd/MM/yyyy}",
            new[]
            {
                new ReportColumn("Member code"), new ReportColumn("Member"), new ReportColumn("Title"), new ReportColumn("Barcode"),
                new ReportColumn("Loan date", ReportValueKind.Date), new ReportColumn("Due date", ReportValueKind.Date),
                new ReportColumn("Renewals", ReportValueKind.Number), new ReportColumn("Status")
            },
            rows, context.Now);
    }
}

/// <summary>Items past their due date with the fine accrued to date.</summary>
public sealed class OverdueItemsReport : IReport
{
    public string Key => "overdue";

    public string Name => "Overdue items";

    public bool UsesDateRange => false;

    public ReportTable Run(ReportContext context, ReportRequest request)
    {
        var today = context.Today;
        var loans = context.Uow.Loans.Query().Where(l => l.ReturnedAt == null && l.DueDate < today).OrderBy(l => l.DueDate).ToList();
        var dtos = new DtoProjector(context.Uow, context.Rules, today).ToLoanDtos(loans);
        var memberIds = dtos.Select(d => d.MemberId).Distinct().ToList();
        var phones = context.Uow.Members.Query().Where(m => memberIds.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Phone);

        var rows = dtos.Select(l => new object?[]
        {
            l.MemberCode, l.MemberName, phones[l.MemberId], l.TitleName, l.Barcode, l.DueDate, l.ChargeableOverdueDays, l.FineToDate
        }).ToList();

        return new ReportTable(Name, $"{rows.Count} overdue item(s) on {today:dd/MM/yyyy}",
            new[]
            {
                new ReportColumn("Member code"), new ReportColumn("Member"), new ReportColumn("Phone"), new ReportColumn("Title"),
                new ReportColumn("Barcode"), new ReportColumn("Due date", ReportValueKind.Date),
                new ReportColumn("Chargeable days", ReportValueKind.Number), new ReportColumn("Fine to date", ReportValueKind.Money)
            },
            rows, context.Now);
    }
}

/// <summary>Members who owe money: recorded unpaid fines plus fines accruing on overdue loans.</summary>
public sealed class OutstandingFinesReport : IReport
{
    public string Key => "outstanding-fines";

    public string Name => "Outstanding fines";

    public bool UsesDateRange => false;

    public ReportTable Run(ReportContext context, ReportRequest request)
    {
        var uow = context.Uow;
        var today = context.Today;
        var ledger = new FineLedger(uow, context.Rules);

        var recorded = uow.Fines.Query().Where(f => f.AmountPaid < f.Amount).ToList()
            .GroupBy(f => f.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.Balance));
        var accrued = uow.Loans.Query().Where(l => l.ReturnedAt == null && l.DueDate < today).ToList()
            .GroupBy(l => l.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(l => ledger.GetFineToDate(l, today)));

        var memberIds = recorded.Keys.Union(accrued.Keys).ToList();
        var members = uow.Members.Query().Where(m => memberIds.Contains(m.Id)).ToList();
        var policy = context.Rules.Policy;

        var rows = members
            .Select(m =>
            {
                var r = recorded.GetValueOrDefault(m.Id);
                var a = accrued.GetValueOrDefault(m.Id);
                return new { Member = m, Recorded = r, Accrued = a, Total = r + a };
            })
            .Where(x => x.Total > 0)
            .OrderByDescending(x => x.Total)
            .Select(x => new object?[]
            {
                x.Member.MemberCode, x.Member.FullName, x.Recorded, x.Accrued, x.Total,
                policy.IsAboveDebtThreshold(x.Total) ? "Blocked (BR2)" : "Can borrow"
            })
            .ToList();

        var sum = rows.Sum(r => (decimal)r[4]!);
        return new ReportTable(Name, $"{rows.Count} member(s), {Domain.Common.Money.Format(sum)} in total",
            new[]
            {
                new ReportColumn("Member code"), new ReportColumn("Member"),
                new ReportColumn("Recorded unpaid", ReportValueKind.Money), new ReportColumn("Accruing", ReportValueKind.Money),
                new ReportColumn("Total", ReportValueKind.Money), new ReportColumn("Status")
            },
            rows, context.Now);
    }
}

/// <summary>Most-borrowed titles in a period (default: the last 12 months).</summary>
public sealed class TopBorrowedTitlesReport : IReport
{
    public string Key => "top-titles";

    public string Name => "Most-borrowed titles";

    public bool UsesDateRange => true;

    public ReportTable Run(ReportContext context, ReportRequest request)
    {
        var uow = context.Uow;
        var to = request.To ?? context.Today;
        var from = request.From ?? to.AddYears(-1);
        if (from > to)
        {
            throw new Domain.Exceptions.ValidationException("From", "The start date must not be after the end date.");
        }

        var top = Math.Clamp(request.Top, 1, 100);
        var fromTime = from.ToDateTime(TimeOnly.MinValue);
        var toTime = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var copyIds = uow.Loans.Query().Where(l => l.LoanDate >= fromTime && l.LoanDate < toTime).Select(l => l.CopyId).ToList();
        var distinctCopyIds = copyIds.Distinct().ToList();
        var copyTitle = uow.Copies.Query().Where(c => distinctCopyIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.TitleId);
        var counts = copyIds.GroupBy(id => copyTitle[id]).Select(g => new { TitleId = g.Key, Loans = g.Count() })
            .OrderByDescending(x => x.Loans).Take(top).ToList();

        var titleIds = counts.Select(c => c.TitleId).ToList();
        var titles = uow.Titles.Query().Where(t => titleIds.Contains(t.Id)).ToDictionary(t => t.Id);
        var categoryNames = uow.Categories.Query().ToDictionary(c => c.Id, c => c.Name);

        var rank = 0;
        var rows = counts.Select(c =>
        {
            var title = titles[c.TitleId];
            return new object?[]
            {
                ++rank, title.Name, title.CategoryId.HasValue ? categoryNames.GetValueOrDefault(title.CategoryId.Value) : null, c.Loans
            };
        }).ToList();

        return new ReportTable(Name, $"Top {top} from {from:dd/MM/yyyy} to {to:dd/MM/yyyy}",
            new[]
            {
                new ReportColumn("Rank", ReportValueKind.Number), new ReportColumn("Title"),
                new ReportColumn("Category"), new ReportColumn("Loans", ReportValueKind.Number)
            },
            rows, context.Now);
    }
}
