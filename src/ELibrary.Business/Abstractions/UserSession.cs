using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Abstractions;

/// <summary>The logged-in user. <see cref="MemberId"/> is set only for the Member role.</summary>
public sealed record CurrentUser(int AccountId, string Username, string FullName, RoleCode Role, int? MemberId);

/// <summary>Holds the logged-in user of this desktop application.</summary>
public interface IUserSession
{
    CurrentUser? User { get; }

    void SignIn(CurrentUser user);

    void SignOut();
}

public sealed class UserSession : IUserSession
{
    public CurrentUser? User { get; private set; }

    public void SignIn(CurrentUser user) => User = user;

    public void SignOut() => User = null;
}

/// <summary>
/// Role checks used at the start of every service method (NFR-04). Because the check is in the
/// service and not in the menu, opening a form directly does not bypass it.
/// </summary>
public static class SessionExtensions
{
    public static CurrentUser RequireAuthenticated(this IUserSession session) =>
        session.User ?? throw new AccessDeniedException("Please log in first.");

    public static CurrentUser Require(this IUserSession session, params RoleCode[] allowedRoles)
    {
        var user = session.RequireAuthenticated();
        if (!allowedRoles.Contains(user.Role))
        {
            throw new AccessDeniedException($"This action is not allowed for the {user.Role} role.");
        }

        return user;
    }

    public static int RequireMemberId(this CurrentUser user) =>
        user.MemberId ?? throw new AccessDeniedException("This action is only available to library members.");

    /// <summary>A member may only see or change his/her own data; staff may act for any member.</summary>
    public static void EnsureCanActForMember(this CurrentUser user, int memberId)
    {
        if (user.Role == RoleCode.Member && user.MemberId != memberId)
        {
            throw new AccessDeniedException("Members can only access their own records.");
        }
    }
}
