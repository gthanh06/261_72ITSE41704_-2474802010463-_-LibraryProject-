using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Internal;
using ELibrary.Business.Security;
using ELibrary.Domain.Common;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>
/// UC02: the administrator manages staff accounts (administrators and librarians). Member logins are
/// created with the member (UC06) and are listed here only so that a locked one can be unlocked.
/// The system always keeps at least one administrator who can log in.
/// </summary>
public sealed class AccountService(IUnitOfWorkFactory factory, IPasswordHasher hasher, IClock clock, IUserSession session)
{
    public IReadOnlyList<AccountDto> GetAccounts(bool includeMembers = false)
    {
        session.Require(RoleCode.Administrator);
        var now = clock.Now;
        using var uow = factory.Create();
        var query = uow.UserAccounts.Query();
        if (!includeMembers)
        {
            query = query.Where(a => a.RoleId != (int)RoleCode.Member);
        }

        return query.OrderBy(a => a.RoleId).ThenBy(a => a.Username).AsEnumerable()
            .Select(a => ToDto(a, now)).ToList();
    }

    public int CreateStaffAccount(string? username, string? fullName, RoleCode role, string? password)
    {
        session.Require(RoleCode.Administrator);
        EnsureStaffRole(role);
        var name = Guard.Code(username, "Username", 50);
        PasswordPolicy.Validate(password, name);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var upper = name.ToUpperInvariant();
            if (uow.UserAccounts.Query().Any(a => a.Username.ToUpper() == upper))
            {
                throw new ValidationException("Username", $"The username '{name}' is already taken.");
            }

            var account = new UserAccount(name, hasher.Hash(password!), fullName ?? string.Empty, role, clock.Now);
            uow.UserAccounts.Add(account);
            uow.SaveChanges();
            return account.Id;
        });
    }

    public AccountDto UpdateStaffAccount(int accountId, string? fullName, RoleCode role)
    {
        var admin = session.Require(RoleCode.Administrator);
        EnsureStaffRole(role);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var account = RequireStaff(uow, accountId);
            if (account.Role != role)
            {
                if (account.Id == admin.AccountId)
                {
                    throw new BusinessRuleException(BusinessRuleException.StateRule, "You cannot change the role of your own account.");
                }

                if (account.Role == RoleCode.Administrator)
                {
                    EnsureAnotherAdministrator(uow, account.Id);
                }

                account.ChangeStaffRole(role);
            }

            account.ChangeFullName(fullName ?? string.Empty);
            uow.SaveChanges();
            return ToDto(account, clock.Now);
        });
    }

    /// <summary>Locks or unlocks an account. Unlocking also clears a temporary lock-out after failed logins.</summary>
    public void SetLocked(int accountId, bool locked)
    {
        var admin = session.Require(RoleCode.Administrator);
        using var uow = factory.Create();
        uow.ExecuteInTransaction(() =>
        {
            var account = uow.UserAccounts.GetRequired(accountId, "Account");
            if (locked)
            {
                if (account.Id == admin.AccountId)
                {
                    throw new BusinessRuleException(BusinessRuleException.StateRule, "You cannot lock your own account.");
                }

                if (account.Role == RoleCode.Administrator)
                {
                    EnsureAnotherAdministrator(uow, account.Id);
                }

                account.Lock();
            }
            else
            {
                if (account.Role == RoleCode.Member
                    && uow.Members.Query().Any(m => m.UserAccountId == account.Id && !m.IsActive))
                {
                    throw new BusinessRuleException(BusinessRuleException.StateRule,
                        "This member is inactive. Reactivate the member on the Members screen instead.");
                }

                account.Unlock();
            }

            uow.SaveChanges();
        });
    }

    public void ResetPassword(int accountId, string? newPassword)
    {
        session.Require(RoleCode.Administrator);
        using var uow = factory.Create();
        var account = RequireStaff(uow, accountId);
        PasswordPolicy.Validate(newPassword, account.Username);
        account.ChangePasswordHash(hasher.Hash(newPassword!));
        uow.SaveChanges();
    }

    private static AccountDto ToDto(UserAccount account, DateTime now) =>
        new(account.Id, account.Username, account.FullName, account.Role,
            account.IsLocked, account.IsLockedOut(now), account.LastLoginAt);

    private static void EnsureStaffRole(RoleCode role)
    {
        if (role is not (RoleCode.Administrator or RoleCode.Librarian))
        {
            throw new ValidationException("Role", "Select an administrator or librarian role for a staff account.");
        }
    }

    private static UserAccount RequireStaff(IUnitOfWork uow, int accountId)
    {
        var account = uow.UserAccounts.GetRequired(accountId, "Account");
        if (!account.IsStaff)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule,
                "Member logins are managed on the Members screen.");
        }

        return account;
    }

    /// <summary>Refuses a change that would leave no unlocked administrator.</summary>
    private static void EnsureAnotherAdministrator(IUnitOfWork uow, int accountId)
    {
        var others = uow.UserAccounts.Query()
            .Any(a => a.Id != accountId && a.RoleId == (int)RoleCode.Administrator && !a.IsLocked);
        if (!others)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule,
                "At least one unlocked administrator account must remain.");
        }
    }
}
