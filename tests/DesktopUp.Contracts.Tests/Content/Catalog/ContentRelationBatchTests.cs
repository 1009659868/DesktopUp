using DesktopUp.Application;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentRelationBatchTests
{
    [Fact]
    public void Batch_preserves_order_missing_members_and_complete_witnesses_without_cross_poster_leakage()
    {
        using var f = new Fixture();
        f.Commit(f.AddCommand(f.Main, Entry("a.txt"), Entry("b.txt")));
        string other = f.NewPoster(); f.Commit(f.AddCommand(other, Entry("c.txt")));
        var catalog = f.Current.ContentCatalog;
        var ids = catalog.PosterItems.Select(p => p.LayoutItemId).Reverse().Append("missing").ToArray();
        var expected = ids.Select(id => ContentRelationPolicy.Resolve(catalog, f.Main, id)).ToArray();
        var actual = ContentRelationPolicy.ResolveMany(catalog, f.Main, ids);
        Assert.Equal(expected, actual);
        Assert.Throws<NotSupportedException>(() => ((IList<ContentRelationResolution?>)actual).Clear());
        var duplicate = ContentRelationPolicy.ResolveMany(catalog, f.Main, [ids[1], ids[1]]);
        Assert.Equal(duplicate[0], duplicate[1]);
        Assert.All(ContentRelationPolicy.ResolveMany(catalog, "missing-poster", ids), Assert.Null);
    }

    [Fact]
    public void Batch_checks_the_whole_catalog_even_when_the_requested_member_is_valid_or_missing()
    {
        using var f = new Fixture(); f.Commit(f.AddCommand(f.Main, Entry("a.txt"), Entry("b.txt")));
        var catalog = f.Current.ContentCatalog;
        string id = catalog.PosterItems[0].LayoutItemId;
        var corrupt = catalog with { Items = catalog.Items.Take(1).ToArray() };
        var priorFailure = Record.Exception(() => ContentRelationPolicy.Resolve(corrupt, f.Main, id));
        Assert.NotNull(priorFailure);
        foreach (string requested in new[] { id, "missing" })
        {
            var failure = Record.Exception(() => ContentRelationPolicy.ResolveMany(corrupt, f.Main, [requested]));
            Assert.NotNull(failure); Assert.Equal(priorFailure.GetType(), failure.GetType());
        }
        var first = ContentRelationPolicy.ResolveMany(catalog, f.Main, [id]);
        // Same version is not a permission to reuse a prior validated result.
        Assert.ThrowsAny<Exception>(() => ContentRelationPolicy.ResolveMany(corrupt, f.Main, [id]));
        Assert.Equal(ContentRelationPolicy.Resolve(catalog, f.Main, id), first[0]);
    }
}
