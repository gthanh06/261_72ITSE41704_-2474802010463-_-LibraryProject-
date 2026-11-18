using System.Runtime.CompilerServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Gives a <see cref="DataGridView"/> the theme's table look: a flat header, taller rows, alternating
/// row colours, a hover highlight, a primary bar on the selected row, badges for <see cref="BadgeValue"/>
/// cells and an empty state when there are no rows. Only appearance is changed — data, selection and
/// sorting behave as before.
/// </summary>
internal static class GridStyle
{
    private static readonly ConditionalWeakTable<DataGridView, State> States = new();

    public static void Apply(DataGridView grid)
    {
        if (States.TryGetValue(grid, out _))
        {
            return;
        }

        var state = new State();
        States.Add(grid, state);

        var cellPadding = grid.Px(Theme.Space.S, 0, Theme.Space.S, 0);
        AntiFlicker.EnableDoubleBuffering(grid);
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Theme.Colors.Surface;
        grid.GridColor = Theme.Colors.Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = grid.Px(Theme.Size.GridHeaderHeight);
        grid.RowTemplate.Height = grid.Px(Theme.Size.GridRowHeight);
        grid.Font = Theme.Fonts.Body;

        var header = grid.ColumnHeadersDefaultCellStyle;
        header.BackColor = Theme.Colors.SurfaceSunken;
        header.ForeColor = Theme.Colors.TextMuted;
        header.SelectionBackColor = Theme.Colors.SurfaceSunken;
        header.SelectionForeColor = Theme.Colors.TextMuted;
        header.Font = Theme.Fonts.SmallSemibold;
        header.Padding = cellPadding;
        header.Alignment = DataGridViewContentAlignment.MiddleLeft;
        header.WrapMode = DataGridViewTriState.False;

        var cells = grid.DefaultCellStyle;
        cells.BackColor = Theme.Colors.Surface;
        cells.ForeColor = Theme.Colors.Text;
        cells.SelectionBackColor = Theme.Colors.PrimarySoft;
        cells.SelectionForeColor = Theme.Colors.Text;
        cells.Padding = cellPadding;
        cells.Font = Theme.Fonts.Body;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.Colors.RowAlternate;

        grid.CellMouseEnter += (_, e) => SetHover(grid, state, e.RowIndex);
        grid.MouseLeave += (_, _) => SetHover(grid, state, -1);
        grid.CellFormatting += (_, e) => ApplyHover(grid, state, e);
        grid.CellPainting += (_, e) => PaintBadge(grid, e);
        grid.RowPostPaint += (_, e) => PaintSelectionBar(grid, e);
        grid.Paint += (_, e) => PaintBorder(grid, e.Graphics);

        state.Empty = new EmptyState(Icons.Search, "Nothing to show",
            "There are no records for this view yet. Changing the search or filters may help.") { Visible = false };
        grid.Controls.Add(state.Empty);
        grid.RowsAdded += (_, _) => UpdateEmpty(grid, state);
        grid.RowsRemoved += (_, _) => UpdateEmpty(grid, state);
        grid.ColumnAdded += (_, e) =>
        {
            FitMinimumWidth(grid, e.Column, cellPadding.Horizontal);
            UpdateEmpty(grid, state);
        };
        grid.Resize += (_, _) => UpdateEmpty(grid, state);
    }

    /// <summary>Replaces the generic empty state of a styled grid with a message for this list.</summary>
    public static void SetEmptyState(DataGridView grid, string glyph, string title, string message, string? actionText = null, Action? action = null)
    {
        Apply(grid);
        var state = States.GetValue(grid, _ => new State());
        if (actionText != null && action != null)
        {
            state.Empty?.Dispose();
            state.Empty = new EmptyState(glyph, title, message, actionText, action) { Visible = false };
            grid.Controls.Add(state.Empty);
        }
        else
        {
            state.Empty?.SetContent(glyph, title, message);
        }

        UpdateEmpty(grid, state);
    }

    /// <summary>
    /// Keeps a column at least wide enough for its header and, for date columns, for a full date, so that
    /// proportional (Fill) widths never cut those down to "10/11/2…". Wider content still gets an ellipsis.
    /// </summary>
    private static void FitMinimumWidth(DataGridView grid, DataGridViewColumn column, int padding)
    {
        var width = Draw.TextWidth(column.HeaderText, Theme.Fonts.SmallSemibold);
        var format = column.DefaultCellStyle.Format;
        if (column.ValueType == typeof(DateTime) && !string.IsNullOrEmpty(format))
        {
            var sample = new DateTime(2000, 12, 28, 23, 58, 0).ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            width = Math.Max(width, Draw.TextWidth(sample, Theme.Fonts.Body));
        }

        column.MinimumWidth = Math.Max(column.MinimumWidth, width + padding + grid.Px(Theme.Space.Xs));
    }

    private static void UpdateEmpty(DataGridView grid, State state)
    {
        if (state.Empty == null)
        {
            return;
        }

        var show = grid.Columns.Count > 0 && grid.Rows.Count == 0;
        if (show)
        {
            var top = grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
            state.Empty.Bounds = new Rectangle(1, top, Math.Max(0, grid.ClientSize.Width - 2), Math.Max(0, grid.ClientSize.Height - top - 1));
        }

        state.Empty.Visible = show;
    }

    private static void SetHover(DataGridView grid, State state, int rowIndex)
    {
        if (state.HoverRow == rowIndex)
        {
            return;
        }

        var previous = state.HoverRow;
        state.HoverRow = rowIndex;
        if (previous >= 0 && previous < grid.Rows.Count)
        {
            grid.InvalidateRow(previous);
        }

        if (rowIndex >= 0 && rowIndex < grid.Rows.Count)
        {
            grid.InvalidateRow(rowIndex);
        }
    }

    private static void ApplyHover(DataGridView grid, State state, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex >= 0 && e.RowIndex == state.HoverRow && e.CellStyle != null && !grid.Rows[e.RowIndex].Selected)
        {
            e.CellStyle.BackColor = Draw.Blend(e.CellStyle.BackColor, Theme.Colors.BorderStrong, 0.28f);
        }
    }

    private static void PaintBadge(DataGridView grid, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Value is not BadgeValue badge || e.Graphics == null || e.CellStyle == null)
        {
            return;
        }

        e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
        var bounds = new Rectangle(e.CellBounds.X + e.CellStyle.Padding.Left, e.CellBounds.Y,
            e.CellBounds.Width - e.CellStyle.Padding.Horizontal, e.CellBounds.Height - 1);
        Badge.PaintPill(e.Graphics, grid, bounds, badge.Text, badge.Tone, Theme.Fonts.SmallSemibold);
        e.Handled = true;
    }

    private static void PaintSelectionBar(DataGridView grid, DataGridViewRowPostPaintEventArgs e)
    {
        if (!grid.Rows[e.RowIndex].Selected)
        {
            return;
        }

        using var brush = new SolidBrush(Theme.Colors.Primary);
        e.Graphics.FillRectangle(brush, e.RowBounds.X, e.RowBounds.Y, grid.Px(3), e.RowBounds.Height - 1);
    }

    private static void PaintBorder(DataGridView grid, Graphics g)
    {
        using var pen = new Pen(Theme.Colors.Border);
        g.DrawRectangle(pen, 0, 0, grid.Width - 1, grid.Height - 1);
    }

    private sealed class State
    {
        public int HoverRow { get; set; } = -1;

        public EmptyState? Empty { get; set; }
    }
}
