using ELibrary.WinForms.Infrastructure;

namespace ELibrary.WinForms.Dialogs;

/// <summary>
/// Base of all dialogs: a content area and OK / Cancel buttons. OK calls <see cref="TrySave"/>; the
/// dialog closes only when it returns true, so the user's input is kept after a validation error.
/// </summary>
internal abstract class DialogBase : Form
{
    private readonly Button _ok;

    protected DialogBase(string title, string okText = "Save")
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Font = new Font("Segoe UI", 9.75F);

        _ok = Ui.Button(okText, OnOk, 100);
        var cancel = new Button { Text = "Cancel", Width = 100, Height = 30, DialogResult = DialogResult.Cancel, Margin = new Padding(3) };
        Buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(0, 10, 0, 0)
        };
        Buttons.Controls.Add(cancel);
        Buttons.Controls.Add(_ok);
        AcceptButton = _ok;
        CancelButton = cancel;
    }

    protected FlowLayoutPanel Buttons { get; }

    /// <summary>Call once from the subclass constructor with the dialog's fields.</summary>
    protected void SetContent(Control content)
    {
        content.Dock = DockStyle.None;
        Controls.Add(Ui.Column(content, Buttons));
    }

    /// <summary>Validates and saves. Returns false (and shows the reason) when the dialog must stay open.</summary>
    protected abstract bool TrySave();

    protected bool Run(Action action)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            action();
            return true;
        }
        catch (Exception ex)
        {
            ErrorHandler.Show(this, ex);
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void OnOk()
    {
        if (TrySave())
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

/// <summary>Asks for one or more text values. Pass '*' as the first character of a label for a password field.</summary>
internal sealed class InputDialog : DialogBase
{
    private readonly TextBox[] _boxes;

    private InputDialog(string title, (string Label, string Initial)[] fields, string okText) : base(title, okText)
    {
        _boxes = fields.Select(f =>
        {
            var password = f.Label.StartsWith('*');
            var box = Ui.TextBox(320, password);
            box.Text = f.Initial;
            return box;
        }).ToArray();
        SetContent(Ui.FieldTable(fields.Select((f, i) => (f.Label.TrimStart('*'), (Control)_boxes[i])).ToArray()));
    }

    public static string[]? Ask(IWin32Window owner, string title, string okText, params (string Label, string Initial)[] fields)
    {
        using var dialog = new InputDialog(title, fields, okText);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._boxes.Select(b => b.Text).ToArray() : null;
    }

    protected override bool TrySave() => true;
}

/// <summary>Asks the user to pick one value from a list.</summary>
internal sealed class ChoiceDialog<T> : DialogBase where T : notnull
{
    private readonly ComboBox _combo;

    private ChoiceDialog(string title, string label, IReadOnlyList<T> choices, T initial) : base(title, "OK")
    {
        _combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        foreach (var choice in choices)
        {
            _combo.Items.Add(choice);
        }

        _combo.SelectedItem = initial;
        if (_combo.SelectedIndex < 0 && _combo.Items.Count > 0)
        {
            _combo.SelectedIndex = 0;
        }

        SetContent(Ui.FieldTable((label, _combo)));
    }

    public static T? Ask(IWin32Window owner, string title, string label, IReadOnlyList<T> choices, T initial)
    {
        using var dialog = new ChoiceDialog<T>(title, label, choices, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK ? (T?)dialog._combo.SelectedItem : default;
    }

    protected override bool TrySave() => _combo.SelectedItem != null;
}

/// <summary>Asks for a date.</summary>
internal sealed class DateDialog : DialogBase
{
    private readonly DateTimePicker _picker;

    private DateDialog(string title, string label, DateOnly initial) : base(title, "OK")
    {
        _picker = Ui.DatePicker(initial);
        SetContent(Ui.FieldTable((label, _picker)));
    }

    public static DateOnly? Ask(IWin32Window owner, string title, string label, DateOnly initial)
    {
        using var dialog = new DateDialog(title, label, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK ? Ui.ToDateOnly(dialog._picker) : null;
    }

    protected override bool TrySave() => true;
}
