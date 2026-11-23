namespace ELibrary.Business.Dtos;

public sealed record MemberInput(string FullName, string? Email, string? Phone, int MembershipTypeId, DateOnly CardExpiryDate);

public sealed record MemberDto(
    int Id, string MemberCode, string FullName, string? Email, string? Phone,
    int MembershipTypeId, string MembershipTypeName, DateOnly RegisteredOn, DateOnly CardExpiryDate,
    bool IsActive, bool HasAccount);
