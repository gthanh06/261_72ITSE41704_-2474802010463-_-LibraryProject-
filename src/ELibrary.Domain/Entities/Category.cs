using ELibrary.Domain.Common;

namespace ELibrary.Domain.Entities;

public class Category : Entity
{
    private Category()
    {
        Name = string.Empty;
    }

    public Category(string name)
    {
        Name = Guard.Required(name, "Category name", 150);
    }

    public string Name { get; private set; }

    public void Rename(string name) => Name = Guard.Required(name, "Category name", 150);
}
