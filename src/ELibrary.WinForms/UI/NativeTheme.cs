using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ELibrary.WinForms.UI;

/// <summary>
/// Restyles standard controls that screens create themselves (ComboBox, CheckBox, RadioButton) so they
/// match the themed ones. Only appearance changes: items, values, events and data binding are untouched.
/// <see cref="ApplyToTree"/> is called by the screen and dialog base classes.
/// </summary>
internal static class NativeTheme
{
    private static readonly ConditionalWeakTable<ComboBox, ComboPainter> Combos = new();

    /// <summary>Styles every supported control under <paramref name="root"/> (each control only once).</summary>
    public static void ApplyToTree(Control root)
    {
        foreach (Control child in root.Controls)
        {
            switch (child)
            {
                case ComboBox combo:
                    Style(combo);
                    CentreInRow(combo);
                    break;
                case CheckBox or RadioButton:
                    child.Font = Theme.Fonts.Body;
                    child.ForeColor = Theme.Colors.Text;
                    CentreInRow(child);
                    break;
            }

            if (child is not (ComboBox or NumericUpDown or DateTimePicker))
            {
                ApplyToTree(child);
            }
        }
    }

    public static void Style(ComboBox combo)
    {
        if (Combos.TryGetValue(combo, out _) || combo.DrawMode == DrawMode.OwnerDrawVariable)
        {
            return;
        }

        combo.Font = Theme.Fonts.Body;
        combo.ForeColor = Theme.Colors.Text;
        combo.BackColor = Theme.Colors.Surface;
        combo.FlatStyle = FlatStyle.Flat;
        if (combo.DrawMode == DrawMode.Normal)
        {
            // Owner drawing gives roomy list items and lets the closed box be drawn in the theme colours.
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem += DrawItem;
        }

        // The box is ItemHeight plus a 6 px frame.
        combo.ItemHeight = combo.Px(Theme.Size.ControlHeight) - 6;
        Combos.Add(combo, new ComboPainter(combo));
    }

    /// <summary>In a toolbar row (FlowLayoutPanel) the other controls are 34 px high with a 4 px margin; centre on that line.</summary>
    private static void CentreInRow(Control control)
    {
        if (control.Parent is not FlowLayoutPanel { FlowDirection: FlowDirection.LeftToRight or FlowDirection.RightToLeft })
        {
            return;
        }

        var line = control.Px(Theme.Size.ControlHeight + Theme.Space.Xxs * 2);
        var height = control.AutoSize ? control.GetPreferredSize(Size.Empty).Height : control.Height;
        var top = Math.Max(0, (line - height) / 2);
        var bottom = Math.Max(0, line - height - top);
        control.Margin = new Padding(control.Margin.Left, top, control.Margin.Right, bottom);
    }

    private static void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        var inBox = (e.State & DrawItemState.ComboBoxEdit) != 0;
        var highlighted = !inBox && (e.State & DrawItemState.Selected) != 0;
        var back = !combo.Enabled ? Theme.Colors.SurfaceSunken : highlighted ? Theme.Colors.PrimarySoft : Theme.Colors.Surface;
        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        if (e.Index < 0 || e.Index >= combo.Items.Count)
        {
            return;
        }

        var text = combo.GetItemText(combo.Items[e.Index]);
        var left = combo.Px(inBox ? Theme.Space.Xxs : Theme.Space.S);
        var bounds = new Rectangle(e.Bounds.X + left, e.Bounds.Y, e.Bounds.Width - left, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, combo.Font, bounds,
            combo.Enabled ? Theme.Colors.Text : Theme.Colors.TextDisabled, Draw.SingleLine | TextFormatFlags.Left);
    }

    /// <summary>Paints the flat border and a chevron over the native frame and drop-down button.</summary>
    private sealed class ComboPainter : NativeWindow
    {
        private readonly ComboBox _combo;
        private bool _hover;

        public ComboPainter(ComboBox combo)
        {
            _combo = combo;
            combo.HandleCreated += (_, _) => AssignHandle(combo.Handle);
            combo.HandleDestroyed += (_, _) => ReleaseHandle();
            combo.MouseEnter += (_, _) => SetHover(true);
            combo.MouseLeave += (_, _) => SetHover(combo.ClientRectangle.Contains(combo.PointToClient(Control.MousePosition)));
            combo.Enter += (_, _) => combo.Invalidate();
            combo.Leave += (_, _) => combo.Invalidate();
            combo.DropDownClosed += (_, _) => combo.Invalidate();
            if (combo.IsHandleCreated)
            {
                AssignHandle(combo.Handle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is NativeChrome.WmPaint or NativeChrome.WmNcPaint)
            {
                Paint();
            }
        }

        private void SetHover(bool hover)
        {
            if (_hover != hover)
            {
                _hover = hover;
                _combo.Invalidate();
            }
        }

        private void Paint()
        {
            var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
            if (!GetComboBoxInfo(_combo.Handle, ref info))
            {
                return;
            }

            NativeChrome.PaintOnWindow(_combo, (g, _) =>
            {
                var surface = _combo.Enabled ? Theme.Colors.Surface : Theme.Colors.SurfaceSunken;
                var item = Rectangle.FromLTRB(info.ItemLeft, info.ItemTop, info.ItemRight, info.ItemBottom);

                // Everything outside the text area: the native frame and button.
                using (var region = new Region(new Rectangle(0, 0, _combo.Width, _combo.Height)))
                {
                    region.Exclude(item);
                    using var fill = new SolidBrush(surface);
                    g.FillRegion(fill, region);
                }

                var button = Rectangle.FromLTRB(info.ButtonLeft, 1, _combo.Width - 1, _combo.Height - 1);
                if (button.Width > 0)
                {
                    Draw.Glyph(g, Icons.ChevronDown, 7.5f, _combo.Enabled ? Theme.Colors.TextMuted : Theme.Colors.TextDisabled, button);
                }

                using var pen = new Pen(NativeChrome.BorderColor(_combo, _combo.Focused || _combo.DroppedDown, _hover));
                g.DrawRectangle(pen, 0, 0, _combo.Width - 1, _combo.Height - 1);
            });
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ComboBoxInfo
        {
            public int Size;
            public int ItemLeft, ItemTop, ItemRight, ItemBottom;
            public int ButtonLeft, ButtonTop, ButtonRight, ButtonBottom;
            public int ButtonState;
            public IntPtr Combo, Item, List;
        }

        [DllImport("user32.dll")]
        private static extern bool GetComboBoxInfo(IntPtr hwnd, ref ComboBoxInfo info);
    }
}
