using ELibrary.Business.Dtos;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Dialogs;

/// <summary>UC03: create or edit a membership type (loan limit, loan period and renewals, BR1/BR7).</summary>
internal sealed class MembershipTypeDialog : DialogBase
{
    private readonly AppServices _app;
    private readonly MembershipTypeDto? _type;
    private readonly TextBox _name = Ui.TextBox(260);
    private readonly NumericUpDown _maxLoans;
    private readonly NumericUpDown _period;
    private readonly NumericUpDown _renewals;
    private readonly CheckBox _active = new() { Text = "Members can be registered with this type", AutoSize = true };

    public MembershipTypeDialog(AppServices app, MembershipTypeDto? type)
        : base(type == null ? "New membership type" : $"Edit {type.Name}", type == null ? "Create" : "Save")
    {
        _app = app;
        _type = type;
        _name.Text = type?.Name ?? string.Empty;
        _maxLoans = Ui.Number(1, 50, type?.MaxLoans ?? 3);
        _period = Ui.Number(1, 365, type?.LoanPeriodDays ?? 14);
        _renewals = Ui.Number(0, 10, type?.MaxRenewals ?? 1);
        _active.Checked = type?.IsActive ?? true;
        SetContent(Ui.FieldTable(
            ("Name", _name),
            ("Loan limit (books)", _maxLoans),
            ("Loan period (days)", Ui.Column(_period, Ui.Label("Moved to the next opening day if it falls on a closed day."))),
            ("Renewals allowed", _renewals),
            ("Active", _active)));
    }

    public int SavedId { get; private set; }

    protected override bool TrySave() => Run(() =>
        SavedId = _app.Settings.SaveMembershipType(new MembershipTypeDto(_type?.Id ?? 0, _name.Text,
            (int)_maxLoans.Value, (int)_period.Value, (int)_renewals.Value, _active.Checked)));
}
