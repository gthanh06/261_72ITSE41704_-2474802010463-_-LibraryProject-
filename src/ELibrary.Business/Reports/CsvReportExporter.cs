using System.Globalization;
using System.Text;

namespace ELibrary.Business.Reports;

/// <summary>Exports a report as CSV (UTF-8 with BOM so that Excel shows Vietnamese characters correctly).</summary>
public sealed class CsvReportExporter : IReportExporter
{
    public string FormatName => "CSV";

    public string FileFilter => "CSV file (*.csv)|*.csv";

    public void Export(ReportTable table, Stream output)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        writer.WriteLine(string.Join(",", table.Columns.Select(c => Escape(c.Header))));
        foreach (var row in table.Rows)
        {
            writer.WriteLine(string.Join(",", row.Select(v => Escape(FormatValue(v)))));
        }
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime t => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
