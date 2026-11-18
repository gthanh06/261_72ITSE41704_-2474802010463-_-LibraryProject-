namespace ELibrary.WinForms.UI;

/// <summary>One destination in the <see cref="Sidebar"/>.</summary>
internal sealed class SidebarItem
{
    public SidebarItem(string key, string text, string glyph, string? shortcutText = null)
    {
        Key = key;
        Text = text;
        Glyph = glyph;
        ShortcutText = shortcutText;
    }

    public string Key { get; }

    public string Text { get; }

    public string Glyph { get; }

    /// <summary>Shown greyed at the right of the item, e.g. "F2".</summary>
    public string? ShortcutText { get; }
}

/// <summary>
/// The main navigation: brand, grouped items with icons, the signed-in user and account actions at the
/// bottom. The selected item has a highlight and a brass marker that slides to it. It can be collapsed
/// to an icon rail (tooltips then show the names). Keyboard: Tab into it, Up/Down/Home/End to move,
/// Enter or Space to open. Screen readers see every item as a menu item.
/// </summary>
internal sealed class Sidebar : Control
{
    private readonly List<(string? Title, SidebarItem[] Items)> _groups = new();
    private readonly List<SidebarItem> _footer = new();
    private readonly List<Entry> _entries = new();
    private readonly List<(string Title, Rectangle Bounds)> _headers = new();
    private readonly List<int> _dividers = new();
    private readonly ToolTip _tip = new() { InitialDelay = 250, ShowAlways = true };
    private string _userName = string.Empty;
    private string _userKey = string.Empty;
    private string _userDetail = string.Empty;
    private Rectangle _itemsArea;
    private int _footerTop;
    private int _scroll;
    private int _maxScroll;
    private int _hover = -1;
    private int _pressed = -1;
    private int _focus;
    private bool _keyboardCue = true;
    private string? _selectedKey;
    private bool _collapsed;
    private float _indicatorY = -1;
    private Tween? _indicatorTween;
    private int? _layoutWidth;
    private Tween? _widthTween;
    private int _dockIndex = -1;
    private string? _tipText;

    public Sidebar()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        Dock = DockStyle.Left;
        Width = this.Px(Theme.Size.SidebarWidth);
        BackColor = Theme.Colors.Sidebar;
        Font = Theme.Fonts.Body;
        AccessibleRole = AccessibleRole.MenuBar;
        AccessibleName = "Main navigation";
    }

    /// <summary>Raised when an item or a footer action is chosen (click, Enter or Space).</summary>
    public event EventHandler<SidebarItem>? ItemInvoked;

    public string BrandText { get; set; } = "E-Library";

    /// <summary>The width the content is laid out for; differs from <see cref="Control.Width"/> only while the rail animates.</summary>
    private int LayoutWidth => _layoutWidth ?? Width;

    public bool Collapsed
    {
        get => _collapsed;
        set
        {
            if (_collapsed == value)
            {
                return;
            }

            _collapsed = value;
            _scroll = 0;
            var target = this.Px(value ? Theme.Size.SidebarCollapsedWidth : Theme.Size.SidebarWidth);
            _widthTween?.Stop();
            if (!Theme.Motion.Enabled || !IsHandleCreated || !Visible || Parent == null || Width == target)
            {
                _layoutWidth = null;
                Redock(); // in case an animation was interrupted
                Width = target;
                Rebuild(animateIndicator: false);
                return;
            }

            // Float above the content while the width animates, so the screens are laid out once at the
            // start and once at the end instead of on every frame.
            FloatOverContent();
            _layoutWidth = target;
            Rebuild(animateIndicator: false);
            var from = Width;
            _widthTween = Tween.Run(Theme.Motion.NormalMs + 60,
                p => Width = (int)Math.Round(from + (target - from) * p),
                () =>
                {
                    _layoutWidth = null;
                    Redock();
                    Rebuild(animateIndicator: false);
                },
                Easing.InOutCubic);
        }
    }

    public string? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (_selectedKey == value)
            {
                return;
            }

            _selectedKey = value;
            var index = _entries.FindIndex(e => e.Kind == EntryKind.Item && e.Item!.Key == value);
            if (index >= 0)
            {
                _focus = index;
                EnsureVisible(index);
            }

            Rebuild(animateIndicator: true);
            AccessibilityNotifyClients(AccessibleEvents.Selection, index);
        }
    }

    public void SetUser(string fullName, string userKey, string detail)
    {
        _userName = fullName;
        _userKey = userKey;
        _userDetail = detail;
        Invalidate();
    }

    public void AddGroup(string? title, params SidebarItem[] items)
    {
        _groups.Add((title, items));
        Rebuild(animateIndicator: false);
    }

    /// <summary>Icon actions next to the user at the bottom (change password, log out).</summary>
    public void AddFooterItems(params SidebarItem[] items)
    {
        _footer.AddRange(items);
        Rebuild(animateIndicator: false);
    }

    internal int EntryCount => _entries.Count;

    internal string EntryName(int index) => _entries[index].Item?.Text ?? (_collapsed ? "Expand navigation" : "Collapse navigation");

    internal Rectangle EntryBounds(int index) => _entries[index].Bounds;

    internal bool IsEntrySelected(int index) => _entries[index].Item?.Key == _selectedKey && _entries[index].Kind == EntryKind.Item;

    internal bool IsEntryFocused(int index) => Focused && _focus == index;

    internal void InvokeEntry(int index)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        var entry = _entries[index];
        if (entry.Kind == EntryKind.Toggle)
        {
            Collapsed = !Collapsed;
            _focus = 0;
            Invalidate();
            return;
        }

        ItemInvoked?.Invoke(this, entry.Item!);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SidebarAccessibleObject(this);

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter or Keys.Space
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_entries.Count == 0)
        {
            return;
        }

        var next = e.KeyCode switch
        {
            Keys.Down => (_focus + 1) % _entries.Count,
            Keys.Up => (_focus - 1 + _entries.Count) % _entries.Count,
            Keys.Home => 0,
            Keys.End => _entries.Count - 1,
            _ => -1
        };

        if (next >= 0)
        {
            MoveFocus(next);
            e.Handled = true;
        }
        else if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            InvokeEntry(_focus);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        var selected = _entries.FindIndex(x => x.Kind == EntryKind.Item && x.Item!.Key == _selectedKey);
        if (_keyboardCue && selected >= 0)
        {
            _focus = selected;
        }

        AccessibilityNotifyClients(AccessibleEvents.Focus, _focus);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardCue = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHover(HitTest(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _keyboardCue = false;
        if (e.Button == MouseButtons.Left)
        {
            _pressed = HitTest(e.Location);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var hit = HitTest(e.Location);
        var pressed = _pressed;
        _pressed = -1;
        Invalidate();
        if (e.Button == MouseButtons.Left && hit >= 0 && hit == pressed)
        {
            _focus = hit;
            InvokeEntry(hit);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_maxScroll > 0)
        {
            _scroll = Math.Clamp(_scroll - Math.Sign(e.Delta) * this.Px(40), 0, _maxScroll);
            Rebuild(animateIndicator: false);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Rebuild(animateIndicator: false);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Colors.Sidebar);
        Draw.HighQuality(g);

        PaintBrand(g);

        var state = g.Save();
        g.SetClip(_itemsArea);
        foreach (var (title, bounds) in _headers)
        {
            TextRenderer.DrawText(g, title, Theme.Fonts.SmallSemibold, bounds, Theme.Colors.SidebarTextMuted,
                (Draw.SingleLine & ~TextFormatFlags.VerticalCenter) | TextFormatFlags.NoPadding | TextFormatFlags.Bottom);
        }

        using (var divider = new Pen(Theme.Colors.SidebarDivider))
        {
            foreach (var y in _dividers)
            {
                g.DrawLine(divider, this.Px(16), y, LayoutWidth - this.Px(16), y);
            }
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Kind == EntryKind.Item)
            {
                PaintItem(g, i);
            }
        }

        PaintIndicator(g);
        g.Restore(state);

        PaintFooter(g);
        PaintFocus(g);

        using var edge = new Pen(Theme.Colors.SidebarDivider);
        g.DrawLine(edge, Width - 1, 0, Width - 1, Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tip.Dispose();
            _indicatorTween?.Dispose();
            _widthTween?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PaintBrand(Graphics g)
    {
        if (!_collapsed)
        {
            var mark = this.Px(32);
            var markRect = new Rectangle(this.Px(Theme.Space.M), this.Px(18), mark, mark);
            Draw.FillRounded(g, Theme.Colors.Accent, markRect, this.Px(8));
            Draw.Glyph(g, Icons.Library, 12f, Theme.Colors.OnPrimary, markRect);
            var textLeft = markRect.Right + this.Px(10);
            TextRenderer.DrawText(g, BrandText, Theme.Fonts.Brand, new Rectangle(textLeft, markRect.Y - this.Px(1), LayoutWidth - textLeft - this.Px(48), mark),
                Theme.Colors.SidebarActiveText, Draw.SingleLine | TextFormatFlags.NoPadding);
        }

        var toggle = _entries.FindIndex(e => e.Kind == EntryKind.Toggle);
        if (toggle >= 0)
        {
            var bounds = _entries[toggle].Bounds;
            if (_hover == toggle || _pressed == toggle)
            {
                Draw.FillRounded(g, Theme.Colors.SidebarHover, bounds, this.Px(Theme.Radius.Control));
            }

            Draw.Glyph(g, Icons.Menu, 11f, Theme.Colors.SidebarText, bounds);
        }
    }

    private void PaintItem(Graphics g, int index)
    {
        var entry = _entries[index];
        var item = entry.Item!;
        var selected = item.Key == _selectedKey;
        var bounds = entry.Bounds;
        var radius = this.Px(Theme.Radius.Control);

        if (selected)
        {
            Draw.FillRounded(g, Theme.Colors.SidebarActive, bounds, radius);
        }
        else if (_pressed == index)
        {
            Draw.FillRounded(g, Theme.Colors.SidebarActive, bounds, radius);
        }
        else if (_hover == index)
        {
            Draw.FillRounded(g, Theme.Colors.SidebarHover, bounds, radius);
        }

        var fore = selected ? Theme.Colors.SidebarActiveText : Theme.Colors.SidebarText;
        var iconBox = this.Px(20);
        var iconRect = _collapsed
            ? new Rectangle(bounds.X + (bounds.Width - iconBox) / 2, bounds.Y + (bounds.Height - iconBox) / 2, iconBox, iconBox)
            : new Rectangle(bounds.X + this.Px(Theme.Space.S), bounds.Y + (bounds.Height - iconBox) / 2, iconBox, iconBox);
        Draw.Glyph(g, item.Glyph, 11.5f, selected ? Theme.Colors.Primary : fore, iconRect);

        if (_collapsed)
        {
            return;
        }

        var textLeft = iconRect.Right + this.Px(Theme.Space.S);
        var shortcutWidth = 0;
        if (item.ShortcutText != null)
        {
            shortcutWidth = TextRenderer.MeasureText(item.ShortcutText, Theme.Fonts.Caption).Width;
            TextRenderer.DrawText(g, item.ShortcutText, Theme.Fonts.Caption,
                new Rectangle(bounds.Right - shortcutWidth - this.Px(10), bounds.Y, shortcutWidth, bounds.Height),
                Theme.Colors.SidebarTextMuted, Draw.SingleLine | TextFormatFlags.Right);
        }

        TextRenderer.DrawText(g, item.Text, selected ? Theme.Fonts.BodySemibold : Theme.Fonts.Body,
            new Rectangle(textLeft, bounds.Y, bounds.Right - textLeft - shortcutWidth - this.Px(14), bounds.Height),
            fore, Draw.SingleLine);
    }

    private void PaintIndicator(Graphics g)
    {
        if (_indicatorY < 0 || _selectedKey == null)
        {
            return;
        }

        var height = this.Px(Theme.Size.ControlHeight + 4) - this.Px(16);
        var rect = new RectangleF(this.Px(4), _indicatorY + this.Px(8), this.Px(3), height);
        Draw.FillRounded(g, Theme.Colors.Primary, rect, this.Px(2));
    }

    private void PaintFooter(Graphics g)
    {
        using (var pen = new Pen(Theme.Colors.SidebarDivider))
        {
            g.DrawLine(pen, 0, _footerTop, LayoutWidth, _footerTop);
        }

        var avatar = this.Px(36);
        var avatarRect = _collapsed
            ? new Rectangle((LayoutWidth - avatar) / 2, _footerTop + this.Px(Theme.Space.S), avatar, avatar)
            : new Rectangle(this.Px(Theme.Space.M), _footerTop + this.Px(Theme.Space.M), avatar, avatar);
        using (var brush = new SolidBrush(Avatar.ColorFor(_userKey)))
        {
            g.FillEllipse(brush, avatarRect);
        }

        using (var ring = new Pen(Theme.Colors.SidebarDivider, this.Px(2)))
        {
            g.DrawEllipse(ring, avatarRect);
        }

        TextRenderer.DrawText(g, Avatar.Initials(_userName), Theme.Fonts.SmallSemibold, avatarRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (!_collapsed)
        {
            var footerButtons = _entries.Where(e => e.Kind == EntryKind.Footer).Select(e => e.Bounds.Left).DefaultIfEmpty(LayoutWidth).Min();
            var textLeft = avatarRect.Right + this.Px(10);
            var textWidth = footerButtons - textLeft - this.Px(6);
            var line = TextRenderer.MeasureText("Ag", Theme.Fonts.BodySemibold).Height;
            TextRenderer.DrawText(g, _userName, Theme.Fonts.BodySemibold, new Rectangle(textLeft, avatarRect.Y - this.Px(1), textWidth, line),
                Theme.Colors.SidebarActiveText, Draw.SingleLine | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _userDetail, Theme.Fonts.Small, new Rectangle(textLeft, avatarRect.Y + line, textWidth, line),
                Theme.Colors.SidebarTextMuted, Draw.SingleLine | TextFormatFlags.NoPadding);
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.Kind != EntryKind.Footer)
            {
                continue;
            }

            if (_hover == i || _pressed == i)
            {
                Draw.FillRounded(g, _pressed == i ? Theme.Colors.SidebarActive : Theme.Colors.SidebarHover, entry.Bounds, this.Px(Theme.Radius.Control));
            }

            Draw.Glyph(g, entry.Item!.Glyph, 11f, Theme.Colors.SidebarText, entry.Bounds);
        }
    }

    private void PaintFocus(Graphics g)
    {
        if (!Focused || !_keyboardCue || _focus < 0 || _focus >= _entries.Count)
        {
            return;
        }

        var bounds = _entries[_focus].Bounds;
        Draw.StrokeRounded(g, Theme.Colors.FocusRing, this.Px(Theme.Size.FocusRingWidth), bounds, this.Px(Theme.Radius.Control));
    }

    private void Rebuild(bool animateIndicator)
    {
        _entries.Clear();
        _headers.Clear();
        _dividers.Clear();

        var toggleSize = this.Px(32);
        var toggleRect = _collapsed
            ? new Rectangle((LayoutWidth - toggleSize) / 2, this.Px(18), toggleSize, toggleSize)
            : new Rectangle(LayoutWidth - toggleSize - this.Px(Theme.Space.S), this.Px(18), toggleSize, toggleSize);
        _entries.Add(new Entry(EntryKind.Toggle, null, toggleRect));

        // Footer (fixed at the bottom)
        var footerButton = this.Px(32);
        var footerHeight = _collapsed
            ? this.Px(Theme.Space.S) + this.Px(36) + this.Px(Theme.Space.Xs) + _footer.Count * (footerButton + this.Px(4)) + this.Px(Theme.Space.S)
            : this.Px(68);
        _footerTop = Height - footerHeight;
        var footerEntries = new List<Entry>();
        for (var i = 0; i < _footer.Count; i++)
        {
            var rect = _collapsed
                ? new Rectangle((LayoutWidth - footerButton) / 2,
                    _footerTop + this.Px(Theme.Space.S) + this.Px(36) + this.Px(Theme.Space.Xs) + i * (footerButton + this.Px(4)),
                    footerButton, footerButton)
                : new Rectangle(LayoutWidth - this.Px(Theme.Space.S) - (_footer.Count - i) * (footerButton + this.Px(2)),
                    _footerTop + (footerHeight - footerButton) / 2, footerButton, footerButton);
            footerEntries.Add(new Entry(EntryKind.Footer, _footer[i], rect));
        }

        // Items (scrollable between the brand and the footer)
        var top = this.Px(68);
        _itemsArea = new Rectangle(0, top, LayoutWidth, Math.Max(0, _footerTop - top));
        var itemHeight = this.Px(Theme.Size.ControlHeight + 4);
        var margin = _collapsed ? this.Px(10) : this.Px(Theme.Space.S);
        var firstItem = _entries.Count;
        var y = top + this.Px(Theme.Space.Xxs);
        for (var gi = 0; gi < _groups.Count; gi++)
        {
            var (title, items) = _groups[gi];
            if (_collapsed)
            {
                if (gi > 0)
                {
                    _dividers.Add(y + this.Px(6));
                    y += this.Px(Theme.Space.S);
                }
            }
            else if (title != null)
            {
                var headerHeight = this.Px(gi == 0 ? 24 : 40);
                _headers.Add((title, new Rectangle(this.Px(Theme.Space.L), y, LayoutWidth - this.Px(Theme.Space.L) * 2, headerHeight - this.Px(6))));
                y += headerHeight;
            }

            foreach (var item in items)
            {
                _entries.Add(new Entry(EntryKind.Item, item, new Rectangle(margin, y, LayoutWidth - margin * 2, itemHeight)));
                y += itemHeight + this.Px(2);
            }
        }

        // Positions above are unscrolled; clamp the scroll offset to the content, then shift.
        var contentBottom = y + this.Px(Theme.Space.S);
        _maxScroll = Math.Max(0, contentBottom - _footerTop);
        _scroll = Math.Clamp(_scroll, 0, _maxScroll);
        if (_scroll > 0)
        {
            for (var i = firstItem; i < _entries.Count; i++)
            {
                var e = _entries[i];
                _entries[i] = e with { Bounds = new Rectangle(e.Bounds.X, e.Bounds.Y - _scroll, e.Bounds.Width, e.Bounds.Height) };
            }

            for (var i = 0; i < _headers.Count; i++)
            {
                var (title, b) = _headers[i];
                _headers[i] = (title, new Rectangle(b.X, b.Y - _scroll, b.Width, b.Height));
            }

            for (var i = 0; i < _dividers.Count; i++)
            {
                _dividers[i] -= _scroll;
            }
        }

        _entries.AddRange(footerEntries);
        _focus = Math.Clamp(_focus, 0, Math.Max(0, _entries.Count - 1));

        UpdateIndicator(animateIndicator);
        Invalidate();
    }

    /// <summary>Leaves the parent's docking and sits on top of the content (for the width animation).</summary>
    private void FloatOverContent()
    {
        var parent = Parent;
        if (parent == null || Dock == DockStyle.None)
        {
            return;
        }

        _dockIndex = parent.Controls.GetChildIndex(this);
        var bounds = Bounds;
        using (AntiFlicker.SuspendDrawing(parent))
        {
            parent.SuspendLayout();
            Dock = DockStyle.None;
            Bounds = bounds;
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            BringToFront();
            parent.ResumeLayout(true);
        }
    }

    /// <summary>Docks to the left again at the position in the z-order it had before <see cref="FloatOverContent"/>.</summary>
    private void Redock()
    {
        var parent = Parent;
        if (parent == null || Dock != DockStyle.None || _dockIndex < 0)
        {
            return;
        }

        using (AntiFlicker.SuspendDrawing(parent))
        {
            parent.SuspendLayout();
            Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Dock = DockStyle.Left;
            parent.Controls.SetChildIndex(this, Math.Min(_dockIndex, parent.Controls.Count - 1));
            parent.ResumeLayout(true);
        }

        _dockIndex = -1;
    }

    private void UpdateIndicator(bool animate)
    {
        var selected = _entries.FirstOrDefault(e => e.Kind == EntryKind.Item && e.Item!.Key == _selectedKey);
        if (selected == null)
        {
            _indicatorY = -1;
            return;
        }

        float target = selected.Bounds.Y;
        _indicatorTween?.Stop();
        if (!animate || _indicatorY < 0)
        {
            _indicatorY = target;
            return;
        }

        var from = _indicatorY;
        _indicatorTween = Tween.Run(Theme.Motion.NormalMs + 40, p =>
        {
            _indicatorY = from + (target - from) * p;
            Invalidate();
        }, ease: Easing.InOutCubic);
    }

    private void EnsureVisible(int index)
    {
        var bounds = _entries[index].Bounds;
        if (_entries[index].Kind != EntryKind.Item || _maxScroll == 0)
        {
            return;
        }

        if (bounds.Top < _itemsArea.Top)
        {
            _scroll -= _itemsArea.Top - bounds.Top + this.Px(Theme.Space.Xs);
        }
        else if (bounds.Bottom > _itemsArea.Bottom)
        {
            _scroll += bounds.Bottom - _itemsArea.Bottom + this.Px(Theme.Space.Xs);
        }

        _scroll = Math.Clamp(_scroll, 0, _maxScroll);
    }

    private void MoveFocus(int index)
    {
        _keyboardCue = true;
        _focus = index;
        EnsureVisible(index);
        Rebuild(animateIndicator: false);
        AccessibilityNotifyClients(AccessibleEvents.Focus, index);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.Kind == EntryKind.Item && !_itemsArea.Contains(p))
            {
                continue;
            }

            if (entry.Bounds.Contains(p))
            {
                return i;
            }
        }

        return -1;
    }

    private void SetHover(int index)
    {
        if (_hover == index)
        {
            return;
        }

        _hover = index;
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        var needsTip = index >= 0 && (_collapsed || _entries[index].Kind != EntryKind.Item);
        var tip = needsTip ? EntryName(index) : null;
        if (tip != _tipText)
        {
            _tipText = tip;
            _tip.SetToolTip(this, tip);
        }

        Invalidate();
    }

    private enum EntryKind
    {
        Toggle,
        Item,
        Footer
    }

    private sealed record Entry(EntryKind Kind, SidebarItem? Item, Rectangle Bounds);

    private sealed class SidebarAccessibleObject : ControlAccessibleObject
    {
        private readonly Sidebar _owner;

        public SidebarAccessibleObject(Sidebar owner) : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.MenuBar;

        public override int GetChildCount() => _owner.EntryCount;

        public override AccessibleObject? GetChild(int index) =>
            index >= 0 && index < _owner.EntryCount ? new EntryAccessibleObject(_owner, this, index) : null;

        public override AccessibleObject? GetFocused()
        {
            for (var i = 0; i < _owner.EntryCount; i++)
            {
                if (_owner.IsEntryFocused(i))
                {
                    return GetChild(i);
                }
            }

            return base.GetFocused();
        }
    }

    private sealed class EntryAccessibleObject : AccessibleObject
    {
        private readonly Sidebar _owner;
        private readonly AccessibleObject _parent;
        private readonly int _index;

        public EntryAccessibleObject(Sidebar owner, AccessibleObject parent, int index)
        {
            _owner = owner;
            _parent = parent;
            _index = index;
        }

        public override string Name => _owner.EntryName(_index);

        public override AccessibleRole Role => AccessibleRole.MenuItem;

        public override AccessibleObject Parent => _parent;

        public override Rectangle Bounds => _owner.RectangleToScreen(_owner.EntryBounds(_index));

        public override string DefaultAction => "Open";

        public override AccessibleStates State
        {
            get
            {
                var state = AccessibleStates.Focusable | AccessibleStates.Selectable;
                if (_owner.IsEntrySelected(_index))
                {
                    state |= AccessibleStates.Selected;
                }

                if (_owner.IsEntryFocused(_index))
                {
                    state |= AccessibleStates.Focused;
                }

                return state;
            }
        }

        public override void DoDefaultAction() => _owner.InvokeEntry(_index);
    }
}
