using ELibrary.Business.Dtos;
using ELibrary.Domain.Enums;
using ELibrary.WinForms.Infrastructure;
using ELibrary.WinForms.UI;

namespace ELibrary.WinForms.Screens;

/// <summary>
/// UC11 (Librarian): the hold shelf. Opening the screen first expires the holds that were not collected
/// in time (BR5), so the list always shows copies that are really waiting for their members.
/// </summary>
internal sealed class HoldsScreen : ScreenBase
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly CheckBox _showQueue = new() { Text = "Also show members waiting", AutoSize = true, Padding = new Padding(0, 8, 0, 0) };

    public HoldsScreen(AppServices app) : base(app, "Holds")
    {
        Header.Description = "Copies kept on the hold shelf for members who reserved them. Uncollected holds expire automatically.";
        _showQueue.CheckedChanged += (_, _) => RefreshData();
        var actions = Ui.Row(
            Ui.Button("Refresh", RefreshData, 90),
            Ui.Button("Cancel reservation…", CancelSelected, 160),
            _showQueue);
        Compose(_grid, actions);
    }

    public override void RefreshData()
    {
        var includeWaiting = _showQueue.Checked;
        _ = RunAsync("holds", () =>
        {
            var processed = App.Reservations.ProcessHolds();
            return (processed, App.Reservations.GetActiveHolds(includeWaiting));
        }, result =>
        {
            var (processed, holds) = result;
            var today = App.Clock.Today;
            Ui.Fill(_grid, holds, new GridColumn<ReservationDto>[]
            {
                new("Title", r => r.TitleName, 190),
                new("Member", r => $"{r.MemberCode} · {r.MemberName}", 150),
                new("Status", r => r.Status == ReservationStatus.OnHold ? "On the hold shelf" : $"Waiting (#{r.QueuePosition})", 110),
                new("Copy", r => r.HeldBarcode, 80),
                GridColumn<ReservationDto>.Date("Collect by", r => r.HoldExpiresOn, 80),
                new("Reserved", r => r.ReservedAt, 85, Ui.DateFormat)
            }, r => r.HoldExpiresOn == today ? Theme.Colors.WarningSoft : null);

            var onShelf = holds.Count(h => h.Status == ReservationStatus.OnHold);
            SetStatus($"{onShelf} cop{(onShelf == 1 ? "y" : "ies")} on the hold shelf.");
            if (processed.ExpiredHolds > 0)
            {
                Notify(Tone.Info, $"{processed.ExpiredHolds} hold(s) expired",
                    $"{processed.CopiesReassigned} cop(ies) passed to the next member, {processed.CopiesReleased} returned to the shelf.");
            }
        }, _grid);
    }

    private void CancelSelected()
    {
        if (Ui.Selected<ReservationDto>(_grid) is not { } reservation)
        {
            Ui.Warn(FindForm()!, "Select a reservation first.");
            return;
        }

        var message = reservation.Status == ReservationStatus.OnHold
            ? $"Copy {reservation.HeldBarcode} will go to the next member in the queue, or back to the shelf."
            : "The member will leave the queue for this title.";
        if (MessageDialog.Confirm(FindForm(), $"Cancel {reservation.MemberCode}'s reservation of '{reservation.TitleName}'?",
                message, "Cancel reservation", "Keep", destructive: true)
            && Run(() => App.Reservations.Cancel(reservation.Id)))
        {
            Notify(Tone.Success, "Reservation cancelled");
            RefreshData();
        }
    }
}

/// <summary>UC10 (Member): the member's reservations — queue position, hold shelf pickups and history.</summary>
internal sealed class MyReservationsScreen : ScreenBase
{
    private readonly DataGridView _grid = Ui.Grid();

    public MyReservationsScreen(AppServices app) : base(app, "My reservations")
    {
        Header.Description = "Reserve a title from the Catalogue when no copy is on the shelf. Collect held copies before the date shown.";
        var actions = Ui.Row(
            Ui.Button("Refresh", RefreshData, 90),
            Ui.Button("Cancel reservation…", CancelSelected, 160));
        Compose(_grid, actions);
    }

    public override void RefreshData() => _ = RunAsync("mine", App.Reservations.GetMyReservations, reservations =>
    {
        Ui.Fill(_grid, reservations, new GridColumn<ReservationDto>[]
        {
            new("Title", r => r.TitleName, 210),
            new("Status", Describe, 190),
            new("Reserved", r => r.ReservedAt, 85, Ui.DateFormat),
            new("Closed", r => r.ClosedAt, 85, Ui.DateFormat)
        }, r => r.Status == ReservationStatus.OnHold ? Theme.Colors.SuccessSoft : null);

        var ready = reservations.FirstOrDefault(r => r.Status == ReservationStatus.OnHold);
        var active = reservations.Count(r => r.IsActive);
        SetStatus(ready != null
            ? $"'{ready.TitleName}' is waiting for you at the desk until {Ui.Date(ready.HoldExpiresOn)}."
            : $"{active} active reservation(s).");
    }, _grid);

    private static string Describe(ReservationDto r) => r.Status switch
    {
        ReservationStatus.Waiting => r.QueuePosition == 1 ? "Waiting – you are next" : $"Waiting – number {r.QueuePosition} in the queue",
        ReservationStatus.OnHold => $"Ready to collect by {Ui.Date(r.HoldExpiresOn)}",
        ReservationStatus.Fulfilled => "Collected",
        ReservationStatus.Cancelled => "Cancelled",
        _ => "Expired (not collected)"
    };

    private void CancelSelected()
    {
        if (Ui.Selected<ReservationDto>(_grid) is not { IsActive: true } reservation)
        {
            Ui.Warn(FindForm()!, "Select an active reservation first.");
            return;
        }

        if (MessageDialog.Confirm(FindForm(), $"Cancel your reservation of '{reservation.TitleName}'?",
                "You will lose your place in the queue.", "Cancel reservation", "Keep", destructive: true)
            && Run(() => App.Reservations.Cancel(reservation.Id)))
        {
            Notify(Tone.Success, "Reservation cancelled");
            RefreshData();
        }
    }
}
