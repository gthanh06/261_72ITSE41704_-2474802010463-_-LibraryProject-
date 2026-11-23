using System.Globalization;
using ELibrary.Business.Dtos;
using ELibrary.WinForms.Dialogs;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

/// <summary>
/// UC03 and NFR-08 (Administrator): membership types, the fine policy, holidays and database backup.
/// The four parts are pages behind a tab row; each page loads its data when it is shown.
/// </summary>
internal sealed class SettingsScreen : ScreenBase
{
    private readonly Panel _pages = new() { Dock = DockStyle.Fill };
    private readonly List<(ThemedButton Tab, Control Page, Action Load)> _tabs = new();
    private int _current = -1;

    // Membership types
    private readonly DataGridView _types = Ui.Grid();

    // Fine policy
    private readonly NumericUpDown _dailyRate = Ui.Number(1_000, 1_000_000, 5_000, 1_000, 130);
    private readonly NumericUpDown _maxFine = Ui.Number(1_000, 10_000_000, 100_000, 5_000, 130);
    private readonly NumericUpDown _threshold = Ui.Number(0, 10_000_000, 50_000, 5_000, 130);
    private readonly NumericUpDown _holdDays = Ui.Number(1, 30, 3);
    private readonly CheckBox _closedOnSundays = new() { Text = "The library is closed on Sundays", AutoSize = true };
    private readonly Label _example = Ui.Label(string.Empty);

    // Holidays
    private readonly DataGridView _holidays = Ui.Grid();
    private readonly NumericUpDown _year;
    private readonly DateTimePicker _holidayDate;
    private readonly TextBox _holidayName = Ui.TextBox(240);

    public SettingsScreen(AppServices app) : base(app, "Settings")
    {
        _year = Ui.Number(2000, 2100, app.Clock.Today.Year);
        _holidayDate = Ui.DatePicker(app.Clock.Today);
        foreach (var box in new[] { _dailyRate, _maxFine, _threshold })
        {
            box.ThousandsSeparator = true;
            box.ValueChanged += (_, _) => ShowExample();
        }

        var tabRow = Ui.Row(
            AddTab("Membership types", TypesPage(), LoadTypes),
            AddTab("Fines & holds", PolicyPage(), LoadPolicy),
            AddTab("Holidays", HolidaysPage(), LoadHolidays),
            AddTab("Backup & restore", BackupPage(), () => SetStatus("Backups include every table: catalogue, members, loans, fines and accounts.")));
        Compose(_pages, tabRow);
        ShowTab(0);
    }

    public override void RefreshData()
    {
        if (_current >= 0)
        {
            _tabs[_current].Load();
        }
    }

    // ---- Tabs ----------------------------------------------------------------------------------

    private ThemedButton AddTab(string text, Control page, Action load)
    {
        var index = _tabs.Count;
        var tab = new ThemedButton(text) { Compact = true };
        tab.Click += (_, _) => ShowTab(index);
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pages.Controls.Add(page);
        _tabs.Add((tab, page, load));
        return tab;
    }

    private void ShowTab(int index)
    {
        if (index == _current)
        {
            return;
        }

        var transition = ScreenTransition.Capture(_pages, _current >= 0 ? _tabs[_current].Page : null);
        using (AntiFlicker.SuspendDrawing(_pages))
        {
            for (var i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Tab.Variant = i == index ? ButtonVariant.Primary : ButtonVariant.Secondary;
                _tabs[i].Page.Visible = i == index;
            }

            transition?.BringToFront();
        }

        _current = index;
        _tabs[index].Load();
        transition?.Play(_tabs[index].Page);
    }

    // ---- Membership types ----------------------------------------------------------------------

    private Control TypesPage()
    {
        var page = new Panel();
        _types.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditType(Ui.Selected<MembershipTypeDto>(_types));
            }
        };
        var actions = Ui.Row(
            Ui.Button("New type…", () => EditType(null), 110, ButtonVariant.Primary),
            Ui.Button("Edit…", () => EditType(Ui.Selected<MembershipTypeDto>(_types) ?? Missing<MembershipTypeDto>("a membership type")), 80),
            Ui.Label("BR1: loan limit and loan period · BR7: renewals"));
        Ui.Stack(page, _types, null, actions);
        return page;
    }

    private void LoadTypes(int? selectId = null) => _ = RunAsync("types", App.Settings.GetMembershipTypes, types =>
    {
        Ui.Fill(_types, types, new GridColumn<MembershipTypeDto>[]
        {
            new("Name", t => t.Name, 170),
            new("Loan limit", t => t.MaxLoans, 80),
            new("Loan period", t => $"{t.LoanPeriodDays} days", 90),
            new("Renewals", t => t.MaxRenewals, 70),
            new("Status", t => t.IsActive ? "Active" : "Inactive", 80)
        }, t => t.IsActive ? null : Theme.Colors.SurfaceSunken);
        var row = _types.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (r.Tag as MembershipTypeDto)?.Id == selectId);
        if (row != null)
        {
            _types.CurrentCell = row.Cells[0];
        }

        SetStatus($"{types.Count} membership type(s).");
    }, _types);

    private void LoadTypes() => LoadTypes(null);

    private void EditType(MembershipTypeDto? type)
    {
        using var dialog = new MembershipTypeDialog(App, type);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Notify(Tone.Success, type == null ? "Membership type created" : "Membership type saved");
            LoadTypes(dialog.SavedId);
        }
    }

    // ---- Fine policy ---------------------------------------------------------------------------

    private Control PolicyPage()
    {
        var fields = Ui.FieldTable(
            ("Fine per overdue day (VND)", Ui.Column(_dailyRate, Ui.Label("Sundays and holidays are not counted (BR3)."))),
            ("Maximum fine per loan (VND)", _maxFine),
            ("Borrowing blocked above (VND)", Ui.Column(_threshold, Ui.Label("Unpaid fines above this stop loans and reservations (BR2)."))),
            ("Hold shelf time (opening days)", Ui.Column(_holdDays, Ui.Label("How long a returned copy waits for the member who reserved it (BR5)."))),
            ("Opening days", _closedOnSundays));
        var save = Ui.Button("Save policy", SavePolicy, 130, ButtonVariant.Primary);
        _example.Margin = _example.Px(Theme.Space.Xxs, Theme.Space.S, 0, Theme.Space.S);
        var card = new Card("Fines and holds", "Changes apply to fines recorded from now on; recorded fines keep their amount.")
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        card.Controls.Add(Ui.Column(fields, _example, save));
        var page = new Panel { AutoScroll = true };
        card.Location = new Point(0, page.Px(Theme.Space.Xs));
        page.Controls.Add(card);
        return page;
    }

    private void LoadPolicy() => _ = RunAsync("policy", App.Settings.GetFinePolicy, policy =>
    {
        _dailyRate.Value = Clamp(_dailyRate, policy.DailyRate);
        _maxFine.Value = Clamp(_maxFine, policy.MaxFinePerLoan);
        _threshold.Value = Clamp(_threshold, policy.DebtThreshold);
        _holdDays.Value = Clamp(_holdDays, policy.HoldPeriodDays);
        _closedOnSundays.Checked = policy.ClosedOnSundays;
        ShowExample();
        SetStatus("Current fine policy loaded.");
    });

    private void SavePolicy()
    {
        var policy = new FinePolicyDto(_dailyRate.Value, _maxFine.Value, _threshold.Value, (int)_holdDays.Value, _closedOnSundays.Checked);
        if (Run(() => App.Settings.UpdateFinePolicy(policy)))
        {
            Notify(Tone.Success, "Fine policy saved");
            SetStatus("Fine policy saved. It applies to fines recorded from now on.");
        }
    }

    private void ShowExample()
    {
        var tenDays = Math.Min(_dailyRate.Value * 10, _maxFine.Value);
        var daysToCap = _dailyRate.Value == 0 ? 0 : (int)Math.Ceiling(_maxFine.Value / _dailyRate.Value);
        _example.Text = $"Example: a book returned 10 opening days late costs {Ui.Money(tenDays)}. " +
                        $"The cap is reached after {daysToCap} day(s).";
    }

    private static decimal Clamp(NumericUpDown box, decimal value) => Math.Min(box.Maximum, Math.Max(box.Minimum, value));

    // ---- Holidays ------------------------------------------------------------------------------

    private Control HolidaysPage()
    {
        var page = new Panel();
        _year.ValueChanged += (_, _) => LoadHolidays();
        Ui.OnEnter(_holidayName, AddHoliday);
        var filter = Ui.Row(Ui.Label("Year"), _year,
            Ui.Button("Remove selected…", RemoveHoliday, 150));
        var add = Ui.Row(Ui.Label("New holiday"), _holidayDate, _holidayName,
            Ui.Button("Add holiday", AddHoliday, 120, ButtonVariant.Primary));
        Ui.Stack(page, _holidays, null, filter, add);
        return page;
    }

    private void LoadHolidays() => _ = RunAsync("holidays", () => App.Settings.GetHolidays((int)_year.Value), holidays =>
    {
        Ui.Fill(_holidays, holidays, new GridColumn<HolidayDto>[]
        {
            GridColumn<HolidayDto>.Date("Date", h => h.Date, 90),
            new("Day", h => h.Date.ToString("dddd", CultureInfo.InvariantCulture), 90),
            new("Description", h => h.Description, 260)
        });
        SetStatus($"{holidays.Count} holiday(s) in {(int)_year.Value}. The library is closed on these days; they are not counted in loan periods or fines.");
    }, _holidays);

    private void AddHoliday()
    {
        var date = Ui.ToDateOnly(_holidayDate);
        if (Run(() => App.Settings.AddHoliday(date, _holidayName.Text)))
        {
            Notify(Tone.Success, "Holiday added", $"{Ui.Date(date)} – {_holidayName.Text.Trim()}");
            _holidayName.Clear();
            if (_year.Value != date.Year)
            {
                _year.Value = date.Year; // reloads
            }
            else
            {
                LoadHolidays();
            }
        }
    }

    private void RemoveHoliday()
    {
        if (Ui.Selected<HolidayDto>(_holidays) is not { } holiday)
        {
            Ui.Warn(FindForm()!, "Select a holiday first.");
            return;
        }

        if (MessageDialog.Confirm(FindForm(), $"Remove {Ui.Date(holiday.Date)} ({holiday.Description})?",
                "Due dates that were already calculated do not change.", "Remove", destructive: true)
            && Run(() => App.Settings.RemoveHoliday(holiday.Id)))
        {
            Notify(Tone.Success, "Holiday removed");
            LoadHolidays();
        }
    }

    // ---- Backup and restore (NFR-08) -----------------------------------------------------------

    private Control BackupPage()
    {
        var backup = new Card("Back up the database", "Writes a full SQL Server backup (.bak). Do this regularly and keep a copy on another drive.")
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        backup.Controls.Add(Ui.Row(Ui.Button("Back up now…", Backup, 140, ButtonVariant.Primary)));

        var restore = new Card("Restore from a backup", "Replaces ALL current data with the backup. Everyone is signed out afterwards.")
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        restore.Controls.Add(Ui.Row(Ui.Button("Restore…", Restore, 120)));

        var note = Ui.Label("The file path is opened by SQL Server, so the SQL Server service must be allowed to read and write that folder.");
        var page = new Panel { AutoScroll = true };
        var column = Ui.Column(backup, restore, note);
        column.Location = new Point(0, page.Px(Theme.Space.Xs));
        page.Controls.Add(column);
        return page;
    }

    private void Backup()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Back up the E-Library database",
            Filter = "SQL Server backup (*.bak)|*.bak",
            FileName = $"ELibrary_{App.Clock.Now:yyyyMMdd_HHmm}.bak"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        var path = dialog.FileName;
        _ = RunAsync("backup", () => { App.Settings.Backup(path); return path; }, file =>
        {
            Notify(Tone.Success, "Backup complete", Path.GetFileName(file));
            SetStatus($"Backed up to {file}.");
        });
    }

    private void Restore()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Restore the E-Library database",
            Filter = "SQL Server backup (*.bak)|*.bak"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        var path = dialog.FileName;
        if (!MessageDialog.Confirm(FindForm(), "Replace all data with this backup?",
                $"{Path.GetFileName(path)}\n\nEverything entered since the backup was made will be lost. This cannot be undone.",
                "Restore", destructive: true))
        {
            return;
        }

        _ = RunAsync("restore", () => { App.Settings.Restore(path); return path; }, _ =>
        {
            MessageDialog.Inform(FindForm(), "Restore complete", "The database was restored. E-Library will restart; please sign in again.", Tone.Success);
            App.Auth.Logout();
            Application.Restart();
        });
    }

    private T? Missing<T>(string what) where T : class
    {
        Ui.Warn(FindForm()!, $"Select {what} first.");
        return null;
    }
}
