using ELibrary.Business.Dtos;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

internal sealed class ReturnScreen : ScreenBase
{
    private readonly TextBox _barcode = Ui.TextBox(180);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Button _return;
    private LoanDto? _preview;

    public ReturnScreen(AppServices app) : base(app, "Return")
    {
        _return = Ui.Button("Confirm return", ConfirmReturn, 130, ButtonVariant.Primary);
        _barcode.TextChanged += (_, _) => ShowPreview(null);
        Ui.OnEnter(_barcode, FindLoan);
        Compose(_grid, Ui.Row(Ui.Label("Copy barcode"), _barcode, Ui.Button("Find loan", FindLoan, 90), _return));
        ShowPreview(null);
    }

    public override void RefreshData()
    {
        ShowPreview(null);
        FocusLater(_barcode);
    }

    private void FindLoan()
    {
        ShowPreview(null);
        if (Run(() => ShowPreview(App.Circulation.GetReturnPreview(_barcode.Text))))
        {
            SetStatus("Review the loan and confirm the return.");
            _return.Focus();
        }
    }

    private void ConfirmReturn()
    {
        if (_preview is not { } preview)
        {
            return;
        }

        Run(() =>
        {
            var receipt = App.Circulation.Return(preview.Barcode, preview.Id);
            _barcode.Clear();
            var hold = receipt.HeldForMemberCode == null ? "Copy is available."
                : $"Place on the hold shelf for {receipt.HeldForMemberCode} through {Ui.Date(receipt.HoldExpiresOn)}.";
            var loan = receipt.Loan;
            SetStatus($"Returned {loan.Barcode}. Fine {Ui.Money(receipt.Fine)}. {hold}");
            MessageDialog.Inform(FindForm(), "Return receipt",
                $"{loan.TitleName} ({loan.Barcode})\n{loan.MemberName} ({loan.MemberCode})\n"
                + $"Returned {Ui.DateTime(loan.ReturnedAt)} · Fine {Ui.Money(receipt.Fine)} ({receipt.OverdueDays} chargeable day(s))\n\n{hold}",
                Tone.Success);
            _barcode.Focus();
        });
    }

    private void ShowPreview(LoanDto? preview)
    {
        _preview = preview;
        _return.Enabled = preview != null;
        Ui.Fill(_grid, preview == null ? Array.Empty<LoanDto>() : new[] { preview }, LoansScreen.LoanColumns);
    }
}
