using ELibrary.Domain.Common;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

public sealed class UserAccount : Entity
{
    private UserAccount()
    {
        Username = string.Empty;
        PasswordHash = string.Empty;
        FullName = string.Empty;
    }

    public UserAccount(string username, string passwordHash, string fullName, RoleCode role, DateTime createdAt)
    {
        Username = Guard.Code(username, "Username", 50);
        PasswordHash = Guard.Required(passwordHash, "Password hash", 200);
        FullName = Guard.Required(fullName, "Full name", 100);
        RoleId = (int)role;
        CreatedAt = createdAt;
    }

    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public string FullName { get; private set; }
    public int RoleId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsLocked { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndsAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public RoleCode Role => (RoleCode)RoleId;
    public bool IsStaff => Role is RoleCode.Administrator or RoleCode.Librarian;

    public void ChangeFullName(string name) => FullName = Guard.Required(name, "Full name", 100);

    public void ChangePasswordHash(string hash)
    {
        PasswordHash = Guard.Required(hash, "Password hash", 200);
        ResetFailedLogins();
    }

    public bool IsLockedOut(DateTime now) => LockoutEndsAt > now;

    public void RegisterFailedLogin(DateTime now)
    {
        if (IsLocked || IsLockedOut(now))
        {
            return;
        }

        if (LockoutEndsAt.HasValue)
        {
            ResetFailedLogins();
        }

        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
        {
            LockoutEndsAt = now.AddMinutes(5);
        }
    }

    public void RegisterSuccessfulLogin(DateTime now)
    {
        ResetFailedLogins();
        LastLoginAt = now;
    }

    public void ChangeStaffRole(RoleCode role)
    {
        if (!IsStaff || role is not (RoleCode.Administrator or RoleCode.Librarian))
        {
            throw new ValidationException("Role", "Select an administrator or librarian role for a staff account.");
        }

        RoleId = (int)role;
    }

    public void Lock() => IsLocked = true;

    public void Unlock()
    {
        IsLocked = false;
        ResetFailedLogins();
    }

    private void ResetFailedLogins()
    {
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
    }
}
