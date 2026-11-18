using ELibrary.Domain.Enums;
using ELibrary.WinForms.Dialogs;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.Screens;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Forms;

/// <summary>
/// Main window. The sidebar is built from the user's role (FR-01); a screen is created the first time it is
/// opened and kept, so switching back keeps its state and reloads its data. The sidebar is only a
/// convenience — every service method checks the role again (NFR-04).
/// </summary>
internal sealed class MainForm : Form
{
    private readonly AppServices _app;
    private readonly Sidebar _sidebar = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, ScreenBase> _openScreens = new();
    private readonly List<string> _recentKeys = new();
    private readonly Dictionary<string, Action> _actions = new();
    private readonly Dictionary<Keys, Action> _shortcuts = new();
    private readonly Dictionary<string, string> _sections = new();
    private EmptyState? _noScreen;
    private string? _currentKey;
    private bool? _narrow;

    public MainForm(AppServices app)
    {
        _app = app;
        var user = app.User;
        Text = $"E-Library – {user.FullName} ({user.Role})";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1280, 800);
        MinimumSize = new Size(1000, 640);
        Font = Theme.Fonts.Body;
        BackColor = Theme.Colors.Background;
        _content.BackColor = Theme.Colors.Background;
        WindowChrome.Apply(this, Theme.Colors.Background);
        AntiFlicker.CompositeWhileResizing(this);

        BuildNavigation(user.Role);
        _sidebar.SetUser(user.FullName, user.Username, $"{user.Role} · {Ui.Date(app.Clock.Today)}");
        _sidebar.ItemInvoked += (_, item) =>
        {
            if (_actions.TryGetValue(item.Key, out var action))
            {
                action();
            }
        };

        Controls.Add(_content);
        Controls.Add(_sidebar);

        Resize += (_, _) => AdaptToWidth();
        Shown += (_, _) =>
        {
            AdaptToWidth();
            OpenHomeScreen(user.Role);
        };
    }

    /// <summary>True when the user chose "Log out" (the program then shows the login window again).</summary>
    public bool LoggedOut { get; private set; }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_shortcuts.TryGetValue(keyData, out var action))
        {
            action();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildNavigation(RoleCode role)
    {
        AddGroup("Library", ScreenItem("catalogue", "Catalogue", Icons.Library, Keys.Control | Keys.F, () => new CatalogueScreen(_app)));

        if (role == RoleCode.Librarian)
        {
            AddGroup("Circulation",
                ScreenItem("checkout", "Check out", Icons.CheckOut, Keys.F2, () => new CheckoutScreen(_app)),
                ScreenItem("return", "Return", Icons.Return, Keys.F3, () => new ReturnScreen(_app)),
                ScreenItem("loans", "Loans & renewals", Icons.Loans, Keys.F4, () => new LoansScreen(_app)),
                ScreenItem("holds", "Holds", Icons.Holds, Keys.F5, () => new HoldsScreen(_app)),
                ScreenItem("fines", "Fines & payments", Icons.Fines, Keys.F6, () => new FinesScreen(_app, memberMode: false)));
            AddGroup("Members", ScreenItem("members", "Members", Icons.Members, Keys.F7, () => new MembersScreen(_app)));
        }

        if (role == RoleCode.Member)
        {
            AddGroup("My library",
                ScreenItem("my-loans", "My loans", Icons.Book, Keys.F2, () => new MyLoansScreen(_app)),
                ScreenItem("my-reservations", "My reservations", Icons.Reservations, Keys.F3, () => new MyReservationsScreen(_app)),
                ScreenItem("my-fines", "My fines", Icons.Fines, Keys.F4, () => new FinesScreen(_app, memberMode: true)));
        }

        if (role is RoleCode.Administrator or RoleCode.Librarian)
        {
            AddGroup("Reports", ScreenItem("reports", "Reports", Icons.Reports, Keys.F8, () => new ReportsScreen(_app)));
        }

        if (role == RoleCode.Administrator)
        {
            AddGroup("Administration",
                ScreenItem("accounts", "User accounts", Icons.Accounts, null, () => new AccountsScreen(_app)),
                ScreenItem("settings", "Settings", Icons.Settings, null, () => new SettingsScreen(_app)));
        }

        _sidebar.AddFooterItems(
            Command("change-password", "Change password…", Icons.Password, ChangePassword),
            Command("logout", "Log out", Icons.SignOut, LogOut));
        _shortcuts[Keys.Control | Keys.W] = CloseCurrentScreen;
    }

    private void AddGroup(string title, params SidebarItem[] items)
    {
        foreach (var item in items)
        {
            _sections[item.Key] = title;
        }

        _sidebar.AddGroup(title, items);
    }

    private SidebarItem ScreenItem(string key, string text, string glyph, Keys? shortcut, Func<ScreenBase> create)
    {
        _actions[key] = () => Open(key, create);
        if (shortcut.HasValue)
        {
            _shortcuts[shortcut.Value] = _actions[key];
        }

        var shortcutText = shortcut.HasValue ? new KeysConverter().ConvertToString(shortcut.Value) : null;
        return new SidebarItem(key, text, glyph, shortcutText);
    }

    private SidebarItem Command(string key, string text, string glyph, Action action)
    {
        _actions[key] = action;
        return new SidebarItem(key, text, glyph);
    }

    private void OpenHomeScreen(RoleCode role)
    {
        switch (role)
        {
            case RoleCode.Librarian:
                Open("checkout", () => new CheckoutScreen(_app));
                break;
            case RoleCode.Member:
                Open("my-loans", () => new MyLoansScreen(_app));
                break;
            default:
                Open("reports", () => new ReportsScreen(_app));
                break;
        }
    }

    /// <summary>Shows the screen for <paramref name="key"/>, creating it on first use; a screen shown again reloads its data.</summary>
    private void Open(string key, Func<ScreenBase> create)
    {
        if (_openScreens.TryGetValue(key, out var existing))
        {
            if (key != _currentKey)
            {
                Display(key, existing);
                existing.RefreshData();
            }

            return;
        }

        try
        {
            var screen = create();
            screen.Section = _sections.GetValueOrDefault(key);
            screen.Visible = false;
            _content.Controls.Add(screen);
            _openScreens[key] = screen;
            Display(key, screen);
            screen.RefreshData();
        }
        catch (Exception ex)
        {
            ErrorHandler.Show(this, ex);
        }
    }

    private void Display(string key, ScreenBase screen)
    {
        // Swap screens with drawing paused so the old and new screen never show half-painted together.
        using (AntiFlicker.SuspendDrawing(_content))
        {
            _content.SuspendLayout();
            screen.Visible = true;
            screen.BringToFront();
            foreach (var other in _openScreens.Values.Where(s => s != screen))
            {
                other.Visible = false;
            }

            if (_noScreen != null)
            {
                _noScreen.Visible = false;
            }

            _content.ResumeLayout(true);
        }

        _currentKey = key;
        _recentKeys.Remove(key);
        _recentKeys.Add(key);
        _sidebar.SelectedKey = key;
        screen.SelectNextControl(null, true, true, true, false);
    }

    /// <summary>Ctrl+W: closes the current screen and returns to the one used before it.</summary>
    private void CloseCurrentScreen()
    {
        if (_currentKey == null || !_openScreens.Remove(_currentKey, out var screen))
        {
            return;
        }

        _recentKeys.Remove(_currentKey);
        _content.Controls.Remove(screen);
        screen.Dispose();
        _currentKey = null;

        if (_recentKeys.Count > 0)
        {
            var previous = _recentKeys[^1];
            var previousScreen = _openScreens[previous];
            Display(previous, previousScreen);
            previousScreen.RefreshData();
            return;
        }

        _sidebar.SelectedKey = null;
        _noScreen ??= new EmptyState(Icons.Library, "No screen open", "Choose a destination in the sidebar to continue.")
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Colors.Background
        };
        if (!_content.Controls.Contains(_noScreen))
        {
            _content.Controls.Add(_noScreen);
        }

        _noScreen.Visible = true;
    }

    /// <summary>Collapses the sidebar to icons on narrow windows (and expands it again when there is room).</summary>
    private void AdaptToWidth()
    {
        var narrow = ClientSize.Width < this.Px(1100);
        if (_narrow != narrow)
        {
            _narrow = narrow;
            _sidebar.Collapsed = narrow;
        }
    }

    private void ChangePassword()
    {
        using var dialog = new ChangePasswordDialog(_app);
        dialog.ShowDialog(this);
    }

    private void LogOut()
    {
        _app.Auth.Logout();
        LoggedOut = true;
        Close();
    }
}
