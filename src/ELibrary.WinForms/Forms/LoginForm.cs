using System.Drawing.Drawing2D;
using ELibrary.Domain.Exceptions;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Forms;

/// <summary>
/// UC01 log-in window: a brand panel with a few generated book covers on the left and the sign-in form on
/// the right. The check runs off the UI thread so the button's spinner keeps turning; a wrong password
/// keeps the username and selects the password for retyping.
/// </summary>
internal sealed class LoginForm : Form
{
    private readonly AppServices _app;
    private readonly FormField _username = new("Username", required: true, placeholder: "e.g. librarian1 or M0001", width: 320);
    private readonly FormField _password = new("Password", required: true, password: true, width: 320);
    private readonly InlineBanner _banner = new();
    private readonly ThemedButton _signIn = new("Sign in", ButtonVariant.Primary);
    private bool _busy;

    public LoginForm(AppServices app)
    {
        _app = app;
        Text = "E-Library – Sign in";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(this.Px(860), this.Px(520));
        BackColor = Theme.Colors.Surface;
        ForeColor = Theme.Colors.Text;
        Font = Theme.Fonts.Body;
        WindowChrome.Apply(this, Theme.Colors.Surface);

        var brand = new BrandPanel { Dock = DockStyle.Left, Width = this.Px(380) };
        var form = BuildForm();
        Controls.Add(form);
        Controls.Add(brand);
        AcceptButton = _signIn;
        _signIn.Click += async (_, _) => await SignInAsync();
        Shown += (_, _) => _username.FocusInput();
    }

    private Control BuildForm()
    {
        var heading = new Label { Text = "Welcome back", Font = Theme.Fonts.PageTitle, ForeColor = Theme.Colors.Text, AutoSize = true };
        var hint = new Label
        {
            Text = "Sign in with your staff account or your member code.",
            Font = Theme.Fonts.Body,
            ForeColor = Theme.Colors.TextMuted,
            AutoSize = true
        };
        hint.Margin = hint.Px(0, Theme.Space.Xxs, 0, Theme.Space.L);
        _signIn.MinimumSize = new Size(this.Px(320), this.Px(40));
        _signIn.Margin = _signIn.Px(0, Theme.Space.S, 0, 0);
        var exit = new ThemedButton("Exit", ButtonVariant.Secondary) { DialogResult = DialogResult.Cancel };
        exit.MinimumSize = new Size(this.Px(320), this.Px(36));
        exit.Margin = exit.Px(0, Theme.Space.Xs, 0, 0);
        CancelButton = exit;

        var column = Ui.Column(heading, hint, _banner, _username, _password, _signIn, exit);
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Colors.Surface };
        host.Controls.Add(column);
        host.Resize += (_, _) => column.Location = new Point(
            Math.Max(this.Px(Theme.Space.L), (host.ClientSize.Width - column.Width) / 2),
            Math.Max(this.Px(Theme.Space.L), (host.ClientSize.Height - column.Height) / 2));
        return host;
    }

    private async Task SignInAsync()
    {
        if (_busy)
        {
            return;
        }

        _banner.HideMessage();
        _username.Error = string.IsNullOrWhiteSpace(_username.Text) ? "Enter your username." : null;
        _password.Error = string.IsNullOrEmpty(_password.Text) ? "Enter your password." : null;
        if (_username.Error != null || _password.Error != null)
        {
            (_username.Error != null ? _username : _password).FocusInput();
            return;
        }

        _busy = true;
        _signIn.IsLoading = true;
        _username.Enabled = _password.Enabled = false;
        var username = _username.Text;
        var password = _password.Text;
        try
        {
            await Task.Run(() => _app.Auth.Login(username, password));
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (LibraryException ex) when (ex is not DataAccessException)
        {
            _banner.ShowMessage(ErrorHandler.Describe(ex), Tone.Danger, _username.Width);
            _username.Enabled = _password.Enabled = true;
            _password.FocusInput();
        }
        catch (Exception ex)
        {
            _username.Enabled = _password.Enabled = true;
            ErrorHandler.Show(this, ex);
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
            {
                _signIn.IsLoading = false;
            }
        }
    }

    /// <summary>The coloured half of the window: the name, a tagline and three generated covers fanned out.</summary>
    private sealed class BrandPanel : Panel
    {
        private static readonly (string Title, string Author, float Angle, float X, float Y)[] Books =
        {
            ("Truyện Kiều", "Nguyễn Du", -12f, 0.16f, 0.50f),
            ("Clean Code", "Robert C. Martin", 7f, 0.52f, 0.47f),
            ("Dế Mèn phiêu lưu ký", "Tô Hoài", -3f, 0.34f, 0.56f)
        };

        private readonly List<Bitmap> _covers = new();

        public BrandPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            BackColor = Theme.Colors.Primary;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Draw.HighQuality(g);
            var top = Theme.Colors.Primary;
            var bottom = Draw.Blend(Theme.Colors.Primary, Color.Black, Theme.Colors.IsDark ? 0.55f : 0.35f);
            using (var fill = new LinearGradientBrush(ClientRectangle, top, bottom, 70f))
            {
                g.FillRectangle(fill, ClientRectangle);
            }

            var pad = this.Px(Theme.Space.Xl);
            var ink = Theme.Colors.IsDark ? Theme.Colors.OnPrimary : Color.White;
            TextRenderer.DrawText(g, "E-Library", Theme.Fonts.PageTitle, new Point(pad, pad), ink);
            TextRenderer.DrawText(g, "Books, loans and reservations in one place.", Theme.Fonts.Body,
                new Rectangle(pad, pad + this.Px(40), Width - pad * 2, this.Px(48)), Color.FromArgb(220, ink),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            var size = new Size(this.Px(120), this.Px(172));
            if (_covers.Count == 0)
            {
                foreach (var book in Books)
                {
                    _covers.Add(BookCover.Render(size, book.Title, book.Author, DeviceDpi / 96f, top));
                }
            }

            for (var i = 0; i < Books.Length; i++)
            {
                var book = Books[i];
                var state = g.Save();
                g.TranslateTransform(Width * book.X + size.Width / 2f, Height * book.Y + size.Height / 2f);
                g.RotateTransform(book.Angle);
                using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                {
                    g.FillRectangle(shadow, -size.Width / 2f + this.Px(5), -size.Height / 2f + this.Px(8), size.Width, size.Height);
                }

                g.DrawImage(_covers[i], -size.Width / 2f, -size.Height / 2f, size.Width, size.Height);
                g.Restore(state);
            }

            TextRenderer.DrawText(g, "72ITSE41704 · Final project", Theme.Fonts.Small,
                new Point(pad, Height - pad - this.Px(16)), Color.FromArgb(190, ink));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _covers.ForEach(c => c.Dispose());
            }

            base.Dispose(disposing);
        }
    }
}
