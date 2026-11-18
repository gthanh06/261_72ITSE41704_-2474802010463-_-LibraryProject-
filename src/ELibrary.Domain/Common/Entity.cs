namespace ELibrary.Domain.Common;

/// <summary>Base class of every persistent domain object. The identifier is assigned by the database.</summary>
public abstract class Entity
{
    public int Id { get; protected set; }
}
