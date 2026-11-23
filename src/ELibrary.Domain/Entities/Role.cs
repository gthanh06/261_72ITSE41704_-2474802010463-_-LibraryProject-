using ELibrary.Domain.Common;
using ELibrary.Domain.Enums;

namespace ELibrary.Domain.Entities;

public sealed class Role : Entity
{
    private Role() => Name = string.Empty;

    public Role(RoleCode code, string name)
    {
        Id = (int)code;
        Name = Guard.Required(name, "Role", 50);
    }

    public RoleCode Code => (RoleCode)Id;
    public string Name { get; private set; }
}
