using ELibrary.Business.Security;
using ELibrary.Domain.Exceptions;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Dialogs;

/// <summary>UC01: the logged-in user changes his/her own password. A strength hint updates while typing.</summary>
internal sealed class ChangePasswordDialog : DialogBase
{
    private readonly AppServices _app;
    private readonly TextBox _current = Ui.TextBox(260, password: true);
    private readonly TextBox _new = Ui.TextBox(260, password: true);
    private readonly TextBox _confirm = Ui.TextBox(260, password: true);
    private readonly Label _strength = Ui.Label(PasswordPolicy.Description);

    public ChangePasswordDialog(AppServices app) : base("Change password", "Change password")
    {
        _app = app;
        _new.TextChanged += (_, _) => ShowStrength();
        SetContent(Ui.FieldTable(
            ("Current password", _current),
            ("New password", Ui.Column(_new, _strength)),
            ("Confirm new password", _confirm)));
    }

    protected override bool TrySave() => Run(() =>
    {
        if (_new.Text != _confirm.Text)
        {
            throw new ValidationException("Confirm new password", "The new passwords do not match.");
        }

        _app.Auth.ChangeOwnPassword(_current.Text, _new.Text);
        Toasts.Show(Owner ?? this, Tone.Success, "Password changed", "Use the new password the next time you log in.");
    });

    private void ShowStrength()
    {
        var username = _app.Session.User?.Username;
        var problems = PasswordPolicy.Problems(_new.Text, username);
        if (_new.TextLength == 0)
        {
            _strength.Text = PasswordPolicy.Description;
            _strength.ForeColor = Theme.Colors.TextMuted;
            return;
        }

        var strength = PasswordPolicy.Strength(_new.Text, username);
        _strength.Text = problems.Count > 0 ? problems[0] : $"Strength: {strength}";
        _strength.ForeColor = strength switch
        {
            PasswordStrength.TooWeak => Theme.Colors.Danger,
            PasswordStrength.Fair => Theme.Colors.Warning,
            _ => Theme.Colors.Success
        };
    }
}
