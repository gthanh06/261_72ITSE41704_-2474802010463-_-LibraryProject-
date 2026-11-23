using ELibrary.Business.Dtos;
using ELibrary.Domain.Enums;
using ELibrary.WinForms.Dialogs;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

/// <summary>UC02 (Administrator): staff accounts — create, edit, lock or unlock, reset the password.</summary>
internal sealed class AccountsScreen : ScreenBase
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly CheckBox _showMembers = new() { Text = "Show member logins", AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
    private readonly ThemedButton _lock = new("Lock");

    public AccountsScreen(AppServices app) : base(app, "User accounts")
    {
        Header.Description = "Staff who can sign in to E-Library. Member logins are created on the Members screen.";
        _showMembers.CheckedChanged += (_, _) => RefreshData();
        _lock.Click += (_, _) => WithSelected(ToggleLock);
        _grid.SelectionChanged += (_, _) => UpdateLockButton();
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                WithSelected(Edit);
            }
        };

        var actions = Ui.Row(
            Ui.Button("New staff account…", () => Edit(null), 160, ButtonVariant.Primary),
            Ui.Button("Edit…", () => WithSelected(Edit), 80),
            _lock,
            Ui.Button("Reset password…", () => WithSelected(ResetPassword), 140),
            _showMembers);
        Compose(_grid, actions);
    }

    public override void RefreshData() => _ = LoadAccounts(null);

    /// <summary>Reloads the list and selects <paramref name="selectId"/> (or keeps the current selection).</summary>
    private Task<bool> LoadAccounts(int? selectId) => RunAsync("accounts",
        () => App.Accounts.GetAccounts(_showMembers.Checked),
        accounts =>
        {
            var selected = selectId ?? Ui.Selected<AccountDto>(_grid)?.Id;
            Ui.Fill(_grid, accounts, new GridColumn<AccountDto>[]
            {
                new("Username", a => a.Username, 100),
                new("Full name", a => a.FullName, 170),
                new("Role", a => a.Role.ToString(), 90),
                new("Status", Status, 120),
                new("Last sign-in", a => a.LastLoginAt.HasValue ? Ui.DateTime(a.LastLoginAt) : "Never", 120)
            }, a => a.IsLocked ? Theme.Colors.SurfaceSunken : null);
            Reselect(selected);
            UpdateLockButton();
            var locked = accounts.Count(a => a.IsLocked || a.IsTemporarilyLockedOut);
            SetStatus(locked == 0 ? $"{accounts.Count} account(s)." : $"{accounts.Count} account(s), {locked} locked.");
        }, _grid);

    private static string Status(AccountDto account) =>
        account.IsLocked ? "Locked" : account.IsTemporarilyLockedOut ? "Locked for 5 min" : "Active";

    private void Edit(AccountDto? account)
    {
        if (account is { Role: RoleCode.Member })
        {
            Ui.Info(FindForm()!, "Member logins are edited with the member on the Members screen.");
            return;
        }

        using var dialog = new StaffAccountDialog(App, account);
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            Notify(Tone.Success, account == null ? "Account created" : "Account saved");
            _ = LoadAccounts(dialog.SavedId);
        }
    }

    private void ToggleLock(AccountDto account)
    {
        var locking = !(account.IsLocked || account.IsTemporarilyLockedOut);
        if (locking && !MessageDialog.Confirm(FindForm(), $"Lock {account.Username}?",
                $"{account.FullName} will not be able to sign in until the account is unlocked.", "Lock", destructive: true))
        {
            return;
        }

        if (Run(() => App.Accounts.SetLocked(account.Id, locking)))
        {
            Notify(Tone.Success, locking ? $"Locked {account.Username}" : $"Unlocked {account.Username}");
            RefreshData();
        }
    }

    private void ResetPassword(AccountDto account)
    {
        if (account.Role == RoleCode.Member)
        {
            Ui.Info(FindForm()!, "Reset a member's password on the Members screen.");
            return;
        }

        var fields = InputDialog.Ask(FindForm()!, $"Reset password for {account.Username}", "Reset", ("*New password", ""));
        if (fields != null && Run(() => App.Accounts.ResetPassword(account.Id, fields[0])))
        {
            Notify(Tone.Success, "Password reset", $"Give the new password to {account.FullName} in person.");
        }
    }

    private void UpdateLockButton()
    {
        var account = Ui.Selected<AccountDto>(_grid);
        var unlock = account != null && (account.IsLocked || account.IsTemporarilyLockedOut);
        _lock.Text = unlock ? "Unlock" : "Lock";
        _lock.Variant = unlock ? ButtonVariant.Primary : ButtonVariant.Secondary;
    }

    private void Reselect(int? id)
    {
        var row = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (r.Tag as AccountDto)?.Id == id);
        if (row != null)
        {
            _grid.CurrentCell = row.Cells[0];
        }
    }

    private void WithSelected(Action<AccountDto> action)
    {
        if (Ui.Selected<AccountDto>(_grid) is { } account)
        {
            action(account);
        }
        else
        {
            Ui.Warn(FindForm()!, "Select an account first.");
        }
    }
}
