using System.Diagnostics;
using ELibrary.Business.Dtos;
using ELibrary.Domain.Enums;
using ELibrary.WinForms.Dialogs;
using ELibrary.WinForms.Infrastructure;

namespace ELibrary.WinForms.Screens;

/// <summary>
/// UC05 Search catalogue / open e-resource link (all roles), UC10 Reserve (members),
/// UC04 Manage titles, copies and links (librarians).
/// </summary>
internal sealed class CatalogueScreen : ScreenBase
{
    private static readonly GridColumn<TitleSummaryDto>[] TitleColumns =
    {
        new("Title", t => t.Name, 260),
        new("Authors", t => t.Authors, 160),
        new("Category", t => t.Category, 100),
        new("Year", t => t.PublicationYear, 45),
        new("ISBN", t => t.Isbn, 95),
        new("Available", t => $"{t.AvailableCopies} / {t.TotalCopies}", 60),
        new("Waiting", t => t.WaitingReservations, 50),
        new("E-link", t => t.HasEResources ? "Yes" : string.Empty, 45),
        new("Status", t => t.IsActive ? string.Empty : "Inactive", 55)
    };

    private readonly GridColumn<CopyDto>[] _copyColumns;

    private static readonly GridColumn<EResourceDto>[] LinkColumns =
    {
        new("Link", e => e.Label, 200),
        new("Address", e => e.Url, 200)
    };

    private readonly bool _isLibrarian;
    private readonly bool _isMember;
    private readonly TextBox _search = Ui.TextBox(280);
    private readonly ComboBox _field = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, DisplayMember = nameof(LookupDto.Name) };
    private readonly CheckBox _onlyAvailable = new() { Text = "Only available", AutoSize = true, Margin = new Padding(8, 8, 3, 3) };
    private readonly CheckBox _includeInactive = new() { Text = "Include inactive", AutoSize = true, Margin = new Padding(8, 8, 3, 3) };
    private readonly DataGridView _titles = Ui.Grid();
    private readonly DataGridView _copies = Ui.Grid();
    private readonly DataGridView _links = Ui.Grid();
    private readonly Label _details = new() { AutoSize = true, MaximumSize = new Size(900, 0), Padding = new Padding(0, 4, 0, 4) };
    private TitleDetailsDto? _current;

    public CatalogueScreen(AppServices app) : base(app, "Catalogue")
    {
        _isLibrarian = app.User.Role == RoleCode.Librarian;
        _isMember = app.User.Role == RoleCode.Member;
        _copyColumns = new[]
        {
            new GridColumn<CopyDto>("Barcode", c => c.Barcode, 80),
            new GridColumn<CopyDto>("Location", c => c.Location, 80),
            new GridColumn<CopyDto>("Status", c => c.Status.ToString(), 70),
            GridColumn<CopyDto>.Date("Due back", c => c.DueDate),
            new GridColumn<CopyDto>("On hold for", c => _isMember ? (c.HoldExpiresOn.HasValue ? "(reserved)" : null) : c.HeldForMemberCode, 70),
            GridColumn<CopyDto>.Date("Hold until", c => c.HoldExpiresOn)
        };

        foreach (var field in Enum.GetValues<CatalogueSearchField>())
        {
            _field.Items.Add(field);
        }

        _field.SelectedItem = CatalogueSearchField.All;
        Ui.OnEnter(_search, () => _ = Search());
        _titles.SelectionChanged += (_, _) => LoadDetails();
        _links.CellDoubleClick += (_, _) => OpenLink();

        var searchRow = Ui.Row(Ui.Label("Search"), _search, Ui.Label("in"), _field, Ui.Label("Category"), _category, _onlyAvailable);
        if (!_isMember)
        {
            searchRow.Controls.Add(_includeInactive);
        }

        searchRow.Controls.Add(Ui.Button("Search", () => _ = Search(), 100));

        var actions = Ui.Row();
        if (_isMember)
        {
            actions.Controls.Add(Ui.Button("Reserve title", Reserve, 130));
            actions.Controls.Add(Ui.Button("Open link", OpenLink, 110));
        }
        else if (_isLibrarian)
        {
            actions.Controls.Add(Ui.Button("New title…", NewTitle));
            actions.Controls.Add(Ui.Button("Edit title…", EditTitle));
            actions.Controls.Add(Ui.Button("Activate / deactivate", ToggleActive, 160));
            actions.Controls.Add(Ui.Button("Add copy…", AddCopy));
            actions.Controls.Add(Ui.Button("Copy status…", ChangeCopyStatus));
            actions.Controls.Add(Ui.Button("Copy location…", ChangeCopyLocation, 130));
            actions.Controls.Add(Ui.Button("Add link…", AddLink));
            actions.Controls.Add(Ui.Button("Remove link", RemoveLink));
            actions.Controls.Add(Ui.Button("Open link", OpenLink, 110));
        }
        else
        {
            actions.Controls.Add(Ui.Button("Open link", OpenLink, 110));
        }

        var lower = Ui.Split(Orientation.Vertical, 0.6);
        lower.Panel1.Controls.Add(WithCaption("Copies", _copies));
        lower.Panel2.Controls.Add(WithCaption("E-resource links (double-click to open)", _links));

        var detailPanel = new Panel();
        Ui.Stack(detailPanel, lower, null, _details, actions);

        var split = Ui.Split(Orientation.Horizontal, 0.45);
        split.Panel1.Controls.Add(_titles);
        split.Panel2.Controls.Add(detailPanel);

        Compose(split, searchRow);
        LoadCategories();
    }

    public override void RefreshData()
    {
        if (_titles.Rows.Count == 0)
        {
            _ = Search();
        }
    }

    private static Control WithCaption(string caption, Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        Ui.Stack(panel, content, null, Ui.Label(caption, bold: true));
        return panel;
    }

    private void LoadCategories()
    {
        _ = RunAsync("categories", () => App.Catalogue.GetCategories().ToList(), categories =>
        {
            _category.Items.Clear();
            _category.Items.Add(new LookupDto(0, "(all categories)"));
            foreach (var category in categories)
            {
                _category.Items.Add(category);
            }

            _category.SelectedIndex = 0;
        });
    }

    /// <summary>Runs the search in the background; the grid shows a skeleton until the results are in.</summary>
    private Task<bool> Search()
    {
        var categoryId = (_category.SelectedItem as LookupDto)?.Id;
        var query = new CatalogueQuery(
            _search.Text,
            (CatalogueSearchField)(_field.SelectedItem ?? CatalogueSearchField.All),
            categoryId is > 0 ? categoryId : null,
            _onlyAvailable.Checked,
            _includeInactive.Checked);

        return RunAsync("search",
            () =>
            {
                var timer = Stopwatch.StartNew();
                var results = App.Catalogue.Search(query);
                return (Results: results, Ms: timer.ElapsedMilliseconds);
            },
            found =>
            {
                Ui.Fill(_titles, found.Results, TitleColumns, t => t.IsActive ? null : Ui.InactiveRowColor);
                SetStatus($"{found.Results.Count} title(s) found in {found.Ms:0} ms.");
            },
            placeholder: _titles);
    }

    private void LoadDetails(int? selectTitleId = null)
    {
        var selected = Ui.Selected<TitleSummaryDto>(_titles);
        var id = selectTitleId ?? selected?.Id;
        if (id == null)
        {
            CancelLoad("details");
            _current = null;
            _details.Text = string.Empty;
            Ui.Fill(_copies, Array.Empty<CopyDto>(), _copyColumns);
            Ui.Fill(_links, Array.Empty<EResourceDto>(), LinkColumns);
            return;
        }

        // Selecting rows quickly starts several loads; only the last one is shown.
        _ = RunAsync("details", () => App.Catalogue.GetTitleDetails(id.Value), details =>
        {
            _current = details;
            var d = details;
            var lines = new List<string>
            {
                $"{d.Name}  —  {string.Join(", ", d.Authors)}",
                $"Publisher: {d.Publisher ?? "–"}   Category: {d.Category ?? "–"}   Year: {d.PublicationYear?.ToString() ?? "–"}   ISBN: {d.Isbn ?? "–"}",
                $"Members waiting: {d.WaitingReservations}" + (d.MyReservationStatus.HasValue ? $"   Your reservation: {d.MyReservationStatus}" : string.Empty)
            };
            if (!d.IsActive)
            {
                lines.Add("This title is inactive (hidden from members).");
            }

            _details.Text = string.Join(Environment.NewLine, lines);
            Ui.Fill(_copies, d.Copies, _copyColumns, c => c.Status == CopyStatus.Available ? null : Ui.UnavailableRowColor);
            Ui.Fill(_links, d.EResources, LinkColumns);
        });
    }

    private bool RequireTitle()
    {
        if (_current != null)
        {
            return true;
        }

        Ui.Warn(FindForm()!, "Please select a title first.");
        return false;
    }

    private void Reserve()
    {
        if (!RequireTitle())
        {
            return;
        }

        if (Run(() =>
            {
                var reservation = App.Reservations.Place(_current!.Id);
                SetStatus($"Reserved '{reservation.TitleName}'. You are number {reservation.QueuePosition} in the queue.");
            }))
        {
            LoadDetails(_current!.Id);
        }
    }

    private void OpenLink()
    {
        var link = Ui.Selected<EResourceDto>(_links);
        if (link == null)
        {
            Ui.Warn(FindForm()!, "Please select an e-resource link first.");
            return;
        }

        Run(() =>
        {
            var url = App.Catalogue.GetEResourceUrl(link.Id);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            SetStatus($"Opened {url}");
        });
    }

    private async void NewTitle()
    {
        using var dialog = new TitleEditorDialog(App, null);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            SetStatus("The title was created. Add its copies with \"Add copy…\".");
            _search.Text = string.Empty;
            var savedId = dialog.SavedTitleId;
            await Search();
            SelectTitle(savedId);
        }
    }

    private async void EditTitle()
    {
        if (!RequireTitle())
        {
            return;
        }

        using var dialog = new TitleEditorDialog(App, _current);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            SetStatus("The title was saved.");
            var savedId = dialog.SavedTitleId;
            await Search();
            SelectTitle(savedId);
        }
    }

    private void SelectTitle(int titleId)
    {
        foreach (DataGridViewRow row in _titles.Rows)
        {
            if (row.Tag is TitleSummaryDto t && t.Id == titleId)
            {
                _titles.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private async void ToggleActive()
    {
        if (!RequireTitle())
        {
            return;
        }

        var activate = !_current!.IsActive;
        var question = activate
            ? $"Make '{_current.Name}' visible to members again?"
            : $"Deactivate '{_current.Name}'? It will be hidden from members.";
        if (Ui.Confirm(FindForm()!, question) && Run(() => App.Catalogue.SetTitleActive(_current.Id, activate)))
        {
            SetStatus(activate ? "The title is active." : "The title was deactivated.");
            _includeInactive.Checked = true;
            var titleId = _current.Id;
            await Search();
            SelectTitle(titleId);
        }
    }

    private void AddCopy()
    {
        if (!RequireTitle())
        {
            return;
        }

        var values = InputDialog.Ask(FindForm()!, $"Add copy – {_current!.Name}", "Add", ("Barcode", string.Empty), ("Location (shelf)", string.Empty));
        if (values != null && Run(() => App.Catalogue.AddCopy(_current.Id, values[0], values[1])))
        {
            SetStatus($"Copy {values[0].Trim()} was added.");
            LoadDetails(_current.Id);
        }
    }

    private void ChangeCopyStatus()
    {
        var copy = Ui.Selected<CopyDto>(_copies);
        if (copy == null)
        {
            Ui.Warn(FindForm()!, "Please select a copy first.");
            return;
        }

        var choices = new[] { CopyStatus.Available, CopyStatus.UnderRepair, CopyStatus.Lost, CopyStatus.Withdrawn };
        var status = ChoiceDialog<CopyStatus>.Ask(FindForm()!, $"Copy {copy.Barcode}", "New status", choices, copy.Status);
        if (status != default && status != copy.Status && Run(() => App.Catalogue.ChangeCopyStatus(copy.Id, status)))
        {
            SetStatus($"Copy {copy.Barcode} is now {status}.");
            LoadDetails(_current!.Id);
        }
    }

    private void ChangeCopyLocation()
    {
        var copy = Ui.Selected<CopyDto>(_copies);
        if (copy == null)
        {
            Ui.Warn(FindForm()!, "Please select a copy first.");
            return;
        }

        var values = InputDialog.Ask(FindForm()!, $"Copy {copy.Barcode}", "Save", ("Location (shelf)", copy.Location));
        if (values != null && Run(() => App.Catalogue.ChangeCopyLocation(copy.Id, values[0])))
        {
            LoadDetails(_current!.Id);
        }
    }

    private void AddLink()
    {
        if (!RequireTitle())
        {
            return;
        }

        var values = InputDialog.Ask(FindForm()!, $"Add e-resource link – {_current!.Name}", "Add", ("Label", string.Empty), ("URL (https://…)", "https://"));
        if (values != null && Run(() => App.Catalogue.AddEResource(_current.Id, values[0], values[1])))
        {
            LoadDetails(_current.Id);
        }
    }

    private void RemoveLink()
    {
        var link = Ui.Selected<EResourceDto>(_links);
        if (link == null)
        {
            Ui.Warn(FindForm()!, "Please select a link first.");
            return;
        }

        if (Ui.Confirm(FindForm()!, $"Remove the link '{link.Label}'?") && Run(() => App.Catalogue.RemoveEResource(link.Id)))
        {
            LoadDetails(_current!.Id);
        }
    }
}
