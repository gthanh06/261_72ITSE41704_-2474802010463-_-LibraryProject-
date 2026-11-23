namespace ELibrary.Domain.Services;

public sealed class LibraryCalendar
{
    private readonly HashSet<DateOnly> _holidays;
    private readonly bool _closedOnSundays;

    public LibraryCalendar(IEnumerable<DateOnly> holidays, bool closedOnSundays = true)
    {
        _holidays = holidays.ToHashSet();
        _closedOnSundays = closedOnSundays;
    }

    public bool IsOpeningDay(DateOnly day) =>
        (!_closedOnSundays || day.DayOfWeek != DayOfWeek.Sunday) && !_holidays.Contains(day);

    public DateOnly FirstOpeningDayOnOrAfter(DateOnly day)
    {
        while (!IsOpeningDay(day))
        {
            day = day.AddDays(1);
        }

        return day;
    }

    public DateOnly AddOpeningDays(DateOnly start, int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        for (var i = 0; i < days; i++)
        {
            start = FirstOpeningDayOnOrAfter(start.AddDays(1));
        }

        return start;
    }

    public int CountChargeableDays(DateOnly due, DateOnly end)
    {
        var count = 0;
        while (due < end)
        {
            due = due.AddDays(1);
            if (IsOpeningDay(due))
            {
                count++;
            }
        }

        return count;
    }
}
