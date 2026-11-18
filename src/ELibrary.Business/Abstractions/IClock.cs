namespace ELibrary.Business.Abstractions;

/// <summary>Source of the current time. Services never call DateTime.Now directly, so tests can fix "today".</summary>
public interface IClock
{
    DateTime Now { get; }

    DateOnly Today => DateOnly.FromDateTime(Now);
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
