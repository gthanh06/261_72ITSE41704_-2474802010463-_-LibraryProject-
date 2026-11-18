using ELibrary.Domain.Exceptions;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

/// <summary>
/// Base of every screen shown in the main window: a page header, a status line and a common way to run
/// service calls. Screens only read controls, call one service method and display the result — no business rules.
/// </summary>
internal abstract class ScreenBase : UserControl
{
    private readonly PageHeader _header;
    private readonly StatusLine _status;

    protected ScreenBase(AppServices app, string title)
    {
        App = app;
        ScreenTitle = title;
        Dock = DockStyle.Fill;
        BackColor = Theme.Colors.Background;
        ForeColor = Theme.Colors.Text;
        Font = Theme.Fonts.Body;
        Padding = this.Px(Theme.Space.Xl, Theme.Space.L, Theme.Space.Xl, Theme.Space.Xs);
        _header = new PageHeader(title);
        _status = new StatusLine();
    }

    public string ScreenTitle { get; }

    /// <summary>The small label above the title (the sidebar group, e.g. "Circulation"). Display only.</summary>
    public string? Section
    {
        get => _header.Section;
        set => _header.Section = value;
    }

    protected AppServices App { get; }

    /// <summary>The page header, for screens that add a description or header actions.</summary>
    protected PageHeader Header => _header;

    /// <summary>Called when the screen is opened or shown again; screens reload their data here.</summary>
    public virtual void RefreshData()
    {
    }

    /// <summary>Lays out the screen: header, <paramref name="tops"/>, then <paramref name="body"/>, then the status line.</summary>
    protected void Compose(Control body, params Control[] tops) =>
        Ui.Stack(this, body, _status, new Control[] { _header }.Concat(tops).ToArray());

    /// <summary>Moves the keyboard focus once the screen is on screen (safe before the window exists).</summary>
    protected void FocusLater(Control control)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(() => control.Focus());
        }
    }

    /// <summary>Gives combo boxes and check boxes that the screen created itself the themed look (display only).</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTheme.ApplyToTree(this);
    }

    protected void SetStatus(string text, bool isError = false) =>
        _status.ShowMessage(text, isError ? Tone.Danger : Tone.Success);

    /// <summary>Shows a short notification in the corner of the main window.</summary>
    protected void Notify(Tone tone, string title, string? message = null) => Toasts.Show(this, tone, title, message);

    /// <summary>
    /// Runs a service call; on failure shows the reason and returns false. Expected errors (validation,
    /// business rules) appear in the status line and as a notification; unexpected errors open a dialog.
    /// </summary>
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
            ReportFailure(ex);
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>
    /// Shows why a service call failed: expected errors (validation, business rules) in the status line and
    /// as a notification; unexpected errors in a dialog.
    /// </summary>
    private void ReportFailure(Exception ex)
    {
        var message = ErrorHandler.Describe(ex);
        SetStatus(message, isError: true);
        if (ex is LibraryException and not DataAccessException)
        {
            Notify(Tone.Danger, "Could not complete this action", message);
        }
        else
        {
            ErrorHandler.Show(FindForm(), ex);
        }
    }
}
