using ELibrary.Domain.Enums;

namespace ELibrary.Business.Dtos;

public enum CatalogueSearchField
{
    All,
    Title,
    Author,
    Isbn,
    Category
}

public sealed record CatalogueQuery(
    string? Text,
    CatalogueSearchField Field = CatalogueSearchField.All,
    int? CategoryId = null,
    bool OnlyAvailable = false,
    bool IncludeInactive = false);

public sealed record TitleSummaryDto(
    int Id, string? Isbn, string Name, string Authors, string? Category, string? Publisher, int? PublicationYear,
    int TotalCopies, int AvailableCopies, int WaitingReservations, bool HasEResources, bool IsActive);

/// <summary><see cref="HeldForMemberCode"/> is only filled in for staff (privacy of other members).</summary>
public sealed record CopyDto(
    int Id, string Barcode, string Location, CopyStatus Status, DateOnly? DueDate, string? HeldForMemberCode, DateOnly? HoldExpiresOn);

public sealed record EResourceDto(int Id, string Label, string Url);

public sealed record TitleDetailsDto(
    int Id, string? Isbn, string Name, IReadOnlyList<string> Authors, string? Publisher, string? Category,
    int? PublicationYear, string? Description, bool IsActive,
    IReadOnlyList<CopyDto> Copies, IReadOnlyList<EResourceDto> EResources,
    int WaitingReservations, ReservationStatus? MyReservationStatus);

/// <summary>Data entered in the title editor. <see cref="Id"/> is null for a new title.</summary>
public sealed record TitleInput(
    int? Id, string? Isbn, string Name, IReadOnlyList<string> Authors,
    string? Publisher, string? Category, int? PublicationYear, string? Description);
