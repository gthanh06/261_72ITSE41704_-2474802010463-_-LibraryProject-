using ClosedXML.Excel;
using ELibrary.Business.Reports;

namespace ELibrary.WinForms.Export;

/// <summary>FR-18: exports a report to an Excel workbook (ClosedXML, MIT licence).</summary>
internal sealed class ExcelReportExporter : IReportExporter
{
    private const int HeaderRow = 5;

    public string FormatName => "Excel";

    public string FileFilter => "Excel workbook (*.xlsx)|*.xlsx";

    public void Export(ReportTable table, Stream output)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        sheet.Cell(1, 1).Value = table.Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = table.Subtitle;
        sheet.Cell(3, 1).Value = "Generated " + table.GeneratedAt.ToString("dd/MM/yyyy HH:mm");

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var header = sheet.Cell(HeaderRow, c + 1);
            header.Value = table.Columns[c].Header;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Columns.Count; c++)
            {
                WriteCell(sheet.Cell(HeaderRow + 1 + r, c + 1), table.Rows[r][c]);
            }
        }

        // Size the columns to the table only; the long report title in A1 must not widen column A.
        sheet.Columns().AdjustToContents(HeaderRow, HeaderRow + table.Rows.Count);
        workbook.SaveAs(output);
    }

    private static void WriteCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                cell.Value = text;
                break;
            case int number:
                cell.Value = (double)number;
                break;
            case decimal amount:
                cell.Value = (double)amount;
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd/MM/yyyy";
                break;
            case DateTime time:
                cell.Value = time;
                cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}
