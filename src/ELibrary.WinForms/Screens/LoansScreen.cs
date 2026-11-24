using ELibrary.Business.Dtos;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

internal class LoansScreen : ScreenBase
{
    internal static readonly GridColumn<LoanDto>[] LoanColumns =
    [
        new("Member", l => $"{l.MemberCode} · {l.MemberName}", 150),
        new("Title", l => l.TitleName, 230),
        new("Barcode", l => l.Barcode, 75),
        new("Loan date", l => l.LoanDate, 75, Ui.DateFormat),
        new("Due date", l => l.DueDate.ToDateTime(TimeOnly.MinValue), 75, Ui.DateFormat),
        new("Renewals", l => l.RenewalCount, 60),
        new("Status", l => l.IsOverdue ? "Overdue" : "On loan", 95),
        new("Fine to date", l => l.FineToDate, 90, Ui.MoneyFormat)
    ];

    /// <summary>The same columns without "Member", for screens that already show one member.</summary>
    internal static readonly GridColumn<LoanDto>[] MemberLoanColumns = LoanColumns.Skip(1).ToArray();

    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _memberCode = Ui.TextBox(150);
    private readonly CheckBox _overdueOnly = new() { Text = "Overdue only", AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
    private readonly bool _memberMode;

    public LoansScreen(AppServices app, bool memberMode = false) : base(app, memberMode ? "My loans" : "Loans & renewals")
    {
        _memberMode = memberMode;
        Ui.OnEnter(_memberCode, RefreshData);
        _overdueOnly.CheckedChanged += (_, _) => RefreshData();
        var actions = Ui.Row(Ui.Button("Refresh", RefreshData, 90),
            Ui.Button("Renew selected…", Renew, 145, ButtonVariant.Primary));
        if (memberMode)
        {
            Compose(_grid, actions);
        }
        else
        {
            Compose(_grid, Ui.Row(Ui.Label("Member code (optional)"), _memberCode, _overdueOnly), actions);
        }
    }

    public override void RefreshData() => Run(() =>
    {
        _grid.Rows.Clear();
        var loans = _memberMode ? App.Circulation.GetMyLoans()
            : App.Circulation.GetOpenLoans(_memberCode.Text, _overdueOnly.Checked);
        Ui.Fill(_grid, loans, LoanColumns);
        SetStatus($"{loans.Count} open loan(s).");
    });

    private void Renew()
    {
        if (Ui.Selected<LoanDto>(_grid) is not { } loan)
        {
            Ui.Warn(FindForm()!, "Select a loan first.");
            return;
        }

        if (Ui.Confirm(FindForm()!, $"Renew {loan.TitleName} ({loan.Barcode})?")
            && Run(() =>
            {
                var renewed = App.Circulation.Renew(loan.Id);
                RefreshData();
                SetStatus($"Renewed {renewed.Barcode}. New due date {Ui.Date(renewed.DueDate)}.");
            }))
        {
            _grid.Focus();
        }
    }
}

internal sealed class MyLoansScreen(AppServices app) : LoansScreen(app, memberMode: true);
