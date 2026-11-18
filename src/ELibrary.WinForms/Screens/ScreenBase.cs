using ELibrary.WinForms.Infrastructure;

namespace ELibrary.WinForms.Screens;

/// <summary>
/// Base of every screen shown in a tab: a title, a status line and a common way to run service calls.
/// Screens only read controls, call one service method and display the result — no business rules.
/// </summary>
internal abstract class ScreenBase : UserControl
{
    private readonly Label _title;
    private readonly Label _status;

    protected ScreenBase(AppServices app, string title)
    {
        App = app;
        ScreenTitle = title;
        Dock = DockStyle.Fill;
        Padding = new Padding(12);
        _title = new Label { Text = title, Font = Ui.TitleFont, AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
        _status = new Label { AutoSize = false, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
    }

    public string ScreenTitle { get; }

    protected AppServices App { get; }

    /// <summary>Called when the tab is opened or selected again; screens reload their data here.</summary>
    public virtual void RefreshData()
    {
    }

    /// <summary>Lays out the screen: title, <paramref name="tops"/>, then <paramref name="body"/>, then the status line.</summary>
    protected void Compose(Control body, params Control[] tops) =>
        Ui.Stack(this, body, _status, new Control[] { _title }.Concat(tops).ToArray());

    /// <summary>Moves the keyboard focus once the screen is on screen (safe before the window exists).</summary>
    protected void FocusLater(Control control)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(() => control.Focus());
        }
    }

    protected void SetStatus(string text, bool isError = false)
    {
        _status.Text = text;
        _status.ForeColor = isError ? Ui.ErrorColor : Ui.OkColor;
    }

    /// <summary>Runs a service call; on failure shows the reason and returns false.</summary>
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
            SetStatus(ErrorHandler.Describe(ex), isError: true);
            ErrorHandler.Show(FindForm(), ex);
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }
}
