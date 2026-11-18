using ELibrary.Business.Reports;
using ELibrary.Business.Services;
using ELibrary.WinForms.Infrastructure;

namespace ELibrary.WinForms.Screens;

/// <summary>UC13 View reports (FR-17) and export them (FR-18). Each report and each format is a Strategy class.</summary>
internal sealed class ReportsScreen : ScreenBase
{
    private readonly ComboBox _report = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, DisplayMember = nameof(ReportInfoDto.Name) };
    private readonly DateTimePicker _from;
    private readonly DateTimePicker _to;
    private readonly NumericUpDown _top = Ui.Number(1, 100, 10, 1, 60);
    private readonly Label _subtitle = new() { AutoSize = true, Font = Ui.BoldFont, Padding = new Padding(0, 6, 0, 6) };
    private readonly DataGridView _grid = Ui.Grid();
    private ReportTable? _table;

    public ReportsScreen(AppServices app) : base(app, "Reports")
    {
        _from = Ui.DatePicker(app.Clock.Today.AddYears(-1));
        _to = Ui.DatePicker(app.Clock.Today);
        _report.SelectedIndexChanged += (_, _) => UpdateParameters();

        var row = Ui.Row(Ui.Label("Report"), _report, Ui.Label("From"), _from, Ui.Label("To"), _to, Ui.Label("Top"), _top,
            Ui.Button("Run report", RunReport, 110));
        var exportRow = Ui.Row(Ui.Label("Export:"));
        foreach (var exporter in app.Exporters)
        {
            exportRow.Controls.Add(Ui.Button($"Export to {exporter.FormatName}…", () => Export(exporter), 150));
        }

        Compose(_grid, row, exportRow, _subtitle);
        Run(() =>
        {
            foreach (var report in App.Reports.GetAvailableReports())
            {
                _report.Items.Add(report);
            }

            if (_report.Items.Count > 0)
            {
                _report.SelectedIndex = 0;
            }
        });
    }

    public override void RefreshData()
    {
        if (_table == null)
        {
            RunReport();
        }
    }

    private void UpdateParameters()
    {
        var usesRange = (_report.SelectedItem as ReportInfoDto)?.UsesDateRange ?? false;
        _from.Enabled = _to.Enabled = _top.Enabled = usesRange;
    }

    private void RunReport()
    {
        if (_report.SelectedItem is not ReportInfoDto info)
        {
            return;
        }

        var request = new ReportRequest(Ui.ToDateOnly(_from), Ui.ToDateOnly(_to), (int)_top.Value);
        Run(() =>
        {
            _table = App.Reports.Run(info.Key, request);
            ShowTable(_table);
            SetStatus($"Generated at {Ui.DateTime(_table.GeneratedAt)}.");
        });
    }

    /// <summary>The report's columns are not known in advance, so the grid is built from the table.</summary>
    private void ShowTable(ReportTable table)
    {
        _subtitle.Text = $"{table.Title} – {table.Subtitle}";
        var columns = table.Columns.Select((c, i) => c.Kind switch
        {
            ReportValueKind.Money => GridColumn<object?[]>.Money(c.Header, r => r[i] as decimal? ?? 0m),
            ReportValueKind.Date => GridColumn<object?[]>.Date(c.Header, r => r[i] as DateOnly?),
            _ => new GridColumn<object?[]>(c.Header, r => r[i])
        }).ToList();
        Ui.Fill(_grid, table.Rows, columns);
    }

    private void Export(IReportExporter exporter)
    {
        if (_table == null)
        {
            Ui.Warn(FindForm()!, "Please run a report first.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = exporter.FileFilter,
            FileName = $"{_table.Title.Replace(' ', '-').ToLowerInvariant()}-{App.Clock.Today:yyyyMMdd}",
            Title = $"Export to {exporter.FormatName}"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        if (Run(() =>
            {
                using var stream = File.Create(dialog.FileName);
                exporter.Export(_table, stream);
            }))
        {
            SetStatus($"Exported to {dialog.FileName}");
        }
    }
}
