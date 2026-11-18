using ELibrary.Domain.Common;

namespace ELibrary.Domain.Entities;

public class Author : Entity
{
    private Author()
    {
        Name = string.Empty;
    }

    public Author(string name)
    {
        Name = Guard.Required(name, "Author name", 150);
    }

    public string Name { get; private set; }

    public void Rename(string name) => Name = Guard.Required(name, "Author name", 150);
}
