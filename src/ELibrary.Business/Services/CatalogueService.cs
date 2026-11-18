using ELibrary.Business.Abstractions;
using ELibrary.Business.Dtos;
using ELibrary.Business.Internal;
using ELibrary.Domain.Common;
using ELibrary.Domain.Entities;
using ELibrary.Domain.Enums;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Services;

/// <summary>UC04 Manage titles and copies (FR-04) and UC05 Search catalogue / open e-resource link (FR-05, FR-06).</summary>
public sealed class CatalogueService
{
    public const int MaxResults = 500;

    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IUserSession _session;
    private readonly IClock _clock;

    public CatalogueService(IUnitOfWorkFactory uowFactory, IUserSession session, IClock clock)
    {
        _uowFactory = uowFactory;
        _session = session;
        _clock = clock;
    }

    // ---- UC05 Search -------------------------------------------------------------------

    public IReadOnlyList<TitleSummaryDto> Search(CatalogueQuery query)
    {
        var user = _session.RequireAuthenticated();
        var includeInactive = query.IncludeInactive && user.Role != RoleCode.Member;

        using var uow = _uowFactory.Create();
        var titles = uow.Titles.Query();
        if (!includeInactive)
        {
            titles = titles.Where(t => t.IsActive);
        }

        if (query.CategoryId.HasValue)
        {
            var categoryId = query.CategoryId.Value;
            titles = titles.Where(t => t.CategoryId == categoryId);
        }

        var text = query.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            titles = ApplyTextFilter(uow, titles, text, query.Field);
        }

        var found = titles.OrderBy(t => t.Name).Take(MaxResults).ToList();
        var summaries = BuildSummaries(uow, found);
        return query.OnlyAvailable ? summaries.Where(s => s.AvailableCopies > 0).ToList() : summaries;
    }

    private static IQueryable<Title> ApplyTextFilter(IUnitOfWork uow, IQueryable<Title> titles, string text, CatalogueSearchField field)
    {
        var isbnText = text.Replace("-", string.Empty).Replace(" ", string.Empty);
        switch (field)
        {
            case CatalogueSearchField.Title:
                return titles.Where(t => t.Name.Contains(text));

            case CatalogueSearchField.Isbn:
                return titles.Where(t => t.Isbn != null && t.Isbn.Contains(isbnText));

            case CatalogueSearchField.Author:
            {
                var titleIds = FindTitleIdsByAuthor(uow, text);
                return titles.Where(t => titleIds.Contains(t.Id));
            }

            case CatalogueSearchField.Category:
            {
                var categoryIds = FindCategoryIds(uow, text);
                return titles.Where(t => t.CategoryId != null && categoryIds.Contains(t.CategoryId.Value));
            }

            default:
            {
                var titleIds = FindTitleIdsByAuthor(uow, text);
                var categoryIds = FindCategoryIds(uow, text);
                return titles.Where(t =>
                    t.Name.Contains(text) ||
                    (t.Isbn != null && t.Isbn.Contains(isbnText)) ||
                    (t.Description != null && t.Description.Contains(text)) ||
                    titleIds.Contains(t.Id) ||
                    (t.CategoryId != null && categoryIds.Contains(t.CategoryId.Value)));
            }
        }
    }

    private static List<int> FindTitleIdsByAuthor(IUnitOfWork uow, string text)
    {
        var authorIds = uow.Authors.Query().Where(a => a.Name.Contains(text)).Select(a => a.Id).ToList();
        return uow.TitleAuthors.Query().Where(ta => authorIds.Contains(ta.AuthorId)).Select(ta => ta.TitleId).Distinct().ToList();
    }

    private static List<int> FindCategoryIds(IUnitOfWork uow, string text) =>
        uow.Categories.Query().Where(c => c.Name.Contains(text)).Select(c => c.Id).ToList();

    private static IReadOnlyList<TitleSummaryDto> BuildSummaries(IUnitOfWork uow, IReadOnlyList<Title> titles)
    {
        if (titles.Count == 0)
        {
            return Array.Empty<TitleSummaryDto>();
        }

        var ids = titles.Select(t => t.Id).ToList();
        var copies = uow.Copies.Query()
            .Where(c => ids.Contains(c.TitleId) && c.Status != CopyStatus.Withdrawn)
            .Select(c => new { c.TitleId, c.Status })
            .ToList()
            .ToLookup(c => c.TitleId);
        var waiting = uow.Reservations.Query()
            .Where(r => ids.Contains(r.TitleId) && r.Status == ReservationStatus.Waiting)
            .Select(r => r.TitleId)
            .ToList()
            .ToLookup(id => id);
        var withLinks = uow.EResources.Query().Where(e => ids.Contains(e.TitleId)).Select(e => e.TitleId).Distinct().ToList().ToHashSet();
        var authors = LoadAuthorNames(uow, ids);
        var categories = LookupNames(uow.Categories.Query().Select(c => new LookupDto(c.Id, c.Name)));
        var publishers = LookupNames(uow.Publishers.Query().Select(p => new LookupDto(p.Id, p.Name)));

        return titles.Select(t => new TitleSummaryDto(
            t.Id, t.Isbn, t.Name,
            string.Join(", ", authors.GetValueOrDefault(t.Id) ?? new List<string>()),
            t.CategoryId.HasValue ? categories.GetValueOrDefault(t.CategoryId.Value) : null,
            t.PublisherId.HasValue ? publishers.GetValueOrDefault(t.PublisherId.Value) : null,
            t.PublicationYear,
            copies[t.Id].Count(),
            copies[t.Id].Count(c => c.Status == CopyStatus.Available),
            waiting[t.Id].Count(),
            withLinks.Contains(t.Id),
            t.IsActive)).ToList();
    }

    private static Dictionary<int, List<string>> LoadAuthorNames(IUnitOfWork uow, List<int> titleIds)
    {
        var links = uow.TitleAuthors.Query().Where(ta => titleIds.Contains(ta.TitleId)).ToList();
        var authorIds = links.Select(l => l.AuthorId).Distinct().ToList();
        var names = uow.Authors.Query().Where(a => authorIds.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Name);
        return links
            .GroupBy(l => l.TitleId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Position).Select(l => names[l.AuthorId]).ToList());
    }

    private static Dictionary<int, string> LookupNames(IQueryable<LookupDto> query) =>
        query.ToList().ToDictionary(l => l.Id, l => l.Name);

    public TitleDetailsDto GetTitleDetails(int titleId)
    {
        var user = _session.RequireAuthenticated();
        var isMember = user.Role == RoleCode.Member;

        using var uow = _uowFactory.Create();
        var title = uow.Titles.GetById(titleId);
        if (title == null || (isMember && !title.IsActive))
        {
            throw new NotFoundException("Title", titleId);
        }

        var copies = uow.Copies.Query().Where(c => c.TitleId == titleId).OrderBy(c => c.Barcode).ToList();
        if (isMember)
        {
            copies = copies.Where(c => c.Status != CopyStatus.Withdrawn).ToList();
        }

        var copyIds = copies.Select(c => c.Id).ToList();
        var dueDates = uow.Loans.Query()
            .Where(l => copyIds.Contains(l.CopyId) && l.ReturnedAt == null)
            .ToList()
            .ToDictionary(l => l.CopyId, l => l.DueDate);
        var holds = uow.Reservations.Query()
            .Where(r => r.TitleId == titleId && r.Status == ReservationStatus.OnHold)
            .ToList();
        var holderIds = holds.Select(h => h.MemberId).ToList();
        var holderCodes = uow.Members.Query().Where(m => holderIds.Contains(m.Id)).ToDictionary(m => m.Id, m => m.MemberCode);

        var copyDtos = copies.Select(c =>
        {
            var hold = holds.FirstOrDefault(h => h.HeldCopyId == c.Id);
            return new CopyDto(
                c.Id, c.Barcode, c.Location, c.Status,
                dueDates.TryGetValue(c.Id, out var due) ? due : null,
                hold != null && !isMember ? holderCodes[hold.MemberId] : null,
                hold?.HoldExpiresOn);
        }).ToList();

        var links = uow.EResources.Query().Where(e => e.TitleId == titleId).OrderBy(e => e.Label).ToList()
            .Select(e => new EResourceDto(e.Id, e.Label, e.Url)).ToList();
        var waitingCount = uow.Reservations.Query().Count(r => r.TitleId == titleId && r.Status == ReservationStatus.Waiting);

        ReservationStatus? myStatus = null;
        if (user.MemberId.HasValue)
        {
            var memberId = user.MemberId.Value;
            myStatus = uow.Reservations.Query()
                .Where(r => r.TitleId == titleId && r.MemberId == memberId &&
                            (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.OnHold))
                .Select(r => (ReservationStatus?)r.Status)
                .FirstOrDefault();
        }

        var authors = LoadAuthorNames(uow, new List<int> { titleId }).GetValueOrDefault(titleId) ?? new List<string>();
        var publisher = title.PublisherId.HasValue ? uow.Publishers.GetById(title.PublisherId.Value)?.Name : null;
        var category = title.CategoryId.HasValue ? uow.Categories.GetById(title.CategoryId.Value)?.Name : null;

        return new TitleDetailsDto(
            title.Id, title.Isbn, title.Name, authors, publisher, category, title.PublicationYear, title.Description,
            title.IsActive, copyDtos, links, waitingCount, myStatus);
    }

    /// <summary>FR-06: returns the address of an e-resource link for a logged-in user to open.</summary>
    public string GetEResourceUrl(int eResourceId)
    {
        var user = _session.RequireAuthenticated();
        using var uow = _uowFactory.Create();
        var link = uow.EResources.GetRequired(eResourceId, "E-resource");
        var title = uow.Titles.GetRequired(link.TitleId, "Title");
        if (user.Role == RoleCode.Member && !title.IsActive)
        {
            throw new NotFoundException("E-resource", eResourceId);
        }

        return link.Url;
    }

    public IReadOnlyList<LookupDto> GetCategories()
    {
        _session.RequireAuthenticated();
        using var uow = _uowFactory.Create();
        return uow.Categories.Query().OrderBy(c => c.Name).Select(c => new LookupDto(c.Id, c.Name)).ToList();
    }

    public IReadOnlyList<LookupDto> GetPublishers()
    {
        _session.RequireAuthenticated();
        using var uow = _uowFactory.Create();
        return uow.Publishers.Query().OrderBy(p => p.Name).Select(p => new LookupDto(p.Id, p.Name)).ToList();
    }

    // ---- UC04 Manage titles and copies ------------------------------------------------

    /// <summary>Creates or updates a title. Unknown authors, publishers and categories are created.</summary>
    public int SaveTitle(TitleInput input)
    {
        _session.Require(RoleCode.Librarian);
        var authorNames = (input.Authors ?? Array.Empty<string>())
            .Select(a => a?.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(a => a!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (authorNames.Count == 0)
        {
            throw new ValidationException("Authors", "At least one author is required.");
        }

        var isbn = Guard.Isbn(input.Isbn);
        using var uow = _uowFactory.Create();
        var titleId = 0;
        uow.ExecuteInTransaction(() =>
        {
            var existingId = input.Id ?? 0;
            if (isbn != null && uow.Titles.Query().Any(t => t.Isbn == isbn && t.Id != existingId))
            {
                throw new ValidationException("ISBN", $"Another title already has ISBN {isbn}.");
            }

            var publisherId = FindOrCreate(uow, uow.Publishers, input.Publisher, name => new Publisher(name), "Publisher");
            var categoryId = FindOrCreate(uow, uow.Categories, input.Category, name => new Category(name), "Category");

            Title title;
            if (input.Id.HasValue)
            {
                title = uow.Titles.GetRequired(input.Id.Value, "Title");
                title.Update(isbn, input.Name, publisherId, categoryId, input.PublicationYear, input.Description);
            }
            else
            {
                title = new Title(isbn, input.Name, publisherId, categoryId, input.PublicationYear, input.Description);
                uow.Titles.Add(title);
            }

            uow.SaveChanges();

            foreach (var oldLink in uow.TitleAuthors.Query().Where(ta => ta.TitleId == title.Id).ToList())
            {
                uow.TitleAuthors.Remove(oldLink);
            }

            uow.SaveChanges();

            var position = 1;
            foreach (var authorName in authorNames)
            {
                var authorId = FindOrCreate(uow, uow.Authors, authorName, name => new Author(name), "Author")!.Value;
                uow.TitleAuthors.Add(new TitleAuthor(title.Id, authorId, position++));
            }

            uow.SaveChanges();
            titleId = title.Id;
        });

        return titleId;
    }

    /// <summary>Finds a lookup row by name (case-insensitive in SQL Server) or creates it. Returns null for an empty name.</summary>
    private static int? FindOrCreate<T>(IUnitOfWork uow, IRepository<T> repository, string? name, Func<string, T> create, string field)
        where T : Entity
    {
        var trimmed = Guard.Optional(name, field, 150);
        if (trimmed == null)
        {
            return null;
        }

        var existing = repository.Query().ToList()
            .FirstOrDefault(e => string.Equals(NameOf(e), trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing.Id;
        }

        var created = create(trimmed);
        repository.Add(created);
        uow.SaveChanges();
        return created.Id;
    }

    private static string NameOf(Entity entity) => entity switch
    {
        Author a => a.Name,
        Publisher p => p.Name,
        Category c => c.Name,
        _ => string.Empty
    };

    /// <summary>
    /// Deactivating hides a title from members. It is refused while copies are on loan or on hold or
    /// members are waiting, so that no circulation is left half-way.
    /// </summary>
    public void SetTitleActive(int titleId, bool active)
    {
        _session.Require(RoleCode.Librarian);
        using var uow = _uowFactory.Create();
        var title = uow.Titles.GetRequired(titleId, "Title");
        if (active)
        {
            title.Reactivate();
        }
        else
        {
            var busy = uow.Copies.Query().Any(c => c.TitleId == titleId &&
                                                   (c.Status == CopyStatus.OnLoan || c.Status == CopyStatus.OnHold));
            var waiting = uow.Reservations.Query().Any(r => r.TitleId == titleId && r.Status == ReservationStatus.Waiting);
            if (busy || waiting)
            {
                throw new BusinessRuleException(BusinessRuleException.StateRule,
                    "This title still has copies on loan or on hold, or members waiting for it.");
            }

            title.Deactivate();
        }

        uow.SaveChanges();
    }

    public int AddCopy(int titleId, string? barcode, string? location)
    {
        _session.Require(RoleCode.Librarian);
        using var uow = _uowFactory.Create();
        var title = uow.Titles.GetRequired(titleId, "Title");
        var code = Guard.Code(barcode, "Barcode", 30);
        if (uow.Copies.Query().Any(c => c.Barcode == code))
        {
            throw new ValidationException("Barcode", $"Barcode {code} is already used by another copy.");
        }

        var copy = new Copy(title.Id, code, location ?? string.Empty);
        uow.Copies.Add(copy);
        uow.SaveChanges();
        return copy.Id;
    }

    public void ChangeCopyLocation(int copyId, string? location)
    {
        _session.Require(RoleCode.Librarian);
        using var uow = _uowFactory.Create();
        uow.Copies.GetRequired(copyId, "Copy").ChangeLocation(location ?? string.Empty);
        uow.SaveChanges();
    }

    /// <summary>Repair / lost / withdrawn / available. A copy that comes back to Available is offered to the queue.</summary>
    public void ChangeCopyStatus(int copyId, CopyStatus newStatus)
    {
        _session.Require(RoleCode.Librarian);
        using var uow = _uowFactory.Create();
        var copy = uow.Copies.GetRequired(copyId, "Copy");
        copy.ChangeStatus(newStatus);
        if (newStatus == CopyStatus.Available)
        {
            var rules = LibraryRules.Load(uow);
            new HoldAllocator(uow, rules).AssignCopy(copy, _clock.Today);
        }

        uow.SaveChanges();
    }

    public int AddEResource(int titleId, string? label, string? url)
    {
        _session.Require(RoleCode.Librarian);
        using var uow = _uowFactory.Create();
        var title = uow.Titles.GetRequired(titleId, "Title");
        var link = new EResource(title.Id, label ?? string.Empty, url ?? string.Empty);
        uow.EResources.Add(link);
        uow.SaveChanges();
        return link.Id;
    }

    public void RemoveEResource(int eResourceId)
    {
        _session.Require(RoleCode.Librarian);
        using var uow = _uowFactory.Create();
        uow.EResources.Remove(uow.EResources.GetRequired(eResourceId, "E-resource"));
        uow.SaveChanges();
    }
}
