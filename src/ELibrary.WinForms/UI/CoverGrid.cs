namespace ELibrary.WinForms.UI;

/// <summary>One book in a <see cref="CoverGrid"/>.</summary>
internal sealed record CoverItem(int Id, string Title, string Authors, string Detail, Tone DetailTone, bool Dimmed);

/// <summary>
/// Books as a wall of covers, like a store shelf. Covers lift when hovered, scroll smoothly, and fade in
/// one after another when the list changes. Click to select, double-click or Enter to open; arrow keys,
/// Home/End and Page Up/Down move the selection. The table view stays available for screen readers
/// that prefer it.
/// </summary>
internal sealed class CoverGrid : Control
{
    private const float LiftSmoothingMs = 70f;
    private const float ScrollSmoothingMs = 90f;
    private const float AppearStaggerMs = 22f;
    private const float AppearMs = 260f;

    private readonly Dictionary<int, Bitmap> _covers = new();
    private readonly Action<double> _onFrame;
    private List<CoverItem> _items = new();
    private float[] _lift = Array.Empty<float>();
    private int _selected = -1;
    private int _hover = -1;
    private float _scroll;
    private float _scrollTarget;
    private int _maxScroll;
    private double _appearStart = double.NegativeInfinity;
    private double _lastFrame;
    private bool _animating;
    private int _dragOffset = -1;

    public CoverGrid()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Theme.Colors.Background;
        Font = Theme.Fonts.Body;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Titles";
        _onFrame = OnFrame;
    }

    public event EventHandler? SelectionChanged;

    /// <summary>Double-click or Enter on a cover.</summary>
    public event EventHandler? ItemActivated;

    public CoverItem? SelectedItem => _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;

    private int CardWidth => this.Px(148);
    private int CoverHeight => this.Px(206);
    private int TextHeight => this.Px(62);
    private int Gap => this.Px(Theme.Space.L);
    private int Pad => this.Px(Theme.Space.M);
    private int RowStride => CoverHeight + TextHeight + Gap;

    private int Columns => Math.Max(1, (Width - Pad * 2 + Gap) / (CardWidth + Gap));

    public void SetItems(IEnumerable<CoverItem> items)
    {
        var selectedId = SelectedItem?.Id;
        _items = items.ToList();
        _lift = new float[_items.Count];
        ClearCovers();
        _selected = selectedId is { } id ? _items.FindIndex(i => i.Id == id) : -1;
        _hover = -1;
        _scroll = _scrollTarget = 0;
        UpdateScrollRange();
        _appearStart = Theme.Motion.Enabled ? AnimationClock.NowMs : double.NegativeInfinity;
        Animate();
        Invalidate();
    }

    /// <summary>Selects the book with <paramref name="id"/> without raising <see cref="SelectionChanged"/>.</summary>
    public void SelectId(int? id)
    {
        var index = id is { } value ? _items.FindIndex(i => i.Id == value) : -1;
        if (index != _selected)
        {
            _selected = index;
            EnsureVisible(index);
            Invalidate();
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End
            or Keys.PageUp or Keys.PageDown or Keys.Enter
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_items.Count == 0)
        {
            return;
        }

        var visibleRows = Math.Max(1, Height / RowStride);
        var current = Math.Max(0, _selected);
        var next = e.KeyCode switch
        {
            Keys.Left => current - 1,
            Keys.Right => current + 1,
            Keys.Up => current - Columns,
            Keys.Down => current + Columns,
            Keys.PageUp => current - Columns * visibleRows,
            Keys.PageDown => current + Columns * visibleRows,
            Keys.Home => 0,
            Keys.End => _items.Count - 1,
            _ => int.MinValue
        };

        if (e.KeyCode == Keys.Enter && _selected >= 0)
        {
            ItemActivated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (next != int.MinValue)
        {
            Select(Math.Clamp(next, 0, _items.Count - 1));
            e.Handled = true;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var thumb = ThumbBounds();
        if (thumb.HasValue && thumb.Value.Contains(e.Location))
        {
            _dragOffset = e.Y - thumb.Value.Y;
            Capture = true;
            return;
        }

        var hit = HitTest(e.Location);
        if (hit >= 0)
        {
            Select(hit);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragOffset >= 0)
        {
            var track = Height - ThumbLength();
            if (track > 0)
            {
                _scroll = _scrollTarget = Math.Clamp((e.Y - _dragOffset) / (float)track * _maxScroll, 0, _maxScroll);
                Invalidate();
            }

            return;
        }

        SetHover(HitTest(e.Location));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragOffset = -1;
        Capture = false;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (HitTest(e.Location) is var hit and >= 0 && hit == _selected)
        {
            ItemActivated?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollTo(_scrollTarget - e.Delta / 120f * this.Px(140));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollRange();
        _scrollTarget = Math.Clamp(_scrollTarget, 0, _maxScroll);
        _scroll = Math.Clamp(_scroll, 0, _maxScroll);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ClearCovers();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var page = Draw.EffectiveBackColor(this);
        g.Clear(page);
        if (_items.Count == 0)
        {
            TextRenderer.DrawText(g, "No titles to show. Try another search.", Theme.Fonts.Body, ClientRectangle,
                Theme.Colors.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        Draw.HighQuality(g);
        var now = AnimationClock.NowMs;
        var first = Math.Max(0, (int)(_scroll - Pad) / RowStride * Columns);
        for (var i = first; i < _items.Count; i++)
        {
            var cell = CellBounds(i);
            if (cell.Top > Height)
            {
                break;
            }

            PaintItem(g, i, cell, page, Appear(i, now));
        }

        PaintScrollThumb(g);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AnimationClock.Unsubscribe(_onFrame);
            ClearCovers();
        }

        base.Dispose(disposing);
    }

    private void PaintItem(Graphics g, int index, Rectangle cell, Color page, float appear)
    {
        var item = _items[index];
        var lift = _lift[index];
        var cover = new Rectangle(cell.X, cell.Y - (int)Math.Round(this.Px(5) * lift), CardWidth, CoverHeight);

        Draw.Shadow(g, cover, this.Px(4), 3 + (int)Math.Round(5 * lift));
        g.DrawImageUnscaled(Cover(index, page), cover.Location);

        if (index == _selected)
        {
            var ring = Rectangle.Inflate(cover, this.Px(3), this.Px(3));
            Draw.StrokeRounded(g, Focused ? Theme.Colors.FocusRing : Theme.Colors.Primary, this.Px(2), ring, this.Px(7));
        }

        var textTop = cell.Y + CoverHeight + this.Px(Theme.Space.Xs);
        var titleRect = new Rectangle(cell.X, textTop, CardWidth, this.Px(36));
        TextRenderer.DrawText(g, item.Title, Theme.Fonts.SmallSemibold, titleRect, Theme.Colors.Text,
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
        TextRenderer.DrawText(g, item.Detail, Theme.Fonts.Caption, new Rectangle(cell.X, titleRect.Bottom, CardWidth, this.Px(18)),
            Tones.Strong(item.DetailTone), Draw.SingleLine);

        // Fading in (and inactive titles): veil the cell with the page colour.
        var veil = Math.Max(1f - appear, item.Dimmed ? 0.45f : 0f);
        if (veil > 0.01f)
        {
            using var brush = new SolidBrush(Color.FromArgb((int)(255 * veil), page));
            g.FillRectangle(brush, cell.X - this.Px(6), cover.Y - this.Px(6), CardWidth + this.Px(12), cell.Bottom - cover.Y + this.Px(12));
        }
    }

    private Bitmap Cover(int index, Color page)
    {
        if (!_covers.TryGetValue(index, out var bitmap))
        {
            var item = _items[index];
            bitmap = BookCover.Render(new Size(CardWidth, CoverHeight), item.Title, item.Authors, DeviceDpi / 96f, page);
            _covers[index] = bitmap;
        }

        return bitmap;
    }

    private void PaintScrollThumb(Graphics g)
    {
        var thumb = ThumbBounds();
        if (thumb == null)
        {
            return;
        }

        var color = Color.FromArgb(_dragOffset >= 0 ? 150 : 90, Theme.Colors.TextMuted);
        Draw.FillRounded(g, color, thumb.Value, thumb.Value.Width / 2f);
    }

    private Rectangle? ThumbBounds()
    {
        if (_maxScroll <= 0)
        {
            return null;
        }

        var length = ThumbLength();
        var y = (int)((Height - length) * (_scroll / _maxScroll));
        var width = this.Px(6);
        return new Rectangle(Width - width - this.Px(3), y, width, length);
    }

    private int ThumbLength() => Math.Max(this.Px(40), (int)(Height * (Height / (float)(Height + _maxScroll))));

    private Rectangle CellBounds(int index)
    {
        var columns = Columns;
        var used = columns * CardWidth + (columns - 1) * Gap;
        var left = Math.Max(Pad, (Width - used) / 2);
        var x = left + index % columns * (CardWidth + Gap);
        var y = Pad + this.Px(6) + index / columns * RowStride - (int)Math.Round(_scroll);
        return new Rectangle(x, y, CardWidth, CoverHeight + TextHeight);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var cell = CellBounds(i);
            if (cell.Top > Height)
            {
                break;
            }

            if (cell.Contains(p))
            {
                return i;
            }
        }

        return -1;
    }

    private void Select(int index)
    {
        if (index == _selected)
        {
            return;
        }

        _selected = index;
        EnsureVisible(index);
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Selection, index);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetHover(int index)
    {
        if (index == _hover)
        {
            return;
        }

        _hover = index;
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        Animate();
    }

    private void EnsureVisible(int index)
    {
        if (index < 0)
        {
            return;
        }

        var row = index / Columns;
        var top = Pad + row * RowStride;
        var bottom = top + RowStride;
        if (top < _scrollTarget)
        {
            ScrollTo(top - Pad);
        }
        else if (bottom > _scrollTarget + Height)
        {
            ScrollTo(bottom - Height + Pad);
        }
    }

    private void ScrollTo(float target)
    {
        _scrollTarget = Math.Clamp(target, 0, _maxScroll);
        if (!Theme.Motion.Enabled)
        {
            _scroll = _scrollTarget;
            Invalidate();
            return;
        }

        Animate();
    }

    private void UpdateScrollRange()
    {
        var rows = (_items.Count + Columns - 1) / Columns;
        var content = Pad * 2 + this.Px(6) + rows * RowStride - Gap;
        _maxScroll = Math.Max(0, content - Height);
    }

    private float Appear(int index, double now)
    {
        if (double.IsNegativeInfinity(_appearStart))
        {
            return 1f;
        }

        // Stagger by position on screen, not by index, so scrolling down later does not replay it.
        var cell = CellBounds(index);
        var order = Math.Max(0, cell.Top / RowStride) * Columns + index % Columns;
        return Easing.OutCubic(Math.Clamp((float)((now - _appearStart - order * AppearStaggerMs) / AppearMs), 0f, 1f));
    }

    /// <summary>Starts the frame loop that eases lifts, scrolling and the fade-in; it stops itself when all settle.</summary>
    private void Animate()
    {
        if (!Theme.Motion.Enabled)
        {
            for (var i = 0; i < _lift.Length; i++)
            {
                _lift[i] = i == _hover ? 1f : 0f;
            }

            Invalidate();
            return;
        }

        if (!_animating)
        {
            _animating = true;
            _lastFrame = AnimationClock.NowMs;
            AnimationClock.Subscribe(_onFrame);
        }
    }

    private void OnFrame(double now)
    {
        var dt = (float)Math.Min(50, now - _lastFrame);
        _lastFrame = now;
        var settled = true;

        var k = 1f - MathF.Exp(-dt / LiftSmoothingMs);
        for (var i = 0; i < _lift.Length; i++)
        {
            var target = i == _hover ? 1f : 0f;
            var diff = target - _lift[i];
            if (Math.Abs(diff) < 0.004f)
            {
                _lift[i] = target;
                continue;
            }

            _lift[i] += diff * k;
            settled = false;
        }

        var scrollDiff = _scrollTarget - _scroll;
        if (Math.Abs(scrollDiff) < 0.5f)
        {
            _scroll = _scrollTarget;
        }
        else
        {
            _scroll += scrollDiff * (1f - MathF.Exp(-dt / ScrollSmoothingMs));
            settled = false;
        }

        if (now - _appearStart < AppearMs + AppearStaggerMs * 40)
        {
            settled = false;
        }

        Invalidate();
        if (settled)
        {
            _animating = false;
            AnimationClock.Unsubscribe(_onFrame);
        }
    }

    private void ClearCovers()
    {
        foreach (var bitmap in _covers.Values)
        {
            bitmap.Dispose();
        }

        _covers.Clear();
    }
}
