using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class IncomingLayoutPreviewTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical, false)] [InlineData(ContentFlowAxis.Vertical, true)]
    [InlineData(ContentFlowAxis.HorizontalLoop, false)] [InlineData(ContentFlowAxis.HorizontalLoop, true)]
    public void HoverMatchesSuccessiveRealCommitsWithoutWritingOrMoving(ContentFlowAxis axis, bool manual)
    {
        using var f = new ManagedTransferPersistenceTests.Fixture();
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("existing.txt")).Item!);
        var state = f.State; var poster = state.Catalog.Posters.Single();
        Assert.True(ContentLayoutPolicy.TryRead(poster.Poster.ContentLayoutJson, f.Main, out var layout, out _));
        var viewport = new ContentLayoutViewport(350, 420);
        layout = layout! with { FlowAxis = axis };
        var edit = manual ? new ContentLayoutEdit(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }
            : new ContentLayoutEdit(ContentLayoutEditKind.Pinned, layout.Placements[0].ItemId) { Enabled = true, PinnedIndex = 1 };
        var edited = ContentLayoutEditing.Apply(layout, edit, viewport); Assert.True(edited.Accepted, edited.Error);
        layout = edited.Document;
        Assert.Equal(StateCommandStatus.Committed, f.Repository.Execute(new("preview-mode", "preview-mode", state.Revision,
            StateReadSet.ForPoster(f.Main, poster.EntityVersion)), _ => new() { Updates = [poster.Poster with {
                ContentLayoutJson = ContentLayoutPolicy.Serialize(layout) }] }).Status);
        string raw = ContentLayoutPolicy.Serialize(layout); long revision = f.State.Revision;
        var landing = new IncomingContentPlacement(viewport, ReadOrder: 0, Column: 0, Row: 0);
        string[] paths = [f.File("incoming1.txt"), f.File("incoming2.txt")];
        var predicted = IncomingLayoutPreview.Create(layout, landing, 2);
        Assert.Equal(raw, ContentLayoutPolicy.Serialize(layout)); Assert.Equal(revision, f.State.Revision);
        Assert.All(paths, p => Assert.True(File.Exists(p))); Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.Equal(ContentLayoutPolicy.Serialize(predicted.Document), ContentLayoutPolicy.Serialize(IncomingLayoutPreview.Create(layout, landing, 2).Document));
        var ids = new List<string>();
        foreach (string path in paths)
        {
            var p = DirectTransferPreparation.Incoming(f.State, f.Main, [path], new(f.Root, f.Collection), landing);
            f.Journal.Begin(p.Plan, p.Files, p.Plan.Fingerprint, f.Collection.Id);
            Assert.Equal(TransferFinishKind.Completed, Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId)).Kind);
            var item = f.State.ContentCatalog.Items.Single(i => i.DisplayName == Path.GetFileName(path));
            var members = f.State.ContentCatalog.Memberships.Where(m => m.ContentItemId == item.Id).Select(m => m.Id).ToHashSet();
            string id = f.State.ContentCatalog.PosterItems.Single(p => members.Contains(p.MembershipId)).LayoutItemId; ids.Add(id);
            Assert.True(ContentLayoutPolicy.TryRead(f.State.Catalog.Posters.Single().Poster.ContentLayoutJson, f.Main, out var committed, out _));
            landing = landing.Advance(committed!, id);
        }
        Assert.True(ContentLayoutPolicy.TryRead(f.State.Catalog.Posters.Single().Poster.ContentLayoutJson, f.Main, out var final, out _));
        var actual = ContentLayoutSolver.Resolve(final!, viewport);
        foreach (var expected in predicted.Snapshot.Items)
        {
            string actualId = predicted.Placeholders.Contains(expected.ItemId)
                ? ids[int.Parse(expected.ItemId.Split(':').Last())] : expected.ItemId;
            var real = actual.Items.Single(i => i.ItemId == actualId);
            Assert.Equal((expected.Column, expected.Row, expected.ColumnSpan, expected.RowSpan, expected.ReadOrder),
                (real.Column, real.Row, real.ColumnSpan, real.RowSpan, real.ReadOrder));
        }
        Assert.Equal(manual, final!.Mode == ContentLayoutMode.Manual);
    }

    [Fact]
    public void ManualHoverPushesOnlyAffectedItemsAndKeepsBlankSpace()
    {
        var basis = ContentLayoutPolicy.Freeze(new ContentLayoutDocument("A", [
            new("A:near", 0) { Column = 0, Row = 0 }, new("A:far", 1) { Column = 8, Row = 20 }
        ]) { Mode = ContentLayoutMode.Manual });
        var preview = IncomingLayoutPreview.Create(basis, new(new(500, 700), Column: 0, Row: 0, ReadOrder: 0), 1);
        Assert.True(preview.Document.Placements.Single(p => p.ItemId == "A:near").Row >= 4);
        var far = preview.Document.Placements.Single(p => p.ItemId == "A:far");
        Assert.Equal((8, 20), (far.Column, far.Row));
        Assert.Equal(0, basis.Placements[0].Row);
    }

    [Theory] [InlineData(0)] [InlineData(101)]
    public void OversizedHoverIsRejectedWithoutChangingBasis(int count)
    {
        var basis = ContentLayoutSamples.Equal("A", 1); string raw = ContentLayoutPolicy.Serialize(basis);
        Assert.Throws<ArgumentException>(() => IncomingLayoutPreview.Create(basis, new(new(300, 400), 0, 0, 0), count));
        Assert.Equal(raw, ContentLayoutPolicy.Serialize(basis));
    }
}
