using ELibrary.Domain.Common;

namespace ELibrary.Domain.Entities;

/// <summary>An electronic resource is kept simple: a labelled link attached to a title (no file storage, no DRM).</summary>
public class EResource : Entity
{
    private EResource()
    {
        Label = string.Empty;
        Url = string.Empty;
    }

    public EResource(int titleId, string label, string url)
    {
        TitleId = titleId;
        Label = string.Empty;
        Url = string.Empty;
        Update(label, url);
    }

    public int TitleId { get; private set; }

    public string Label { get; private set; }

    public string Url { get; private set; }

    public void Update(string label, string url)
    {
        Label = Guard.Required(label, "Label", 150);
        Url = Guard.Url(url);
    }
}
