using ELibrary.Business.Dtos;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

internal sealed class CheckoutScreen : ScreenBase
{
    private readonly TextBox _memberCode = Ui.TextBox(150);
    private readonly TextBox _barcode = Ui.TextBox(180);
    private readonly Label _details = Ui.Label(string.Empty, bold: true);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Button _lend;
    private MemberSummaryDto? _member;

    public CheckoutScreen(AppServices app) : base(app, "Check out")
    {
        _lend = Ui.Button("Check out", CheckOut, 115, ButtonVariant.Primary);
        _memberCode.TextChanged += (_, _) =>
        {
            _barcode.Clear();
            ShowMember(null);
        };
        Ui.OnEnter(_memberCode, LoadMember);
        Ui.OnEnter(_barcode, CheckOut);
        var memberRow = Ui.Row(Ui.Label("Member code"), _memberCode, Ui.Button("Find member", LoadMember, 110));
        var copyRow = Ui.Row(Ui.Label("Copy barcode"), _barcode, _lend);
        Compose(_grid, memberRow, copyRow, Ui.Row(_details));
        ShowMember(null);
    }

    public override void RefreshData()
    {
        if (string.IsNullOrWhiteSpace(_memberCode.Text))
        {
            FocusLater(_memberCode);
        }
        else
        {
            LoadMember();
        }
    }

    private void LoadMember()
    {
        ShowMember(null);
        if (Run(() => ShowMember(App.Circulation.GetMemberSummary(_memberCode.Text))))
        {
            SetStatus(_member!.BorrowingBlock ?? "Member found. Enter the copy barcode to check it out.",
                isError: _member.BorrowingBlock != null);
            if (_barcode.Enabled)
            {
                _barcode.Focus();
            }
        }
    }

    private void CheckOut()
    {
        if (_member is not { } member)
        {
            return;
        }

        if (Run(() =>
        {
            var loan = App.Circulation.CheckOut(member.Member.MemberCode, _barcode.Text);
            _barcode.Clear();
            var message = $"Checked out {loan.TitleName} to {loan.MemberCode}. Due {Ui.Date(loan.DueDate)}.";
            Notify(Tone.Success, "Copy checked out", message);
            if (Run(() => ShowMember(App.Circulation.GetMemberSummary(member.Member.MemberCode))))
            {
                SetStatus(message);
            }
        }))
        {
            _barcode.Focus();
        }
    }

    private void ShowMember(MemberSummaryDto? summary)
    {
        _member = summary;
        _details.Text = summary == null ? string.Empty
            : $"{summary.Member.FullName} · {summary.Member.MembershipTypeName} · Card expires {Ui.Date(summary.Member.CardExpiryDate)} · "
              + $"{summary.OpenLoans.Count}/{summary.MaxLoans} on loan · Fines {Ui.Money(summary.OutstandingFines)}";
        _barcode.Enabled = _lend.Enabled = summary != null && summary.BorrowingBlock == null;
        GridStyle.SetEmptyState(_grid, Icons.Book,
            summary == null ? "No member selected" : "No items on loan",
            summary == null
                ? "Enter a member code and press Enter to see the member's loans."
                : $"{summary.Member.MemberCode} has nothing on loan. Scan a copy barcode to check one out.");
        Ui.Fill(_grid, summary?.OpenLoans ?? Array.Empty<LoanDto>(), LoansScreen.MemberLoanColumns);
    }
}
