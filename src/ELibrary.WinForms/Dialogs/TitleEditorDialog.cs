using System.Globalization;
using ELibrary.Business.Dtos;
using ELibrary.WinForms.Infrastructure;

namespace ELibrary.WinForms.Dialogs;

/// <summary>UC04: create or edit a title. Authors are entered one per line; new publishers/categories are created.</summary>
internal sealed class TitleEditorDialog : DialogBase
{
    private readonly AppServices _app;
    private readonly int? _titleId;
    private readonly TextBox _isbn = Ui.TextBox(200);
    private readonly TextBox _name = Ui.TextBox(420);
    private readonly TextBox _authors = new() { Multiline = true, Width = 420, Height = 80, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _publisher = new() { Width = 300, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _category = new() { Width = 300, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _year = Ui.TextBox(80);
    private readonly TextBox _description = new() { Multiline = true, Width = 420, Height = 80, ScrollBars = ScrollBars.Vertical };

    public TitleEditorDialog(AppServices app, TitleDetailsDto? existing)
        : base(existing == null ? "New title" : "Edit title")
    {
        _app = app;
        _titleId = existing?.Id;
        _publisher.Items.AddRange(app.Catalogue.GetPublishers().Select(p => (object)p.Name).ToArray());
        _category.Items.AddRange(app.Catalogue.GetCategories().Select(c => (object)c.Name).ToArray());

        if (existing != null)
        {
            _isbn.Text = existing.Isbn;
            _name.Text = existing.Name;
            _authors.Text = string.Join(Environment.NewLine, existing.Authors);
            _publisher.Text = existing.Publisher;
            _category.Text = existing.Category;
            _year.Text = existing.PublicationYear?.ToString(CultureInfo.InvariantCulture);
            _description.Text = existing.Description;
        }

        var table = Ui.FieldTable(
            ("ISBN (optional)", _isbn),
            ("Title *", _name),
            ("Authors * (one per line)", _authors),
            ("Publisher", _publisher),
            ("Category", _category),
            ("Publication year", _year),
            ("Description", _description));
        SetContent(table);
    }

    public int SavedTitleId { get; private set; }

    protected override bool TrySave()
    {
        int? year = null;
        var yearText = _year.Text.Trim();
        if (yearText.Length > 0)
        {
            if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                Ui.Warn(this, "Publication year must be a whole number, for example 2019.");
                _year.Focus();
                return false;
            }

            year = parsed;
        }

        var authors = _authors.Lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var input = new TitleInput(_titleId, _isbn.Text, _name.Text, authors, _publisher.Text, _category.Text, year, _description.Text);
        return Run(() => SavedTitleId = _app.Catalogue.SaveTitle(input));
    }
}
