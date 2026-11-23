using System.Globalization;
using ELibrary.Business.Dtos;
using ELibrary.Domain.Exceptions;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

internal sealed class FinesScreen : ScreenBase
{
    private static readonly GridColumn<FineLineDto>[] Columns =
    [
        new("Title", f => f.TitleName, 160),
        new("Barcode", f => f.Barcode, 70),
        new("Due date", f => f.DueDate.ToDateTime(TimeOnly.MinValue), 80, Ui.DateFormat),
        new("Days", f => f.OverdueDays, 50),
        new("Fine", f => f.Amount, 80, Ui.MoneyFormat),
        new("Paid", f => f.AmountPaid, 70, Ui.MoneyFormat),
        new("Balance", f => f.Balance, 80, Ui.MoneyFormat),
        new("Status", f => f.IsAccruing ? "Accruing" : f.Balance == 0 ? "Paid" : "Recorded", 75)
    ];

    private readonly TextBox _memberCode = Ui.TextBox(150);
    private readonly TextBox _amount = Ui.TextBox(150);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _totals = Ui.Label(string.Empty, bold: true);
    private readonly Label _eligibility = Ui.Label(string.Empty);
    private readonly bool _memberMode;
    private readonly Button _pay;
    private MemberFinesDto? _statement;

    public FinesScreen(AppServices app, bool memberMode) : base(app, memberMode ? "My fines" : "Fines & payments")
    {
        _memberMode = memberMode;
        _pay = Ui.Button("Record payment", RecordPayment, 130, ButtonVariant.Primary);
        _memberCode.TextChanged += (_, _) =>
        {
            _amount.Clear();
            ShowStatement(null);
        };
        Ui.OnEnter(_memberCode, RefreshData);
        Ui.OnEnter(_amount, RecordPayment);

        if (memberMode)
        {
            Compose(_grid, Ui.Row(Ui.Button("Refresh", RefreshData, 90)), Ui.Row(_totals), Ui.Row(_eligibility));
        }
        else
        {
            Compose(_grid, Ui.Row(Ui.Label("Member code"), _memberCode, Ui.Button("Find fines", RefreshData, 100)),
                Ui.Row(_totals), Ui.Row(_eligibility), Ui.Row(Ui.Label("Amount (VND)"), _amount, _pay));
        }

        ShowStatement(null);
    }

    public override void RefreshData()
    {
        ShowStatement(null);
        if (_memberMode || !string.IsNullOrWhiteSpace(_memberCode.Text))
        {
            Run(() => ShowStatement(_memberMode ? App.Fines.GetMyFines() : App.Fines.GetMemberFines(_memberCode.Text)));
        }
    }

    private void RecordPayment()
    {
        if (_statement is not { } statement || _memberMode)
        {
            return;
        }

        Run(() =>
        {
            if (!decimal.TryParse(_amount.Text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
                || amount <= 0)
            {
                throw new ValidationException("Amount", "Enter a positive amount without grouping separators, for example 25000.");
            }

            if (!Ui.Confirm(FindForm()!, $"Record {Ui.Money(amount)} from {statement.Member.MemberCode}?"))
            {
                return;
            }

            var receipt = App.Fines.RecordPayment(statement.Member.MemberCode, amount);
            _amount.Clear();
            RefreshData();
            SetStatus($"Recorded {Ui.Money(receipt.Amount)} for {receipt.MemberCode}.");
            MessageDialog.Inform(FindForm(), "Payment receipt",
                $"{receipt.MemberName} ({receipt.MemberCode})\n{Ui.DateTime(receipt.PaidAt)} · Received by {receipt.ReceivedBy}\n"
                + $"Paid {Ui.Money(receipt.Amount)} · Payable balance {Ui.Money(receipt.RecordedBalance)}\n"
                + $"Total outstanding {Ui.Money(receipt.TotalBalance)}",
                Tone.Success);
        });
    }

    private void ShowStatement(MemberFinesDto? statement)
    {
        _statement = statement;
        _totals.Text = statement == null ? string.Empty
            : $"{statement.Member.FullName} · Payable {Ui.Money(statement.RecordedBalance)} · "
              + $"Accruing {Ui.Money(statement.AccruingBalance)} · Total {Ui.Money(statement.TotalBalance)}";
        _eligibility.Text = statement == null ? string.Empty
            : statement.BorrowingBlock ?? "Card and fine balance meet the borrowing requirements.";
        _pay.Enabled = _amount.Enabled = !_memberMode && statement is { RecordedBalance: > 0 };
        Ui.Fill(_grid, statement?.Fines ?? Array.Empty<FineLineDto>(), Columns);
    }
}
