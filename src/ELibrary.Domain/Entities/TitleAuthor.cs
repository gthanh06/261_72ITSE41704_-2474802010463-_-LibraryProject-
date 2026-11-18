using ELibrary.Domain.Common;

namespace ELibrary.Domain.Entities;

/// <summary>Join entity for the many-to-many association between <see cref="Title"/> and <see cref="Author"/>.</summary>
public class TitleAuthor : Entity
{
    private TitleAuthor()
    {
    }

    public TitleAuthor(int titleId, int authorId, int position)
    {
        TitleId = titleId;
        AuthorId = authorId;
        Position = position;
    }

    public int TitleId { get; private set; }

    public int AuthorId { get; private set; }

    /// <summary>Order of the author on the title page (1 = first author).</summary>
    public int Position { get; private set; }
}
