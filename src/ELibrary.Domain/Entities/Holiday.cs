using ELibrary.Domain.Common;

namespace ELibrary.Domain.Entities;

public sealed class Holiday : Entity
{
    private Holiday() => Description = string.Empty;

    public Holiday(DateOnly date, string description)
    {
        Date = date;
        Description = Guard.Required(description, "Holiday description", 100);
    }

    public DateOnly Date { get; private set; }
    public string Description { get; private set; }
}
