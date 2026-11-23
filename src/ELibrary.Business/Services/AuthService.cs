using ELibrary.Business.Abstractions;
using ELibrary.Business.Security;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>
/// UC01: log in, log out and change the own password. Five wrong passwords in a row lock the account
/// for five minutes (NFR-05); the error message never says whether the username exists.
/// </summary>
public sealed class AuthService(IUnitOfWorkFactory factory, IPasswordHasher hasher, IClock clock, IUserSession session)
{
    public const string WrongCredentials = "The username or password is incorrect.";

    public CurrentUser Login(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            throw new ValidationException("Username", "Enter your username and password.");
        }

        var now = clock.Now;
        using var uow = factory.Create();
        var name = username.Trim().ToUpperInvariant();
        var account = uow.UserAccounts.Query().FirstOrDefault(a => a.Username.ToUpper() == name)
                      ?? throw new AuthenticationFailedException(WrongCredentials);

        if (account.IsLocked)
        {
            throw new AuthenticationFailedException("This account is locked. Please contact the library administrator.");
        }

        if (account.IsLockedOut(now))
        {
            throw new AuthenticationFailedException(LockoutMessage(account));
        }

        if (!hasher.Verify(password, account.PasswordHash))
        {
            account.RegisterFailedLogin(now);
            uow.SaveChanges();
            throw new AuthenticationFailedException(account.IsLockedOut(now) ? LockoutMessage(account) : WrongCredentials);
        }

        int? memberId = null;
        if (account.Role == RoleCode.Member)
        {
            var member = uow.Members.Query().FirstOrDefault(m => m.UserAccountId == account.Id)
                         ?? throw new AuthenticationFailedException("This member account is not linked to a library member.");
            if (!member.IsActive)
            {
                throw new AuthenticationFailedException("This library membership is inactive. Please contact the library.");
            }

            memberId = member.Id;
        }

        account.RegisterSuccessfulLogin(now);
        uow.SaveChanges();
        var user = new CurrentUser(account.Id, account.Username, account.FullName, account.Role, memberId);
        session.SignIn(user);
        return user;
    }

    public void Logout() => session.SignOut();

    public void ChangeOwnPassword(string? currentPassword, string? newPassword)
    {
        var user = session.RequireAuthenticated();
        using var uow = factory.Create();
        var account = uow.UserAccounts.GetById(user.AccountId)
                      ?? throw new NotFoundException("Account", user.Username);
        if (string.IsNullOrEmpty(currentPassword) || !hasher.Verify(currentPassword, account.PasswordHash))
        {
            throw new ValidationException("Current password", "The current password is incorrect.");
        }

        PasswordPolicy.Validate(newPassword, account.Username, "New password");
        if (newPassword == currentPassword)
        {
            throw new ValidationException("New password", "Choose a password that is different from the current one.");
        }

        account.ChangePasswordHash(hasher.Hash(newPassword!));
        uow.SaveChanges();
    }

    private static string LockoutMessage(UserAccount account) =>
        $"Too many failed attempts. Try again after {account.LockoutEndsAt:HH:mm}.";
}
