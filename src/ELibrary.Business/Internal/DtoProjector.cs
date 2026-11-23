using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Domain.Entities;

namespace ELibrary.Business.Internal;

internal sealed class DtoProjector(IUnitOfWork uow, LibraryRules rules, DateOnly today)
{
    public static MemberDto ToMemberDto(Member member, MembershipType type) =>
        new(member.Id, member.MemberCode, member.FullName, member.Email, member.Phone,
            member.MembershipTypeId, type.Name, member.RegisteredOn, member.CardExpiryDate,
            member.IsActive, member.UserAccountId.HasValue);

    public IReadOnlyList<LoanDto> ToLoanDtos(IReadOnlyList<Loan> loans)
    {
        if (loans.Count == 0)
        {
            return Array.Empty<LoanDto>();
        }

        var memberIds = loans.Select(l => l.MemberId).Distinct().ToArray();
        var members = uow.Members.Query().Where(m => memberIds.Contains(m.Id)).ToDictionary(m => m.Id);
        var copyIds = loans.Select(l => l.CopyId).Distinct().ToArray();
        var copies = uow.Copies.Query().Where(c => copyIds.Contains(c.Id)).ToDictionary(c => c.Id);
        var titleIds = copies.Values.Select(c => c.TitleId).Distinct().ToArray();
        var titles = uow.Titles.Query().Where(t => titleIds.Contains(t.Id)).ToDictionary(t => t.Id);
        return loans.Select(loan =>
        {
            var member = members[loan.MemberId];
            var copy = copies[loan.CopyId];
            var end = loan.ReturnedAt.HasValue ? DateOnly.FromDateTime(loan.ReturnedAt.Value) : today;
            var days = rules.Calendar.CountChargeableDays(loan.DueDate, end);
            return new LoanDto(loan.Id, member.Id, member.MemberCode, member.FullName, titles[copy.TitleId].Name,
                copy.Barcode, loan.LoanDate, loan.DueDate, loan.ReturnedAt, loan.RenewalCount,
                loan.IsOverdue(today), days, rules.Policy.CalculateFine(days));
        }).ToList();
    }
}
