using ELibrary.Domain.Common;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Entities;

public sealed class Member : Entity
{
    private Member()
    {
        MemberCode = string.Empty;
        FullName = string.Empty;
    }

    public Member(string memberCode, string fullName, string? email, string? phone,
        int membershipTypeId, DateOnly registeredOn, DateOnly cardExpiryDate) : this()
    {
        MemberCode = Guard.Code(memberCode, "Member code", 20);
        UpdateDetails(fullName, email, phone, membershipTypeId);
        if (cardExpiryDate < registeredOn)
        {
            throw new ValidationException("Card expiry", "The card cannot expire before registration.");
        }

        RegisteredOn = registeredOn;
        CardExpiryDate = cardExpiryDate;
        IsActive = true;
    }

    public string MemberCode { get; private set; }
    public string FullName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public int MembershipTypeId { get; private set; }
    public DateOnly RegisteredOn { get; private set; }
    public DateOnly CardExpiryDate { get; private set; }
    public bool IsActive { get; private set; }
    public int? UserAccountId { get; private set; }

    public bool IsCardValid(DateOnly today) => IsActive && CardExpiryDate >= today;

    public string? GetBorrowingBlock(DateOnly today, decimal outstandingFines, FinePolicy policy)
    {
        if (!IsActive)
        {
            return "The member is inactive.";
        }

        if (!IsCardValid(today))
        {
            return $"The library card expired on {CardExpiryDate:dd/MM/yyyy}.";
        }

        return policy.IsAboveDebtThreshold(outstandingFines)
            ? $"Outstanding fines ({Money.Format(outstandingFines)}) exceed the limit of {Money.Format(policy.DebtThreshold)}."
            : null;
    }

    public void EnsureCanBorrow(DateOnly today, decimal outstandingFines, FinePolicy policy)
    {
        var block = GetBorrowingBlock(today, outstandingFines, policy);
        if (block != null)
        {
            throw new BusinessRuleException("BR2", block);
        }
    }

    public void UpdateDetails(string fullName, string? email, string? phone, int membershipTypeId)
    {
        FullName = Guard.Required(fullName, "Full name", 100);
        Email = Guard.Email(email);
        Phone = Guard.Phone(phone);
        MembershipTypeId = Guard.InRange(membershipTypeId, 1, int.MaxValue, "Membership type");
    }

    public void ExtendCard(DateOnly newExpiry)
    {
        if (newExpiry <= CardExpiryDate)
        {
            throw new ValidationException("Card expiry", "The new expiry date must be after the current expiry date.");
        }

        CardExpiryDate = newExpiry;
    }

    public void LinkAccount(int accountId)
    {
        if (UserAccountId.HasValue)
        {
            throw new BusinessRuleException(BusinessRuleException.StateRule, "This member already has an account.");
        }

        UserAccountId = Guard.InRange(accountId, 1, int.MaxValue, "Account");
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
