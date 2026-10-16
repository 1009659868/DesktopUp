using DesktopUp.Domain;

namespace DesktopUp.Application;

// Trusted host source whose Read returns one locked, coherent metadata snapshot. Its global
// observation version may advance concurrently without invalidating unrelated Item identities.
public interface IAtomicContentItemViewSource : IContentItemViewSource { }

public interface IContentOpenHandler
{
    // Trusted host adapter only. Neither SourceKey nor this intent is an executable instruction.
    void Open(ContentOpenIntent intent);
}

public sealed class DemoContentOpenHandler : IContentOpenHandler
{
    public static DemoContentOpenHandler Instance { get; } = new();
    public void Open(ContentOpenIntent intent) { } // Deliberately no Shell or file operations.
}

public sealed record ContentSourcePorts(IContentItemViewSource Views, IContentAssetProvider Assets, IContentOpenHandler Open)
{
    public string? UnavailableOpenReason { get; init; }
    public static ContentSourcePorts Demo { get; } = new(new DemoContentItemViewSource(), NullContentAssetProvider.Instance, DemoContentOpenHandler.Instance);
}

/// <summary>Source metadata cannot add/remove placements or impose its enumeration order.
/// Missing/invalid entries render an unavailable fallback; membership still requires a CAS command.</summary>
public static class ContentSourceProjection
{
    public static IReadOnlyList<ContentItemView> Read(IContentItemViewSource source, ContentLayoutDocument document, long? expectedVersion = null)
    {
        IReadOnlyList<ContentItemView> supplied;
        try
        {
            long version = source.Version;
            bool atomic = source is IAtomicContentItemViewSource;
            supplied = version >= 1 && (atomic || !expectedVersion.HasValue || expectedVersion == version) ? source.Read(document) : [];
            if (!atomic && source.Version != version) supplied = [];
        }
        catch (Exception) { supplied = []; }
        var accepted = new Dictionary<string, ContentItemView>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var members = document.Placements.Select(p => p.ItemId).ToHashSet(StringComparer.Ordinal);
        if (supplied is not null && supplied.Count <= 1000)
            foreach (var item in supplied)
            {
                if (item is null || !members.Contains(item.ItemId) || string.IsNullOrWhiteSpace(item.Name) ||
                    item.Name.Length > 256 || item.Name.Any(char.IsControl) || string.IsNullOrWhiteSpace(item.Monogram) ||
                    item.Monogram.Length > 16 || item.Monogram.Any(char.IsControl)) continue;
                if (!accepted.TryAdd(item.ItemId, item)) duplicates.Add(item.ItemId);
            }
        return document.Placements.Select(p => !duplicates.Contains(p.ItemId) && accepted.TryGetValue(p.ItemId, out var item)
            ? item : new ContentItemView(p.ItemId, "内容暂不可用", "?", 0xFF66737D) { IsAvailable = false }).ToArray();
    }
}
