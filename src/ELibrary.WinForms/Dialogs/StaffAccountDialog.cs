using ELibrary.Business.Dtos;
using ELibrary.Business.Security;
using ELibrary.Domain.Enums;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Dialogs;

/// <summary>UC02: create a staff account (username, name, role, first password) or edit an existing one.</summary>
internal sealed class StaffAccountDialog : DialogBase
{
    private static readonly RoleCode[] StaffRoles = { RoleCode.Librarian, RoleCode.Administrator };

    private readonly AppServices _app;
    private readonly AccountDto? _account;
    private readonly TextBox _username = Ui.TextBox(220);
    private readonly TextBox _fullName = Ui.TextBox(300);
    private readonly TextBox _password = Ui.TextBox(220, password: true);
    private readonly ComboBox _role = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };

    public StaffAccountDialog(AppServices app, AccountDto? account)
        : base(account == null ? "New staff account" : $"Edit {account.Username}", account == null ? "Create" : "Save")
    {
        _app = app;
        _account = account;
        foreach (var role in StaffRoles)
        {
            _role.Items.Add(role);
        }

        _role.SelectedItem = account?.Role ?? RoleCode.Librarian;
        _fullName.Text = account?.FullName ?? string.Empty;

        var fields = new List<(string Label, Control Field)>();
        if (account == null)
        {
            fields.Add(("Username", Ui.Column(_username, Ui.Label("Letters, digits, '.', '-' and '_'."))));
        }

        fields.Add(("Full name", _fullName));
        fields.Add(("Role", _role));
        if (account == null)
        {
            fields.Add(("First password", Ui.Column(_password, Ui.Label(PasswordPolicy.Description))));
        }

        SetContent(Ui.FieldTable(fields.ToArray()));
    }

    /// <summary>The id of the created or edited account.</summary>
    public int SavedId { get; private set; }

    protected override bool TrySave() => Run(() =>
    {
        var role = (RoleCode)_role.SelectedItem!;
        SavedId = _account == null
            ? _app.Accounts.CreateStaffAccount(_username.Text, _fullName.Text, role, _password.Text)
            : _app.Accounts.UpdateStaffAccount(_account.Id, _fullName.Text, role).Id;
    });
}
