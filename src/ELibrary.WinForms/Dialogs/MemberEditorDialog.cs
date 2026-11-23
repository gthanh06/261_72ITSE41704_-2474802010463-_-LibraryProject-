using ELibrary.Business.Dtos;
using ELibrary.Business.Security;
using ELibrary.Domain.Exceptions;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Dialogs;

internal sealed class MemberEditorDialog : DialogBase
{
    private readonly AppServices _app;
    private readonly MemberDto? _member;
    private readonly TextBox _name = Ui.TextBox(300);
    private readonly TextBox _email = Ui.TextBox(300);
    private readonly TextBox _phone = Ui.TextBox(180);
    private readonly TextBox _password = Ui.TextBox(220, password: true);
    private readonly ComboBox _type = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        DisplayMember = nameof(MembershipTypeDto.Name),
        Width = 220
    };
    private readonly DateTimePicker _expiry;

    public MemberEditorDialog(AppServices app, MemberDto? member, IReadOnlyList<MembershipTypeDto> types)
        : base(member == null ? "Register member" : $"Edit {member.MemberCode}", member == null ? "Register" : "Save")
    {
        _app = app;
        _member = member;
        _name.Text = member?.FullName ?? string.Empty;
        _email.Text = member?.Email ?? string.Empty;
        _phone.Text = member?.Phone ?? string.Empty;
        _expiry = Ui.DatePicker(member?.CardExpiryDate ?? app.Clock.Today.AddYears(1));
        foreach (var type in types.Where(t => t.IsActive || t.Id == member?.MembershipTypeId))
        {
            _type.Items.Add(type);
            if (type.Id == member?.MembershipTypeId)
            {
                _type.SelectedItem = type;
            }
        }

        if (_type.SelectedIndex < 0 && _type.Items.Count > 0)
        {
            _type.SelectedIndex = 0;
        }

        var fields = new List<(string Label, Control Field)>
        {
            ("Full name", _name), ("Email", _email), ("Phone", _phone),
            ("Membership type", _type), ("Card expires on", _expiry)
        };
        if (member == null)
        {
            fields.Add(("Initial password", Ui.Column(_password, Ui.Label(PasswordPolicy.Description))));
        }

        SetContent(Ui.FieldTable(fields.ToArray()));
    }

    public MemberDto? SavedMember { get; private set; }

    protected override bool TrySave() => Run(() =>
    {
        var type = _type.SelectedItem as MembershipTypeDto
            ?? throw new ValidationException("Membership type", "Select an active membership type.");
        var input = new MemberInput(_name.Text, _email.Text, _phone.Text, type.Id, Ui.ToDateOnly(_expiry));
        SavedMember = _member == null
            ? _app.Members.Register(input, _password.Text)
            : _app.Members.Update(_member.Id, input);
    });
}
