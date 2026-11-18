namespace ELibrary.WinForms.UI;

/// <summary>
/// Entrance and exit motion for dialogs: they fade in while rising a few pixels into place, and fade out
/// (sinking slightly) when closed. The dialog's result is kept, so callers see no difference. Nothing
/// happens when animations are off.
/// </summary>
internal static class DialogMotion
{
    private const int RiseLogicalPx = 12;
    private const int EnterMs = 220;
    private const int ExitMs = 130;

    /// <summary>Call once from the dialog's constructor.</summary>
    public static void Apply(Form dialog)
    {
        if (!Theme.Motion.Enabled)
        {
            return;
        }

        var state = new State();
        dialog.Opacity = 0; // invisible until the entrance starts, so the first frame never pops in
        dialog.Shown += (_, _) => Enter(dialog, state);
        dialog.FormClosing += (_, e) => Exit(dialog, state, e);
    }

    private static void Enter(Form dialog, State state)
    {
        var rise = dialog.Px(RiseLogicalPx);
        var top = dialog.Top;
        dialog.Top = top + rise;
        state.Moving = Tween.Run(EnterMs, p =>
        {
            dialog.Opacity = p;
            dialog.Top = top + (int)Math.Round(rise * (1f - p));
        }, () => dialog.Opacity = 1.0, Easing.OutQuint);
    }

    private static void Exit(Form dialog, State state, FormClosingEventArgs e)
    {
        if (state.ExitDone || e.Cancel || !dialog.Visible || !Theme.Motion.Enabled ||
            e.CloseReason is not (CloseReason.UserClosing or CloseReason.None))
        {
            return;
        }

        // Hold the window open for the fade; WinForms clears the result of a cancelled close, so keep it.
        e.Cancel = true;
        if (state.Exiting)
        {
            return;
        }

        state.Exiting = true;
        state.Result = dialog.DialogResult;
        state.Moving?.Stop();
        var opacity = dialog.Opacity;
        var top = dialog.Top;
        var sink = dialog.Px(RiseLogicalPx / 2);
        Tween.Run(ExitMs, p =>
        {
            dialog.Opacity = opacity * (1f - p);
            dialog.Top = top + (int)Math.Round(sink * p);
            DimOverlay.SetVisibility(dialog, 1f - p);
        }, () =>
        {
            state.ExitDone = true;
            if (dialog.IsDisposed)
            {
                return;
            }

            if (dialog.Modal && state.Result != DialogResult.None)
            {
                dialog.DialogResult = state.Result; // closes the modal dialog with the original result
            }
            else
            {
                dialog.Close();
            }
        }, Easing.InOutCubic);
    }

    private sealed class State
    {
        public Tween? Moving;
        public bool Exiting;
        public bool ExitDone;
        public DialogResult Result;
    }
}
