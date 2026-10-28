using System.Security.Cryptography;
using System.Text;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

/// <summary>Fixed-seed continuous operations, not endpoint-only fixtures. No native windows or persistence.</summary>
public sealed class ContentLayoutStressTests
{
    [Theory]
    [InlineData(173)] [InlineData(947)] [InlineData(2027)] [InlineData(4093)] [InlineData(7907)]
    [InlineData(104729)] [InlineData(130363)] [InlineData(161803)] [InlineData(524287)] [InlineData(999983)]
    public void Fixed_seed_continuous_sequences_are_safe_deterministic_and_do_not_drift(int seed)
    {
        // 240 different sequences, each replayed from its starting state to prove whole-sequence determinism.
        int semanticEdits = 0, rejectedEdits = 0, geometryEdits = 0;
        for (int sequence = 0; sequence < 24; sequence++)
        {
            int currentSeed = unchecked(seed * 397 + sequence);
            var first = Run(currentSeed, sequence % 6, 40);
            Assert.Equal(first, Run(currentSeed, sequence % 6, 40));
            semanticEdits += first.SemanticEdits; rejectedEdits += first.RejectedEdits; geometryEdits += first.GeometryEdits;
        }
        // Invalid proposals are useful, but a workload that only rejects obsolete actions cannot
        // demonstrate undo, collision or revision safety for the current interaction contract.
        Assert.True(semanticEdits > 0, "The deterministic workload must commit real semantic changes.");
        Assert.True(geometryEdits > 0, "The deterministic workload must accept movement or resize changes.");
        Assert.True(rejectedEdits > 0, "The deterministic workload must also exercise whole-edit rejection.");
    }

    private sealed record SequenceResult(string Digest, int SemanticEdits, int RejectedEdits, int GeometryEdits);
    private static SequenceResult Run(int seed, int scene, int steps)
    {
        var random = new Random(seed);
        // Half the scenes explicitly exercise heat sizes; the other half retain authored spans.
        var document = Scene(scene) with { AllowHeatDrivenSize = scene % 2 == 0 };
        var viewport = new ContentLayoutViewport(354, 160);
        var history = new ContentLayoutHistory();
        var heat = new ContentHeatProposalBuffer(document.PosterId, document.ItemSetVersion);
        var trace = new StringBuilder();
        int semanticEdits = 0, rejectedEdits = 0, geometryEdits = 0;
        for (int step = 0; step < steps; step++)
        {
            string beforeJson = ContentLayoutPolicy.Serialize(document);
            var before = document;
            var ids = before.Placements.Select(p => p.ItemId).Order(StringComparer.Ordinal).ToArray();
            string? target = ids.Length == 0 ? null : ids[random.Next(ids.Length)];
            int action = random.Next(12);
            switch (action)
            {
                case 0:
                    // Width is viewport-only; walking narrow -> wide -> original must leave exact intent/geometry intact.
                    var original = ContentLayoutSolver.Resolve(document, viewport);
                    ContentLayoutSolver.Resolve(document, viewport with { WidthDip = random.Next(0, 120) });
                    ContentLayoutSolver.Resolve(document, viewport with { WidthDip = 1800 });
                    SameGeometry(original, ContentLayoutSolver.Resolve(document, viewport));
                    viewport = viewport with { WidthDip = random.Next(0, 800), HeightDip = random.Next(0, 400),
                        ScrollXDip = random.Next(0, 800), ScrollYDip = random.Next(0, 1500), Generation = step + 1 };
                    break;
                case 1:
                    CommitEdit(new(ContentLayoutEditKind.Mode) { Mode = random.Next(2) == 0
                        ? ContentLayoutMode.Automatic : ContentLayoutMode.Manual });
                    break;
                case 2:
                    if (document.Mode == ContentLayoutMode.Manual || document.Placements.Any(p => p.ItemId == target && p.FixedPosition))
                        CommitEdit(new(ContentLayoutEditKind.Move, target) { Column = random.Next(-1, 17), Row = random.Next(0, 24) });
                    else
                        CommitEdit(new(ContentLayoutEditKind.Move, target) { TargetReadOrder = random.Next(-1, Math.Max(1, ids.Length + 1)) });
                    break;
                case 3:
                    CommitEdit(new(ContentLayoutEditKind.Resize, target) { ColumnSpan = random.Next(0, 14), RowSpan = random.Next(1, 12) });
                    break;
                case 4:
                    CommitEdit(new(ContentLayoutEditKind.Pinned, target) { Enabled = random.Next(2) == 0 });
                    break;
                case 5:
                    CommitEdit(new(ContentLayoutEditKind.Pinned, target) { Enabled = random.Next(2) == 0,
                        PinnedIndex = random.Next(0, Math.Max(1, ids.Length + 2)) });
                    break;
                case 6:
                    CommitEdit(new(ContentLayoutEditKind.Guides) { ShowBaselineGrid = random.Next(2) == 0, ShowSpanLabels = random.Next(2) == 0 });
                    break;
                case 7:
                    CommitEdit(new(ContentLayoutEditKind.SizeOverride, target) { Enabled = random.Next(2) == 0 });
                    break;
                case 8:
                case 9:
                    bool undo = action == 8;
                    int count = undo ? history.UndoCount : history.RedoCount;
                    var proposal = undo ? history.ProposeUndo(document) : history.ProposeRedo(document);
                    Assert.Equal(count > 0, proposal is not null);
                    if (proposal is not null)
                    {
                        Assert.Equal(count, undo ? history.UndoCount : history.RedoCount);
                        if (random.Next(5) == 0)
                        {
                            // Simulated failed CAS must not consume either stack or alter the document.
                            history.CancelProposal();
                            Assert.False(history.Confirm(proposal, proposal.Document));
                            Assert.Equal(count, undo ? history.UndoCount : history.RedoCount);
                        }
                        else
                        {
                            ContentLayoutSolver.Resolve(proposal.Document, viewport);
                            Assert.True(history.Confirm(proposal, proposal.Document));
                            document = proposal.Document;
                        }
                    }
                    break;
                case 10:
                    // Two arrivals during protection: only the latest is allowed to attempt reflow.
                    var first = Proposal(document, step * 2L + 1, random);
                    var latest = Proposal(document, step * 2L + 2, random);
                    Assert.True(heat.Offer(first)); Assert.Null(heat.Take(true)); Assert.True(heat.Offer(latest));
                    Assert.Null(heat.Take(true)); var ready = heat.Take(false)!;
                    Assert.Equal(latest.Version, ready.Version); Assert.Null(heat.Take(false));
                    var projected = ContentLayoutProposals.ResolveHeat(document, ready).Document;
                    if (!document.AllowHeatDrivenSize || document.Mode == ContentLayoutMode.Manual)
                        Assert.Equal(document.Placements.Select(p => (p.ColumnSpan, p.RowSpan)), projected.Placements.Select(p => (p.ColumnSpan, p.RowSpan)));
                    if (document.Mode == ContentLayoutMode.Manual) Assert.True(ContentLayoutEditing.SameState(document, projected));
                    try { AssertSnapshot(ContentLayoutSolver.Resolve(projected, viewport), viewport, projected); }
                    catch (ContentLayoutException) { /* Fixed/manual constraints reject the proposal; author intent is last-good. */ }
                    Assert.Equal(beforeJson, ContentLayoutPolicy.Serialize(document));
                    break;
                default:
                    var snapshot = ContentLayoutSolver.Resolve(document, viewport);
                    var clippedView = viewport with { HeightDip = random.Next(0, 250), ScrollYDip = random.Next(0, 2000), Generation = step + 1 };
                    var clipped = ContentLayoutSolver.Reclip(snapshot, clippedView);
                    Assert.Equal(snapshot.Items.Select(p => p.Bounds), clipped.Items.Select(p => p.Bounds));
                    Assert.Equal(snapshot.ExtentWidthDip, clipped.ExtentWidthDip); Assert.Equal(snapshot.ExtentHeightDip, clipped.ExtentHeightDip);
                    AssertSnapshot(clipped, clippedView, document);
                    break;
            }
            Assert.Equal(ids, document.Placements.Select(p => p.ItemId).Order(StringComparer.Ordinal));
            Assert.Equal(before.ItemSetVersion, document.ItemSetVersion);
            VerifyVersions(before, document);
            var actual = ContentLayoutSolver.Resolve(document, viewport);
            SameGeometry(actual, ContentLayoutSolver.Resolve(document, viewport));
            AssertSnapshot(actual, viewport, document);
            var canonical = new ContentLayoutViewport(document.MaximumColumns * (document.BaseCellDip + document.BaseGapDip) - document.BaseGapDip, 1024);
            AssertSnapshot(ContentLayoutSolver.Resolve(document, canonical), canonical, document);
            Assert.InRange(history.UndoCount, 0, ContentLayoutHistory.Capacity);
            Assert.InRange(history.RedoCount, 0, ContentLayoutHistory.Capacity);
            trace.Append(action).Append(':').Append(ContentLayoutPolicy.Serialize(document)).Append('|')
                .Append(actual.ScrollXDip).Append(',').Append(actual.ScrollYDip).Append(';');

            void CommitEdit(ContentLayoutEdit edit)
            {
                var a = ContentLayoutEditing.Apply(document, edit, viewport);
                var b = ContentLayoutEditing.Apply(document, edit, viewport);
                Assert.Equal(a.Accepted, b.Accepted); Assert.Equal(a.Error, b.Error);
                Assert.True(ContentLayoutEditing.SameState(a.Document, b.Document));
                if (a.Accepted)
                {
                    if (!ContentLayoutEditing.SameState(document, a.Document))
                    {
                        semanticEdits++;
                        if (edit.Kind is ContentLayoutEditKind.Move or ContentLayoutEditKind.Resize) geometryEdits++;
                    }
                    history.Record(document, a.Document); document = a.Document;
                }
                else
                {
                    rejectedEdits++;
                    Assert.Same(document, a.Document); Assert.Equal(beforeJson, ContentLayoutPolicy.Serialize(document));
                }
            }
        }
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString()))), semanticEdits, rejectedEdits, geometryEdits);
    }

    private static ContentLayoutDocument Scene(int scene)
    {
        if (scene == 2) return ContentLayoutSamples.Illustration("A");
        if (scene == 3) return ContentLayoutSamples.Landscape("A");
        if (scene == 4) return ContentLayoutSamples.Covers("A");
        var d = ContentLayoutSamples.Equal("A", scene == 5 ? 0 : 8) with { MaximumColumns = 12 };
        if (scene == 0) return d;
        return d with { Regions = [new("main", 0, 0, 6), new("side", 1, 6, 6)],
            ReservedSpaces = [new(0, 0, 2, 4)],
            Placements = d.Placements.Select((p, i) => p with { RegionId = i < 4 ? "main" : "side" }).ToArray() };
    }

    private static ContentHeatProposal Proposal(ContentLayoutDocument d, long version, Random random) => new(d.PosterId, d.ItemSetVersion, version,
        d.Placements.Select(p => new ContentHeatItem(p.ItemId, random.Next(Math.Max(1, d.Placements.Count)), ContentLayoutProposals.TierScales[random.Next(5)])).ToArray());

    private static void VerifyVersions(ContentLayoutDocument before, ContentLayoutDocument after)
    {
        bool changed = !ContentLayoutEditing.SameState(before, after, true);
        Assert.Equal(before.DocumentVersion + (changed ? 1 : 0), after.DocumentVersion);
        var old = before.Placements.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        foreach (var p in after.Placements)
        {
            bool edited = (p with { Version = old[p.ItemId].Version }) != old[p.ItemId];
            Assert.Equal(old[p.ItemId].Version + (edited ? 1 : 0), p.Version);
        }
    }

    private static void SameGeometry(ContentLayoutSnapshot a, ContentLayoutSnapshot b)
    {
        Assert.Equal(a.Items, b.Items); Assert.Equal(a.Columns, b.Columns);
        Assert.Equal(a.ExtentWidthDip, b.ExtentWidthDip); Assert.Equal(a.ExtentHeightDip, b.ExtentHeightDip);
        Assert.Equal(a.ScrollXDip, b.ScrollXDip); Assert.Equal(a.ScrollYDip, b.ScrollYDip);
    }

    private static void AssertSnapshot(ContentLayoutSnapshot s, ContentLayoutViewport view, ContentLayoutDocument d)
    {
        Assert.Equal(d.Placements.Count, s.Items.Count);
        Assert.Equal(d.Placements.Select(p => p.ItemId).Order(StringComparer.Ordinal), s.Items.Select(p => p.ItemId).Order(StringComparer.Ordinal));
        Assert.True(double.IsFinite(s.ExtentWidthDip) && double.IsFinite(s.ExtentHeightDip));
        Assert.InRange(s.ScrollXDip, 0, Math.Max(0, s.ExtentWidthDip - view.WidthDip));
        Assert.InRange(s.ScrollYDip, 0, Math.Max(0, s.ExtentHeightDip - view.HeightDip));
        for (int i = 0; i < s.Items.Count; i++)
        {
            var p = s.Items[i];
            Assert.True(p.Bounds.Width >= 48 && p.Bounds.Height >= 48);
            bool vertical = s.FlowAxis == ContentFlowAxis.Vertical;
            int cross = vertical ? p.Column : p.Row, main = vertical ? p.Row : p.Column;
            double crossSpan = vertical ? p.ColumnSpan : p.RowSpan, mainSpan = vertical ? p.RowSpan : p.ColumnSpan;
            Assert.InRange(cross, 0, ContentLayoutPolicy.MaximumColumns - 1);
            Assert.InRange(main, 0, ContentLayoutPolicy.MaximumRow);
            Assert.True(cross + crossSpan <= ContentLayoutPolicy.MaximumColumns && main + mainSpan <= ContentLayoutPolicy.MaximumRow + 1);
            Assert.True(p.Bounds.Right <= s.ExtentWidthDip + 1e-7 && p.Bounds.Bottom <= s.ExtentHeightDip + 1e-7);
            if (!p.Clip.IsEmpty)
            {
                Assert.True(p.Clip.X >= 0 && p.Clip.Y >= 0 && p.Clip.Right <= view.WidthDip + 1e-7 && p.Clip.Bottom <= view.HeightDip + 1e-7);
                Assert.Equal(p.ItemId, s.HitTest(p.Clip.X + p.Clip.Width / 2, p.Clip.Y + p.Clip.Height / 2)!.ItemId);
            }
            for (int j = i + 1; j < s.Items.Count; j++) Assert.False(p.Bounds.Intersects(s.Items[j].Bounds));
            foreach (var blank in d.ReservedSpaces.Where(space => space.Kind == ContentReservedKind.Explicit))
            {
                double pitch = s.CellDip + s.GapDip;
                Assert.False(p.Bounds.Intersects(new(blank.Column * pitch, blank.Row * pitch, blank.ColumnSpan * pitch - s.GapDip, blank.RowSpan * pitch - s.GapDip)));
            }
        }
    }

    [Theory]
    [InlineData(.8)] [InlineData(1)] [InlineData(1.5)]
    public void Minimum_readable_metric_conflicts_reject_without_mutating_fixed_or_reserved_intent(double scale)
    {
        var d = new ContentLayoutDocument("A", [new("A:a", 0, 2, 2) { Column = 0, Row = 0, FixedPosition = true },
            new("A:b", 1, 2, 2) { Column = 2, Row = 0, FixedPosition = true }])
        { Mode = ContentLayoutMode.Anchored, BaseCellDip = 16, BaseGapDip = 0 };
        string original = ContentLayoutPolicy.Serialize(d);
        var view = new ContentLayoutViewport(600, 400, Scale: scale);
        if (scale < 1.5) Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(d, view));
        else AssertSnapshot(ContentLayoutSolver.Resolve(d, view), view, d);
        Assert.Equal(original, ContentLayoutPolicy.Serialize(d));
        d = d with { Placements = [d.Placements[0]], ReservedSpaces = [new(2, 0, 2, 4)] };
        if (scale < 1.5) Assert.Throws<ContentLayoutException>(() => ContentLayoutSolver.Resolve(d, view));
        else AssertSnapshot(ContentLayoutSolver.Resolve(d, view), view, d);
    }
}
