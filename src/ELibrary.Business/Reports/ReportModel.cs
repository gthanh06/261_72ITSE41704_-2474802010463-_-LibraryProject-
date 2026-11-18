using ELibrary.Business.Abstractions;
using ELibrary.Business.Internal;

namespace ELibrary.Business.Reports;

/// <summary>Parameters a report may use. Reports ignore what they do not need.</summary>
public sealed record ReportRequest(DateOnly? From = null, DateOnly? To = null, int Top = 10);

public enum ReportValueKind
{
    Text,
    Number,
    Money,
    Date
}

public sealed record ReportColumn(string Header, ReportValueKind Kind = ReportValueKind.Text);

/// <summary>The output of every report: a title, columns and rows. Exporters only need this shape.</summary>
public sealed class ReportTable
{
    public ReportTable(string title, string subtitle, IReadOnlyList<ReportColumn> columns, IReadOnlyList<object?[]> rows, DateTime generatedAt)
    {
        Title = title;
        Subtitle = subtitle;
        Columns = columns;
        Rows = rows;
        GeneratedAt = generatedAt;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public IReadOnlyList<ReportColumn> Columns { get; }

    /// <summary>Cell values: string, int, decimal, DateOnly or DateTime, matching <see cref="ReportColumn.Kind"/>.</summary>
    public IReadOnlyList<object?[]> Rows { get; }

    public DateTime GeneratedAt { get; }
}

/// <summary>What a report needs to run.</summary>
public sealed class ReportContext
{
    internal ReportContext(IUnitOfWork uow, LibraryRules rules, DateTime now)
    {
        Uow = uow;
        Rules = rules;
        Now = now;
    }

    public IUnitOfWork Uow { get; }

    internal LibraryRules Rules { get; }

    public DateTime Now { get; }

    public DateOnly Today => DateOnly.FromDateTime(Now);
}

/// <summary>
/// Strategy: every report is one class. Adding a report means adding a class that implements this
/// interface and registering it in <see cref="ReportService.DefaultReports"/>; no screen changes.
/// </summary>
public interface IReport
{
    string Key { get; }

    string Name { get; }

    bool UsesDateRange { get; }

    ReportTable Run(ReportContext context, ReportRequest request);
}

/// <summary>Strategy for the export format (FR-18). Excel lives in the presentation layer; CSV is here.</summary>
public interface IReportExporter
{
    string FormatName { get; }

    /// <summary>File dialog filter, e.g. "Excel workbook (*.xlsx)|*.xlsx".</summary>
    string FileFilter { get; }

    void Export(ReportTable table, Stream output);
}
