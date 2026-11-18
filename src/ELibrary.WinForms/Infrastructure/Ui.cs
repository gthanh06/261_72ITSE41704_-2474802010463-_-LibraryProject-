using System.Globalization;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Infrastructure;

/// <summary>A column of a grid filled by <see cref="Ui.Fill{T}"/>.</summary>
internal sealed record GridColumn<T>(string Header, Func<T, object?> Value, float Weight = 100, string? Format = null)
{
    public static GridColumn<T> Date(string header, Func<T, DateOnly?> value, float weight = 70) =>
        new(header, x => value(x)?.ToDateTime(TimeOnly.MinValue), weight, Ui.DateFormat);

    public static GridColumn<T> Money(string header, Func<T, decimal> value, float weight = 70) =>
        new(header, x => value(x), weight, Ui.MoneyFormat);
}

/// <summary>
/// Small helpers so that all screens look and behave the same. Colours, fonts and sizes come from
/// <see cref="Theme"/>; the controls are the themed ones from <c>ELibrary.WinForms.UI</c>.
/// </summary>
internal static class Ui
{
    public const string DateFormat = "dd/MM/yyyy";
    public const string MoneyFormat = "#,0";

    public static readonly Font TitleFont = Theme.Fonts.PageTitle;
    public static readonly Font BoldFont = Theme.Fonts.BodySemibold;
    public static readonly Color OverdueColor = Theme.Colors.DangerSoft;
    public static readonly Color HighlightColor = Theme.Colors.WarningSoft;
    public static readonly Color OkColor = Theme.Colors.Success;
    public static readonly Color ErrorColor = Theme.Colors.Danger;

    /// <summary>Row colour for records that are inactive (muted, not a warning).</summary>
    public static readonly Color InactiveRowColor = Theme.Colors.NeutralSoft;

    /// <summary>Row colour for items that exist but are not available right now.</summary>
    public static readonly Color UnavailableRowColor = Theme.Colors.SurfaceSunken;

    public static string Date(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string Date(DateOnly? date) => date.HasValue ? Date(date.Value) : string.Empty;

    public static string DateTime(DateTime? value) =>
        value.HasValue ? value.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : string.Empty;

    public static string Money(decimal amount) => Domain.Common.Money.Format(amount);

    public static Label Label(string text, bool bold = false)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = bold ? Theme.Colors.Text : Theme.Colors.TextMuted,
            Font = bold ? BoldFont : Theme.Fonts.SmallSemibold
        };
        // Centred on the 34 px controls of a row.
        label.Margin = label.Px(Theme.Space.Xxs, 13, Theme.Space.Xxs, Theme.Space.Xxs);
        return label;
    }

    public static TextBox TextBox(int width = 200, bool password = false)
    {
        var box = new ThemedTextBox { UseSystemPasswordChar = password };
        box.Width = box.Px(width);
        box.Margin = box.Px(Theme.Space.Xxs, 9, Theme.Space.Xxs, Theme.Space.Xxs);
        return box;
    }

    public static Button Button(string text, Action onClick, int width = 120, ButtonVariant variant = ButtonVariant.Secondary)
    {
        var button = new ThemedButton(text, variant);
        button.MinimumSize = new Size(button.Px(width), 0);
        button.Click += (_, _) => onClick();
        return button;
    }

    public static NumericUpDown Number(decimal min, decimal max, decimal value, decimal increment = 1, int width = 90)
    {
        var number = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Increment = increment,
            ThousandsSeparator = true,
            Font = Theme.Fonts.Body,
            ForeColor = Theme.Colors.Text
        };
        number.Width = number.Px(width);
        number.Margin = number.Px(Theme.Space.Xxs, 9, Theme.Space.Xxs, Theme.Space.Xxs);
        return number;
    }

    public static DateTimePicker DatePicker(DateOnly value, int width = 130)
    {
        var picker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = DateFormat,
            Value = value.ToDateTime(TimeOnly.MinValue),
            Font = Theme.Fonts.Body,
            CalendarFont = Theme.Fonts.Body
        };
        picker.Width = picker.Px(width);
        picker.Margin = picker.Px(Theme.Space.Xxs, 9, Theme.Space.Xxs, Theme.Space.Xxs);
        return picker;
    }

    public static DateOnly ToDateOnly(DateTimePicker picker) => DateOnly.FromDateTime(picker.Value);

    /// <summary>A horizontal row of controls (toolbars, input lines).</summary>
    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true
        };
        row.Padding = row.Px(0, 0, 0, Theme.Space.Xs);
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>Controls stacked vertically, sized to their content (used in dialogs).</summary>
    public static FlowLayoutPanel Column(params Control[] controls)
    {
        var column = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        column.Controls.AddRange(controls);
        return column;
    }

    /// <summary>A two-column "label: field" form.</summary>
    public static TableLayoutPanel FieldTable(params (string Label, Control Field)[] fields)
    {
        var table = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = fields.Length,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top
        };
        table.Padding = table.Px(0, Theme.Space.Xxs, 0, Theme.Space.Xxs);
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < fields.Length; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(Label(fields[i].Label), 0, i);
            table.Controls.Add(fields[i].Field, 1, i);
        }

        return table;
    }

    /// <summary>
    /// Adds docked children so that they appear in the given order: <paramref name="tops"/> from top
    /// to bottom, then <paramref name="fill"/> taking the remaining space, then <paramref name="bottom"/>.
    /// (WinForms docks the last-added control first.)
    /// </summary>
    public static void Stack(Control parent, Control fill, Control? bottom, params Control[] tops)
    {
        parent.SuspendLayout();
        fill.Dock = DockStyle.Fill;
        parent.Controls.Add(fill);
        if (bottom != null)
        {
            bottom.Dock = DockStyle.Bottom;
            parent.Controls.Add(bottom);
        }

        for (var i = tops.Length - 1; i >= 0; i--)
        {
            tops[i].Dock = DockStyle.Top;
            parent.Controls.Add(tops[i]);
        }

        parent.ResumeLayout(true);
    }

    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Dock = DockStyle.Fill
        };
        GridStyle.Apply(grid);
        return grid;
    }

    /// <summary>Fills a grid with one row per item; the item is kept in the row's Tag.</summary>
    public static void Fill<T>(DataGridView grid, IEnumerable<T> items, IReadOnlyList<GridColumn<T>> columns, Func<T, Color?>? rowColor = null)
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        var sameColumns = grid.Columns.Count == columns.Count &&
                          columns.Select((c, i) => grid.Columns[i].HeaderText == c.Header).All(same => same);
        if (!sameColumns)
        {
            grid.Columns.Clear();
            foreach (var column in columns)
            {
                var gridColumn = new DataGridViewTextBoxColumn
                {
                    HeaderText = column.Header,
                    FillWeight = column.Weight,
                    SortMode = DataGridViewColumnSortMode.Automatic
                };
                if (column.Format != null)
                {
                    gridColumn.DefaultCellStyle.Format = column.Format;
                    gridColumn.ValueType = column.Format == MoneyFormat ? typeof(decimal) : typeof(System.DateTime);
                }

                if (column.Format == MoneyFormat)
                {
                    gridColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }

                grid.Columns.Add(gridColumn);
            }
        }

        // Build the rows first so that each row already carries its item when the grid raises
        // SelectionChanged during AddRange.
        var rows = new List<DataGridViewRow>();
        foreach (var item in items)
        {
            var row = new DataGridViewRow { Tag = item, Height = grid.RowTemplate.Height };
            row.CreateCells(grid, columns.Select(c => c.Value(item)).ToArray());
            var color = rowColor?.Invoke(item);
            if (color.HasValue)
            {
                row.DefaultCellStyle.BackColor = color.Value;
            }

            rows.Add(row);
        }

        grid.Rows.AddRange(rows.ToArray());
        grid.ResumeLayout();
    }

    /// <summary>
    /// A split container whose splitter is placed at <paramref name="ratio"/> once the control has a real
    /// size (setting SplitterDistance before that throws).
    /// </summary>
    public static SplitContainer Split(Orientation orientation, double ratio)
    {
        var split = new SplitContainer { Orientation = orientation, Dock = DockStyle.Fill, BackColor = Theme.Colors.Background };
        split.SplitterWidth = split.Px(Theme.Space.M);
        var placed = false;
        split.SizeChanged += (_, _) =>
        {
            var length = orientation == Orientation.Horizontal ? split.Height : split.Width;
            if (placed || length < 100)
            {
                return;
            }

            placed = true;
            split.SplitterDistance = (int)(length * ratio);
        };
        return split;
    }

    public static T? Selected<T>(DataGridView grid) where T : class => grid.CurrentRow?.Tag as T;

    public static bool Confirm(IWin32Window owner, string message) =>
        MessageDialog.Confirm(owner, "Please confirm", message, "Yes", "No");

    public static void Info(IWin32Window owner, string message) =>
        MessageDialog.Inform(owner, "Information", message, Tone.Info);

    public static void Warn(IWin32Window owner, string message) =>
        MessageDialog.Inform(owner, "Check this first", message, Tone.Warning);

    /// <summary>Runs Enter-key actions on a text box (barcode scanners also send Enter).</summary>
    public static void OnEnter(TextBox box, Action action)
    {
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                action();
            }
        };
    }
}
