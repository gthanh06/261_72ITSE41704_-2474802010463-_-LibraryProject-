using ELibrary.Business.Dtos;
using ELibrary.Business.Reports;
using ELibrary.Domain.Enums;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

/// <summary>
/// The home dashboard: four headline numbers and two charts about the collection. Staff also see current
/// loans and overdue items (from the report services). Read-only; every number comes from an existing
/// service call, loaded in the background.
/// </summary>
internal sealed class OverviewScreen : ScreenBase
{
    private readonly bool _isStaff;
    private readonly StatCard _titles = new("Titles in the catalogue", Icons.Library, Tone.Info);
    private readonly StatCard _available = new("Copies on the shelf", Icons.Book, Tone.Success);
    private readonly StatCard _third;
    private readonly StatCard _fourth;
    private readonly BarChart _byCategory = new("Titles by category", "The largest categories in the collection");
    private readonly BarChart _mostWanted = new("Most reserved", "Titles with the longest waiting lists") { BarColor = Theme.Colors.Info };

    public OverviewScreen(AppServices app) : base(app, "Overview")
    {
        _isStaff = app.User.Role is RoleCode.Administrator or RoleCode.Librarian;
        _third = _isStaff
            ? new StatCard("Current loans", Icons.Loans, Tone.Accent)
            : new StatCard("Copies out or unavailable", Icons.Loans, Tone.Accent);
        _fourth = _isStaff
            ? new StatCard("Overdue items", Icons.Warning, Tone.Danger)
            : new StatCard("Members waiting", Icons.Reservations, Tone.Warning);

        Header.Description = $"{Greeting(app.Clock.Now.Hour)}, {FirstName(app.User.FullName)}. Here is the library today, {Ui.Date(app.Clock.Today)}.";

        var cards = Grid(4, _titles, _available, _third, _fourth);
        var charts = Grid(2, _byCategory, _mostWanted);
        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Colors.Background };
        var column = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, BackColor = Theme.Colors.Background };
        column.Controls.Add(cards);
        column.Controls.Add(charts);
        body.Controls.Add(column);
        Compose(body);
    }

    public override void RefreshData() => _ = RunAsync("overview", LoadSnapshot, Show);

    private Snapshot LoadSnapshot()
    {
        var titles = App.Catalogue.Search(new CatalogueQuery(null));
        if (!_isStaff)
        {
            return new Snapshot(titles, null, null);
        }

        var request = new ReportRequest(App.Clock.Today.AddYears(-1), App.Clock.Today, 10);
        var loans = App.Reports.Run("current-loans", request).Rows.Count;
        var overdue = App.Reports.Run("overdue", request).Rows.Count;
        return new Snapshot(titles, loans, overdue);
    }

    private void Show(Snapshot data)
    {
        var titles = data.Titles;
        var total = titles.Sum(t => t.TotalCopies);
        var available = titles.Sum(t => t.AvailableCopies);
        var categories = titles.Select(t => t.Category).Where(c => c != null).Distinct().Count();

        _titles.SetValue(titles.Count, $"in {categories} categories");
        _available.SetValue(available, $"of {total:N0} copies");
        if (_isStaff)
        {
            _third.SetValue(data.CurrentLoans ?? 0, "borrowed right now");
            _fourth.SetValue(data.Overdue ?? 0, data.Overdue > 0 ? "need a reminder" : "everything is on time");
        }
        else
        {
            _third.SetValue(total - available, "on loan, on hold or in repair");
            _fourth.SetValue(titles.Sum(t => t.WaitingReservations), "reservations in the queues");
        }

        _byCategory.SetItems(titles
            .GroupBy(t => t.Category ?? "Uncategorised")
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2).ThenBy(x => x.Key)
            .Take(8));
        _mostWanted.SetItems(titles
            .Where(t => t.WaitingReservations > 0)
            .OrderByDescending(t => t.WaitingReservations).ThenBy(t => t.Name)
            .Take(8)
            .Select(t => (t.Name, t.WaitingReservations)), "Nobody is waiting for a title right now.");
        SetStatus($"Updated {Ui.DateTime(App.Clock.Now)}.");
    }

    /// <summary>Equal-width columns of cards with a gap between them.</summary>
    private static TableLayoutPanel Grid(int columns, params Control[] cells)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columns,
            RowCount = 1,
            BackColor = Theme.Colors.Background,
            Margin = Padding.Empty
        };
        for (var i = 0; i < columns; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        }

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            cell.Dock = DockStyle.Fill;
            var gap = cell.Px(Theme.Space.M);
            cell.Margin = new Padding(0, 0, i % columns == columns - 1 ? 0 : gap, gap);
            grid.Controls.Add(cell, i % columns, i / columns);
        }

        return grid;
    }

    private static string Greeting(int hour) => hour switch
    {
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening"
    };

    private static string FirstName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? fullName : parts[^1]; // Vietnamese names put the given name last
    }

    private sealed record Snapshot(IReadOnlyList<TitleSummaryDto> Titles, int? CurrentLoans, int? Overdue);
}
