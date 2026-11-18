using ELibrary.Domain.Common;

namespace ELibrary.Domain.Entities;

public class Publisher : Entity
{
    private Publisher()
    {
        Name = string.Empty;
    }

    public Publisher(string name)
    {
        Name = Guard.Required(name, "Publisher name", 150);
    }

    public string Name { get; private set; }

    public void Rename(string name) => Name = Guard.Required(name, "Publisher name", 150);
}
