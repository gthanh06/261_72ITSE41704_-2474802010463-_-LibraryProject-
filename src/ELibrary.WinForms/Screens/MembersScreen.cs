using ELibrary.Business.Dtos;
using ELibrary.WinForms.Dialogs;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

internal sealed class MembersScreen : ScreenBase
{
    private readonly TextBox _search = Ui.TextBox(260);
    private readonly DataGridView _grid = Ui.Grid();

    public MembersScreen(AppServices app) : base(app, "Members")
    {
        Ui.OnEnter(_search, RefreshData);
        var searchRow = Ui.Row(Ui.Label("Code, name, email or phone"), _search, Ui.Button("Search", RefreshData, 90));
        var actions = Ui.Row(
            Ui.Button("Register member…", () => Edit(null), 140, ButtonVariant.Primary),
            Ui.Button("Edit…", () => WithSelected(Edit), 80),
            Ui.Button("Extend card…", () => WithSelected(ExtendCard), 110),
            Ui.Button("Change active status…", () => WithSelected(ChangeActive), 160),
            Ui.Button("Reset password…", () => WithSelected(ResetPassword), 140));
        Compose(_grid, searchRow, actions);
    }

    public override void RefreshData() => Run(() =>
    {
        var members = App.Members.Search(_search.Text);
        var today = App.Clock.Today;
        Ui.Fill(_grid, members, new GridColumn<MemberDto>[]
        {
            new("Code", m => m.MemberCode, 70),
            new("Full name", m => m.FullName, 155),
            new("Type", m => m.MembershipTypeName, 110),
            new("Email", m => m.Email, 160),
            new("Phone", m => m.Phone, 90),
            new("Card expiry", m => m.CardExpiryDate.ToDateTime(TimeOnly.MinValue), 90, Ui.DateFormat),
            new("Status", m => !m.IsActive ? "Inactive" : m.CardExpiryDate < today ? "Expired" : "Active", 80)
        });
        SetStatus($"{members.Count} member(s).");
    });

    private void Edit(MemberDto? member)
    {
        Run(() =>
        {
            using var dialog = new MemberEditorDialog(App, member, App.Members.GetMembershipTypes());
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _search.Clear();
                RefreshData();
                var saved = dialog.SavedMember!;
                var row = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (r.Tag as MemberDto)?.Id == saved.Id);
                if (row != null)
                {
                    _grid.CurrentCell = row.Cells[0];
                }

                SetStatus(member == null ? $"Registered {saved.FullName}. Member code and login: {saved.MemberCode}."
                    : $"Saved {saved.MemberCode}.");
            }
        });
    }

    private void ExtendCard(MemberDto member)
    {
        var today = App.Clock.Today;
        var initial = (member.CardExpiryDate > today ? member.CardExpiryDate : today).AddYears(1);
        var expiry = DateDialog.Ask(FindForm()!, $"Extend {member.MemberCode}", "New expiry date", initial);
        if (expiry.HasValue && Run(() => App.Members.ExtendCard(member.Id, expiry.Value)))
        {
            RefreshData();
            SetStatus($"Extended {member.MemberCode} through {Ui.Date(expiry)}.");
        }
    }

    private void ChangeActive(MemberDto member)
    {
        var verb = member.IsActive ? "Deactivate" : "Reactivate";
        if (Ui.Confirm(FindForm()!, $"{verb} {member.FullName} ({member.MemberCode})?")
            && Run(() => App.Members.SetActive(member.Id, !member.IsActive)))
        {
            RefreshData();
            SetStatus($"{member.MemberCode} is now {(member.IsActive ? "inactive" : "active")}.");
        }
    }

    private void ResetPassword(MemberDto member)
    {
        var fields = InputDialog.Ask(FindForm()!, $"Reset password for {member.MemberCode}", "Reset", ("*New password", ""));
        if (fields != null && Run(() => App.Members.ResetPassword(member.Id, fields[0])))
        {
            SetStatus($"Reset the password for {member.MemberCode}.");
        }
    }

    private void WithSelected(Action<MemberDto> action)
    {
        if (Ui.Selected<MemberDto>(_grid) is { } member)
        {
            action(member);
        }
        else
        {
            Ui.Warn(FindForm()!, "Select a member first.");
        }
    }
}
