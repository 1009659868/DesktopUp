using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace DesktopUp.Domain;

// V3-05 entities. No provider, filesystem, UI object or persistent store lives in Domain.
public enum ContentKind { File, Folder, Shortcut, Application, ShellItem, VirtualStream, Unknown }
public enum ContentTargetKind { FileSystem, Shortcut, Application, Shell, Protocol, Virtual, Unknown }
public enum ContentAvailability { Available, Missing, Unavailable, AccessDenied, ParseFailed, RestrictedProtocol, Unsupported }
public enum ContentCollectionKind { Reference, Mapped, Managed }
public enum ContentStorageOwnership { None, Borrowed, Managed }
public enum ContentMembershipOrigin { User, Source }

[Flags]
public enum ContentCapability
{
    None = 0, Reference = 1, Open = 2, Reveal = 4, Copy = 8, Move = 16,
    Link = 32, Rename = 64, Recycle = 128, Enumerate = 256, Materialize = 512
}

// Provider-defined opaque identity: no path normalization, disk lookup, or case folding.
// A locator is mutable metadata; changing it does not allocate a new ContentItem ID.
public sealed record ContentSourceIdentity(string ProviderId, string Key);
public sealed record ContentOpenTarget([property: JsonRequired] ContentTargetKind Kind, string Locator,
    [property: JsonRequired] string Arguments = "", [property: JsonRequired] string WorkingDirectory = "");

public sealed record ContentItem(Guid Id, [property: JsonRequired] ContentKind Kind, ContentSourceIdentity Source,
    ContentOpenTarget Target, string DisplayName, [property: JsonRequired] ContentCapability Capabilities,
    [property: JsonRequired] ContentAvailability Availability = ContentAvailability.Available, [property: JsonRequired] long Version = 1);

public sealed record Collection(Guid Id, string Name, [property: JsonRequired] ContentCollectionKind Kind,
    string? StorageLocator = null, [property: JsonRequired] ContentStorageOwnership Ownership = ContentStorageOwnership.None,
    [property: JsonRequired] long Version = 1);
public sealed record CollectionMembership(Guid Id, Guid CollectionId, Guid ContentItemId,
    [property: JsonRequired] ContentMembershipOrigin Origin = ContentMembershipOrigin.User, [property: JsonRequired] long Version = 1);
public sealed record ContentPosterBinding(string PosterId, Guid CollectionId, [property: JsonRequired] long Version = 1);

// Arrangement remains solely in ContentLayoutDocument, keyed by LayoutItemId.
// Membership resolves to the shared content entity; this is not another copy of its source.
public sealed record PosterItem(Guid Id, string PosterId, Guid MembershipId, [property: JsonRequired] long Version = 1)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IconAssetId { get; init; }
    [JsonIgnore]
    public string LayoutItemId => $"{PosterId}:content-{Id:N}";
}

public sealed record ContentCatalogSnapshot(IReadOnlyList<ContentItem> Items,
    IReadOnlyList<Collection> Collections, IReadOnlyList<CollectionMembership> Memberships,
    IReadOnlyList<ContentPosterBinding> Bindings, IReadOnlyList<PosterItem> PosterItems)
{
    public static ContentCatalogSnapshot Empty { get; } = new([], [], [], [], []);
    // Independent contract fixture format. NOT a SQLite/portable/layout schema upgrade.
    public int FormatVersion { get; init; } = 1;
}

[Flags]
public enum ContentDuplicateHint { None = 0, SameSource = 1, SameOpenSemantics = 2 }
public static class ContentIdentityPolicy
{
    // Hints require explicit reuse/independent/skip/repair choice; never auto-merge.
    public static ContentDuplicateHint Compare(ContentItem left, ContentItem right)
    {
        ContentCatalogPolicy.ValidateItem(left);
        ContentCatalogPolicy.ValidateItem(right);
        var hint = left.Source == right.Source ? ContentDuplicateHint.SameSource : ContentDuplicateHint.None;
        if (left.Kind == right.Kind && left.Source.ProviderId == right.Source.ProviderId && left.Target == right.Target)
            hint |= ContentDuplicateHint.SameOpenSemantics;
        return hint;
    }
}

public static class ContentCatalogPolicy
{
    public const int MaximumEntries = 10000;
    public const ContentCapability AllCapabilities = ContentCapability.Reference | ContentCapability.Open |
        ContentCapability.Reveal | ContentCapability.Copy | ContentCapability.Move | ContentCapability.Link |
        ContentCapability.Rename | ContentCapability.Recycle | ContentCapability.Enumerate | ContentCapability.Materialize;

    public static void ValidateItem(ContentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Require(item.Id != Guid.Empty && item.Version > 0 && Enum.IsDefined(item.Kind) &&
            Enum.IsDefined(item.Availability), "Invalid content identity/version/state.");
        Require(item.Source is not null && Text(item.Source.ProviderId, 128) && Text(item.Source.Key, 8192),
            "A provider-scoped stable identity is required.");
        Require(item.Target is not null && Enum.IsDefined(item.Target.Kind) && Text(item.Target.Locator, 32768) &&
            Text(item.Target.Arguments, 32768, true) && Text(item.Target.WorkingDirectory, 32768, true),
            "Invalid target semantics.");
        Require(Text(item.DisplayName, 1024) && (item.Capabilities & ~AllCapabilities) == 0, "Invalid content metadata/capabilities.");
        bool compatible = item.Kind switch
        {
            ContentKind.File or ContentKind.Folder => item.Target.Kind == ContentTargetKind.FileSystem,
            ContentKind.Shortcut => item.Target.Kind == ContentTargetKind.Shortcut,
            ContentKind.Application => item.Target.Kind is ContentTargetKind.Application or ContentTargetKind.FileSystem or ContentTargetKind.Protocol,
            ContentKind.ShellItem => item.Target.Kind == ContentTargetKind.Shell,
            ContentKind.VirtualStream => item.Target.Kind == ContentTargetKind.Virtual,
            ContentKind.Unknown => item.Target.Kind == ContentTargetKind.Unknown,
            _ => false
        };
        Require(compatible, "Content type and opening semantics disagree.");
    }

    public static ContentCatalogSnapshot Freeze(ContentCatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Require(catalog.FormatVersion == 1, "Unknown content contract format.");
        var items = Copy(catalog.Items);
        var collections = Copy(catalog.Collections);
        var members = Copy(catalog.Memberships);
        var bindings = Copy(catalog.Bindings);
        var posters = Copy(catalog.PosterItems);
        foreach (var item in items) ValidateItem(item);
        foreach (var collection in collections)
        {
            Require(collection.Id != Guid.Empty && collection.Version > 0 && Text(collection.Name, 1024) &&
                Enum.IsDefined(collection.Kind) && Enum.IsDefined(collection.Ownership), "Invalid collection.");
            Require(collection.Kind switch
            {
                ContentCollectionKind.Reference => collection.Ownership == ContentStorageOwnership.None && collection.StorageLocator is null,
                ContentCollectionKind.Mapped => collection.Ownership == ContentStorageOwnership.Borrowed && Text(collection.StorageLocator, 32768),
                ContentCollectionKind.Managed => collection.Ownership == ContentStorageOwnership.Managed && Text(collection.StorageLocator, 32768),
                _ => false
            }, "Collection storage ownership is inconsistent.");
        }
        Unique(items.Select(x => x.Id));
        Unique(collections.Select(x => x.Id));
        Unique(members.Select(x => x.Id));
        Unique(posters.Select(x => x.Id));
        Unique(bindings.Select(x => x.PosterId));
        Unique(members.Select(x => (x.CollectionId, x.ContentItemId)));
        Unique(posters.Select(x => (x.PosterId, x.MembershipId)));
        var itemIds = items.Select(x => x.Id).ToHashSet();
        var collectionMap = collections.ToDictionary(x => x.Id);
        foreach (var member in members)
        {
            Require(member.Id != Guid.Empty && member.Version > 0 && itemIds.Contains(member.ContentItemId) &&
                collectionMap.ContainsKey(member.CollectionId) && Enum.IsDefined(member.Origin), "Dangling/invalid membership.");
            Require(collectionMap[member.CollectionId].Kind == ContentCollectionKind.Reference
                ? member.Origin == ContentMembershipOrigin.User : member.Origin == ContentMembershipOrigin.Source,
                "Directory membership comes from source facts; reference membership from explicit commands.");
        }
        foreach (var binding in bindings)
            Require(Text(binding.PosterId, 128) && binding.Version > 0 && collectionMap.ContainsKey(binding.CollectionId),
                "Invalid poster collection binding.");
        var memberMap = members.ToDictionary(x => x.Id);
        var bindingMap = bindings.ToDictionary(x => x.PosterId, StringComparer.Ordinal);
        foreach (var poster in posters)
            Require(poster.Id != Guid.Empty && poster.Version > 0 && Text(poster.PosterId, 128) &&
                (poster.IconAssetId is null || ContentIconAsset.ValidId(poster.IconAssetId)) &&
                memberMap.TryGetValue(poster.MembershipId, out var member) &&
                bindingMap.TryGetValue(poster.PosterId, out var binding) && member.CollectionId == binding.CollectionId,
                "Poster item must belong to its poster's main collection.");
        return catalog with
        {
            Items = Array.AsReadOnly(items), Collections = Array.AsReadOnly(collections),
            Memberships = Array.AsReadOnly(members), Bindings = Array.AsReadOnly(bindings),
            PosterItems = Array.AsReadOnly(posters)
        };
    }

    internal static bool Text(string? value, int maximum, bool allowEmpty = false) =>
        value is not null && value.Length <= maximum && (allowEmpty || !string.IsNullOrWhiteSpace(value)) &&
        !value.Contains('\0');
    internal static void Require([DoesNotReturnIf(false)] bool valid, string reason)
    {
        if (!valid) throw new ArgumentException(reason);
    }
    private static T[] Copy<T>(IReadOnlyList<T> values)
    {
        Require(values is not null && values.Count <= MaximumEntries, "Missing/oversized content catalog list.");
        var copy = values!.ToArray();
        Require(copy.All(x => x is not null), "Null catalog entry.");
        return copy;
    }
    private static void Unique<T>(IEnumerable<T> values) =>
        Require(values.Distinct().Count() == values.Count(), "Duplicate catalog identity/relation.");
}
