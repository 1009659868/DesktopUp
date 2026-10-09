using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

internal static class RealContentFixtures
{
    public static readonly Guid ItemId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid CollectionId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid MemberId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid RelationA = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public static readonly Guid RelationB = Guid.Parse("00000000-0000-0000-0000-000000000005");
    public static ContentItem Item() => new(ItemId, ContentKind.File, new("test", "stable-id"),
        new(ContentTargetKind.FileSystem, @"Z:\fixture-only\report.txt"), "Report",
        ContentCapability.Reference | ContentCapability.Open | ContentCapability.Copy | ContentCapability.Move |
        ContentCapability.Reveal | ContentCapability.Link | ContentCapability.Rename | ContentCapability.Recycle);
    public static ContentCatalogSnapshot Catalog(ContentItem? item = null) => ContentCatalogPolicy.Freeze(new(
        [item ?? Item()], [new(CollectionId, "References", ContentCollectionKind.Reference)],
        [new(MemberId, CollectionId, ItemId)], [new("A", CollectionId), new("B", CollectionId)],
        [new(RelationA, "A", MemberId), new(RelationB, "B", MemberId)]));
    public static ContentActivationTarget Target(ContentCatalogSnapshot c, string poster = "A") =>
        new(poster, c.PosterItems.Single(x => x.PosterId == poster).LayoutItemId, 1, 1, "presented-frame");
    public static ContentOpenRequest Request(ContentCatalogSnapshot c)
    {
        var target = Target(c);
        return ContentOpenContract.Prepare(c, new(target, Guid.NewGuid(), ContentOpenOrigin.DoubleClick), target).Request!;
    }
    public static ContentOpenState Running(ContentCatalogSnapshot c) =>
        ContentOpenContract.Start(ContentOpenContract.Begin(Request(c)), c).State;
    public static ContentOpenCompletion Complete(ContentOpenState state,
        ContentOpenOutcome outcome = ContentOpenOutcome.Succeeded, ContentOpenEvidence evidence = ContentOpenEvidence.ShellAccepted) =>
        new(state.Request.ActivationId, state.Request.Source.ProviderId, outcome, evidence,
            new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
}
