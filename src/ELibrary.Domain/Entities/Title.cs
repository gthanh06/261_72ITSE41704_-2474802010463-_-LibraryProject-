using ELibrary.Domain.Common;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

/// <summary>A bibliographic title. The physical items are <see cref="Copy"/> objects; links are <see cref="EResource"/> objects.</summary>
public class Title : Entity
{
    public const int MinYear = 1450;
    public const int MaxYear = 2100;

    private Title()
    {
        Name = string.Empty;
    }

    public Title(string? isbn, string name, int? publisherId, int? categoryId, int? publicationYear, string? description)
    {
        Name = string.Empty;
        Update(isbn, name, publisherId, categoryId, publicationYear, description);
        IsActive = true;
    }

    /// <summary>Digits only (ISBN-10 or ISBN-13), or null for items without an ISBN.</summary>
    public string? Isbn { get; private set; }

    public string Name { get; private set; }

    public int? PublisherId { get; private set; }

    public int? CategoryId { get; private set; }

    public int? PublicationYear { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string? isbn, string name, int? publisherId, int? categoryId, int? publicationYear, string? description)
    {
        Isbn = Guard.Isbn(isbn);
        Name = Guard.Required(name, "Title", 300);
        if (publicationYear is < MinYear or > MaxYear)
        {
            throw new ValidationException("Publication year", $"Publication year must be between {MinYear} and {MaxYear}.");
        }

        PublisherId = publisherId;
        CategoryId = categoryId;
        PublicationYear = publicationYear;
        Description = Guard.Optional(description, "Description", 2000);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
