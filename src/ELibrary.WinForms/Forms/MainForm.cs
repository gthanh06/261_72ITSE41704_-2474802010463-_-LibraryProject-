using ELibrary.Domain.Enums;
using ELibrary.WinForms.Dialogs;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.Screens;

namespace ELibrary.WinForms.Forms;

/// <summary>
/// Main window. The menu is built from the user's role (FR-01); each screen opens in a tab. The menu
/// is only a convenience — every service method checks the role again (NFR-04).
/// </summary>
internal sealed class MainForm : Form
{
    private readonly AppServices _app;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
    private readonly Dictionary<string, TabPage> _openTabs = new();
    private bool _opening;

    public MainForm(AppServices app)
    {
        _app = app;
        var user = app.User;
        Text = $"E-Library – {user.FullName} ({user.Role})";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1280, 800);
        MinimumSize = new Size(1000, 640);
        Font = new Font("Segoe UI", 9.75F);

        var menu = BuildMenu(user.Role);
        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel($"Logged in as {user.Username} – {user.Role}"));
        status.Items.Add(new ToolStripStatusLabel(Ui.Date(app.Clock.Today)) { Spring = true, TextAlign = ContentAlignment.MiddleRight });

        // Switching back to an open tab reloads its data (a new tab is loaded by Open).
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (!_opening)
            {
                (_tabs.SelectedTab?.Controls.OfType<ScreenBase>().FirstOrDefault())?.RefreshData();
            }
        };

        Controls.Add(_tabs);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        Shown += (_, _) => OpenHomeScreen(user.Role);
    }

    /// <summary>True when the user chose "Log out" (the program then shows the login window again).</summary>
    public bool LoggedOut { get; private set; }

    private MenuStrip BuildMenu(RoleCode role)
    {
        var menu = new MenuStrip();

        var catalogue = new ToolStripMenuItem("&Catalogue");
        catalogue.DropDownItems.Add(Item("Search catalogue", () => Open("catalogue", () => new CatalogueScreen(_app)), Keys.Control | Keys.F));
        menu.Items.Add(catalogue);

        if (role == RoleCode.Librarian)
        {
            var circulation = new ToolStripMenuItem("C&irculation");
            circulation.DropDownItems.Add(Item("Check out", () => Open("checkout", () => new CheckoutScreen(_app)), Keys.F2));
            circulation.DropDownItems.Add(Item("Return", () => Open("return", () => new ReturnScreen(_app)), Keys.F3));
            circulation.DropDownItems.Add(Item("Loans and renewals", () => Open("loans", () => new LoansScreen(_app)), Keys.F4));
            circulation.DropDownItems.Add(Item("Holds (reservations ready for pickup)", () => Open("holds", () => new HoldsScreen(_app)), Keys.F5));
            circulation.DropDownItems.Add(Item("Fines and payments", () => Open("fines", () => new FinesScreen(_app, memberMode: false)), Keys.F6));
            menu.Items.Add(circulation);

            var members = new ToolStripMenuItem("&Members");
            members.DropDownItems.Add(Item("Members", () => Open("members", () => new MembersScreen(_app)), Keys.F7));
            menu.Items.Add(members);
        }

        if (role == RoleCode.Member)
        {
            var mine = new ToolStripMenuItem("My &library");
            mine.DropDownItems.Add(Item("My loans", () => Open("my-loans", () => new MyLoansScreen(_app)), Keys.F2));
            mine.DropDownItems.Add(Item("My reservations", () => Open("my-reservations", () => new MyReservationsScreen(_app)), Keys.F3));
            mine.DropDownItems.Add(Item("My fines", () => Open("my-fines", () => new FinesScreen(_app, memberMode: true)), Keys.F4));
            menu.Items.Add(mine);
        }

        if (role is RoleCode.Administrator or RoleCode.Librarian)
        {
            var reports = new ToolStripMenuItem("&Reports");
            reports.DropDownItems.Add(Item("Reports", () => Open("reports", () => new ReportsScreen(_app)), Keys.F8));
            menu.Items.Add(reports);
        }

        if (role == RoleCode.Administrator)
        {
            var admin = new ToolStripMenuItem("&Administration");
            admin.DropDownItems.Add(Item("User accounts", () => Open("accounts", () => new AccountsScreen(_app)), null));
            admin.DropDownItems.Add(Item("Settings, holidays and backup", () => Open("settings", () => new SettingsScreen(_app)), null));
            menu.Items.Add(admin);
        }

        var account = new ToolStripMenuItem("&Account");
        account.DropDownItems.Add(Item("Close current tab", CloseCurrentTab, Keys.Control | Keys.W));
        account.DropDownItems.Add(new ToolStripSeparator());
        account.DropDownItems.Add(Item("Change password…", ChangePassword, null));
        account.DropDownItems.Add(Item("Log out", LogOut, null));
        account.DropDownItems.Add(Item("Exit", Close, null));
        menu.Items.Add(account);

        return menu;
    }

    private static ToolStripMenuItem Item(string text, Action action, Keys? shortcut)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        if (shortcut.HasValue)
        {
            item.ShortcutKeys = shortcut.Value;
        }

        return item;
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

    private void Open(string key, Func<ScreenBase> create)
    {
        if (_openTabs.TryGetValue(key, out var existing))
        {
            _tabs.SelectedTab = existing;
            return;
        }

        try
        {
            var screen = create();
            var page = new TabPage(screen.ScreenTitle) { Tag = key };
            page.Controls.Add(screen);
            _opening = true;
            try
            {
                _tabs.TabPages.Add(page);
                _openTabs[key] = page;
                _tabs.SelectedTab = page;
            }
            finally
            {
                _opening = false;
            }

            screen.RefreshData();
        }
        catch (Exception ex)
        {
            ErrorHandler.Show(this, ex);
        }
    }

    private void CloseCurrentTab()
    {
        if (_tabs.SelectedTab is not { } page)
        {
            return;
        }

        _openTabs.Remove((string)page.Tag!);
        _tabs.TabPages.Remove(page);
        page.Dispose();
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
