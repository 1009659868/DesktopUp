using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ContentLegacyDisposition { DemoOrLegacyPreserved, UnknownLayoutReadOnly }
public sealed record ContentLegacyPoster(string PosterId, string? ContentLayoutJson);
public sealed record ContentLegacyAssessment(string PosterId, string? OriginalLayoutJson, ContentLegacyDisposition Disposition);
public sealed record ContentLegacyPreparation(IReadOnlyList<ContentLegacyAssessment> Posters, ContentCatalogSnapshot EmptyCatalog);

// Dry assessment only: no database connection, schema bump, automatic ID minting or layout conversion.
public static class ContentLegacyCompatibility
{
    public static ContentLegacyPreparation Inspect(int existingSqliteVersion, IReadOnlyList<ContentLegacyPoster> posters)
    {
        if (existingSqliteVersion is < 1 or > 12) throw new ArgumentException("Unknown SQLite source version remains read-only.");
        if (posters is null || posters.Count > ContentCatalogPolicy.MaximumEntries ||
            posters.Any(x => x is null || string.IsNullOrWhiteSpace(x.PosterId)) ||
            posters.Select(x => x.PosterId).Distinct(StringComparer.Ordinal).Count() != posters.Count)
            throw new ArgumentException("Invalid legacy poster catalog.");
        var result = posters.Select(p => new ContentLegacyAssessment(p.PosterId, p.ContentLayoutJson,
            p.ContentLayoutJson is null || ContentLayoutPolicy.TryRead(p.ContentLayoutJson, p.PosterId, out _, out _)
                ? ContentLegacyDisposition.DemoOrLegacyPreserved : ContentLegacyDisposition.UnknownLayoutReadOnly)).ToArray();
        return new(Array.AsReadOnly(result), ContentCatalogPolicy.Freeze(new([], [], [], [], [])));
    }
}
