using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Internal;
using ELibrary.Business.Security;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>UC06: manage members, cards and linked member accounts.</summary>
public sealed class MemberService(IUnitOfWorkFactory factory, IPasswordHasher hasher, IClock clock, IUserSession session)
{
    public IReadOnlyList<MemberDto> Search(string? keyword = null)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        var query = uow.Members.Query();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToUpperInvariant();
            query = query.Where(m => m.MemberCode.ToUpper().Contains(term) || m.FullName.ToUpper().Contains(term)
                || (m.Email != null && m.Email.ToUpper().Contains(term)) || (m.Phone != null && m.Phone.Contains(term)));
        }

        var types = uow.MembershipTypes.Query().ToDictionary(t => t.Id);
        return query.OrderBy(m => m.MemberCode).AsEnumerable()
            .Select(m => DtoProjector.ToMemberDto(m, types[m.MembershipTypeId])).ToList();
    }

    public IReadOnlyList<MembershipTypeDto> GetMembershipTypes()
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        return uow.MembershipTypes.Query().OrderBy(t => t.Name)
            .Select(t => new MembershipTypeDto(t.Id, t.Name, t.MaxLoans, t.LoanPeriodDays, t.MaxRenewals, t.IsActive))
            .ToList();
    }

    public MemberDto GetMember(int memberId)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        return Describe(uow, uow.Members.GetRequired(memberId, "Member"));
    }

    public MemberDto GetMyProfile()
    {
        var memberId = session.Require(RoleCode.Member).RequireMemberId();
        using var uow = factory.Create();
        return Describe(uow, uow.Members.GetRequired(memberId, "Member"));
    }

    public MemberDto Register(MemberInput input, string password)
    {
        session.Require(RoleCode.Librarian);
        PasswordPolicy.Validate(password);
        var hash = hasher.Hash(password);
        var now = clock.Now;
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var type = RequireMembershipType(uow, input.MembershipTypeId);
            var code = NextCode(uow);
            PasswordPolicy.Validate(password, code);
            var member = new Member(code, input.FullName, input.Email, input.Phone,
                type.Id, DateOnly.FromDateTime(now), input.CardExpiryDate);
            var account = new UserAccount(code, hash, member.FullName, RoleCode.Member, now);
            uow.UserAccounts.Add(account);
            uow.SaveChanges();
            member.LinkAccount(account.Id);
            uow.Members.Add(member);
            uow.SaveChanges();
            return DtoProjector.ToMemberDto(member, type);
        });
    }

    public MemberDto Update(int memberId, MemberInput input)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        return uow.InTransaction(() =>
        {
            var member = uow.Members.GetRequired(memberId, "Member");
            var type = RequireMembershipType(uow, input.MembershipTypeId, member.MembershipTypeId);
            member.UpdateDetails(input.FullName, input.Email, input.Phone, type.Id);
            if (input.CardExpiryDate != member.CardExpiryDate)
            {
                Extend(member, input.CardExpiryDate);
            }

            LinkedAccount(uow, member)?.ChangeFullName(member.FullName);
            uow.SaveChanges();
            return DtoProjector.ToMemberDto(member, type);
        });
    }

    public void ExtendCard(int memberId, DateOnly newExpiry)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        uow.ExecuteInTransaction(() =>
        {
            Extend(uow.Members.GetRequired(memberId, "Member"), newExpiry);
            uow.SaveChanges();
        });
    }

    public void SetActive(int memberId, bool active)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        uow.ExecuteInTransaction(() =>
        {
            var member = uow.Members.GetRequired(memberId, "Member");
            if (member.IsActive == active)
            {
                return;
            }

            var account = LinkedAccount(uow, member);
            if (active)
            {
                member.Reactivate();
                account?.Unlock();
            }
            else
            {
                member.Deactivate();
                account?.Lock();
            }

            uow.SaveChanges();
        });
    }

    public void ResetPassword(int memberId, string newPassword)
    {
        session.Require(RoleCode.Librarian);
        using var uow = factory.Create();
        var member = uow.Members.GetRequired(memberId, "Member");
        var account = LinkedAccount(uow, member)
            ?? throw new BusinessRuleException(BusinessRuleException.StateRule, "This member has no linked login account.");
        PasswordPolicy.Validate(newPassword, account.Username);
        account.ChangePasswordHash(hasher.Hash(newPassword));
        uow.SaveChanges();
    }

    private static string NextCode(IUnitOfWork uow)
    {
        long number = (uow.Members.Query().Max(m => (int?)m.Id) ?? 0) + 1L;
        while (true)
        {
            var code = FormattableString.Invariant($"M{number:D4}");
            if (!uow.Members.Query().Any(m => m.MemberCode == code) && !uow.UserAccounts.Query().Any(a => a.Username == code))
            {
                return code;
            }

            number++;
        }
    }

    private void Extend(Member member, DateOnly expiry)
    {
        if (expiry < clock.Today)
        {
            throw new ValidationException("Card expiry", "Enter today's date or a later expiry date.");
        }

        member.ExtendCard(expiry);
    }

    private static UserAccount? LinkedAccount(IUnitOfWork uow, Member member) =>
        member.UserAccountId is int id ? uow.UserAccounts.GetRequired(id, "Account") : null;

    private static MemberDto Describe(IUnitOfWork uow, Member member) =>
        DtoProjector.ToMemberDto(member, uow.MembershipTypes.GetRequired(member.MembershipTypeId, "Membership type"));

    private static MembershipType RequireMembershipType(IUnitOfWork uow, int id, int? currentId = null)
    {
        var type = uow.MembershipTypes.GetRequired(id, "Membership type");
        if (!type.IsActive && type.Id != currentId)
        {
            throw new ValidationException("Membership type", "Select an active membership type.");
        }

        return type;
    }
}
