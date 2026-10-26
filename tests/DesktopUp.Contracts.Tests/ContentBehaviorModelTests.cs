using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentBehaviorModelTests
{
    private static ContentModelInput Input(string model = ContentModelProtocol.BuiltInId, ContentFlowAxis axis = ContentFlowAxis.Vertical)
    {
        var author = ContentLayoutSamples.Equal("A", 3) with
        { AllowHeatDrivenSize = true, FlowAxis = axis, Activity = new() { ModelId = model } };
        var now = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
        var items = author.Placements.Select((p, i) => new ContentActivityInput(p.ItemId, p.Order,
            new(Guid.NewGuid(), now.AddDays(-100), i == 0 ? 200 : 5, now.AddDays(i - 2)))).ToArray();
        return new(author, items, new(800, 800), now, 1);
    }

    private sealed class Model(Func<ContentModelInput, CancellationToken, ContentModelOutput> evaluate,
        ContentModelCapabilities capabilities = ContentModelProtocol.All) : IContentBehaviorModel
    {
        public ContentModelDescriptor Descriptor { get; } = new("test.model", 1, "Test", capabilities);
        public ContentModelOutput Evaluate(ContentModelInput input, CancellationToken cancellation) => evaluate(input, cancellation);
    }

    [Fact]
    public async Task Built_in_keeps_exact_accepted_policy_and_stats()
    {
        var input = Input();
        var facts = input.Items.ToArray();
        var expected = ContentActivityPolicy.Evaluate(input.Items, input.Author.Activity, input.Now);
        var actual = await ContentModelRegistry.BuiltIns().EvaluateAsync(input, default);
        Assert.Equal(expected.Select(x => (x.ItemId, x.Rank, x.RelativeScale)), actual.Items.Select(x => (x.ItemId, x.Rank, x.RelativeScale)));
        Assert.Equal(facts, input.Items); Assert.Null(actual.Transition);
        Assert.Equal(ContentModelProtocol.BuiltInId, actual.Model!.Id);
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public async Task Alternative_covers_all_dimensions_without_authoring_or_statistics_writes(ContentFlowAxis axis)
    {
        var input = Input(ContentModelProtocol.RecentId, axis);
        string before = ContentLayoutPolicy.Serialize(input.Author);
        var proposal = await ContentModelRegistry.BuiltIns().EvaluateAsync(input, default);
        Assert.Equal(input.Items[2].ItemId, proposal.Items[0].ItemId);
        Assert.Equal(ContentModelTransitionKind.Fade, proposal.Transition!.Kind);
        Assert.All(proposal.Items, x => Assert.NotNull(x.Position));
        var projection = ContentHeatSizing.Project(input.Author, proposal, input.Viewport);
        var layout = ContentLayoutSolver.Resolve(projection, input.Viewport);
        Assert.False(layout.IsLayoutReadOnly);
        Assert.All(projection.Placements, x => { Assert.False(x.FixedPosition); Assert.Null(x.Column); Assert.NotNull(x.ModelPosition); });
        Assert.Equal(before, ContentLayoutPolicy.Serialize(input.Author));
        Assert.DoesNotContain("modelPosition", ContentLayoutPolicy.Serialize(projection));
        Assert.True(ContentLayoutPolicy.TryRead(ContentLayoutPolicy.Serialize(projection), "A", out var read, out _));
        Assert.All(read!.Placements, x => Assert.Null(x.ModelPosition));
        var manual = ContentLayoutEditing.Apply(input.Author, new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual },
            input.Viewport, layout);
        Assert.True(manual.Accepted, manual.Error);
        Assert.All(manual.Document.Placements, x => { Assert.Null(x.ModelPosition); Assert.False(x.FixedPosition); });
    }

    [Fact]
    public async Task Omitted_dimensions_keep_author_sizes_and_order_even_when_heat_size_is_enabled()
    {
        var input = Input("test.model");
        var registry = new ContentModelRegistry([new Model((i, _) => new(i.Author.Placements.Select(p => new ContentModelItem(p.ItemId)).ToArray()))]);
        var heat = await registry.EvaluateAsync(input, default);
        var derived = ContentHeatSizing.Project(input.Author, heat, input.Viewport);
        Assert.Equal(input.Author.Placements, derived.Placements);
    }

    [Fact]
    public async Task Input_is_frozen_and_output_is_detached()
    {
        var input = Input("test.model");
        input = input with { CurrentLayout = ContentLayoutSolver.Resolve(input.Author, input.Viewport) };
        ContentModelItem[] output = [new(input.Items[0].ItemId, 2)];
        var model = new Model((i, _) =>
        {
            Assert.Throws<NotSupportedException>(() => ((IList<ContentPlacement>)i.Author.Placements)[0] = new("bad", 0));
            Assert.Throws<NotSupportedException>(() => ((IList<ContentActivityInput>)i.Items)[0] = i.Items[1]);
            Assert.Throws<NotSupportedException>(() => ((IList<ContentResolvedPlacement>)i.CurrentLayout!.Items)[0] = i.CurrentLayout.Items[1]);
            return new(output);
        });
        var proposal = await new ContentModelRegistry([model]).EvaluateAsync(input, default);
        output[0] = new("foreign", 0);
        Assert.Equal(input.Items[0].ItemId, proposal.Items[0].ItemId);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("duplicate")]
    [InlineData("nan")]
    [InlineData("negative-position")]
    [InlineData("transition")]
    [InlineData("capability")]
    public async Task Invalid_model_output_is_rejected_before_growth_or_publication(string fault)
    {
        var input = Input("test.model");
        var item = new ContentModelItem(input.Items[0].ItemId, 0);
        var model = new Model((_, _) => fault switch
        {
            "foreign" => new([item with { ItemId = "B:foreign" }]),
            "duplicate" => new([item, item]),
            "nan" => new([item with { RelativeScale = double.NaN }]),
            "negative-position" => new([item with { Position = new(-1, 0) }]),
            "transition" => new([item], new(ContentModelTransitionKind.Fade, 301)),
            _ => new([item with { Position = new(0, 0) }])
        }, fault == "capability" ? ContentModelCapabilities.Ranking : ContentModelProtocol.All);
        await Assert.ThrowsAsync<ContentLayoutException>(() => new ContentModelRegistry([model]).EvaluateAsync(input, default));
    }

    [Fact]
    public async Task Manual_fixed_pins_and_conflicting_positions_preserve_host_protection()
    {
        var input = Input(ContentModelProtocol.RecentId);
        var heat = await ContentModelRegistry.BuiltIns().EvaluateAsync(input, default);
        var pinned = input.Author with { Placements = input.Author.Placements.Select((p, i) => p with { PinnedIndex = i == 0 ? 1 : null }).ToArray() };
        Assert.All(ContentHeatSizing.Project(pinned, heat, input.Viewport).Placements, x => Assert.Null(x.ModelPosition));
        var manual = input.Author with { Mode = ContentLayoutMode.Manual };
        Assert.Equal(manual, ContentHeatSizing.Project(manual, heat, input.Viewport));
        var collision = heat with { Items = heat.Items.Select(x => x with { Position = new(0, 0) }).ToArray() };
        var fallback = ContentHeatSizing.Project(input.Author, collision, input.Viewport);
        Assert.All(fallback.Placements, x => Assert.Null(x.ModelPosition));
        Assert.False(ContentLayoutSolver.Resolve(fallback, input.Viewport).IsLayoutReadOnly);
        var fixedAuthor = input.Author with { Placements = input.Author.Placements.Select((p, i) => i == 0
            ? p with { FixedPosition = true, Column = 0, Row = 0 } : p).ToArray() };
        var fixedProjection = ContentHeatSizing.Project(fixedAuthor, heat, input.Viewport);
        Assert.Null(fixedProjection.Placements[0].ModelPosition);
        Assert.Equal(fixedAuthor.Placements[0].ColumnSpan, fixedProjection.Placements[0].ColumnSpan);
    }

    [Fact]
    public async Task Switching_uses_same_session_and_native_rejection_restores_last_good()
    {
        var input = Input(); var registry = ContentModelRegistry.BuiltIns();
        var first = await registry.EvaluateAsync(input, default);
        var session = new ContentHeatSession(); session.Reconcile(input.Author, input.Viewport);
        Assert.True(session.Offer(first)); Assert.True(session.ApplyPending(input.Author, input.Viewport, false).Applied);
        session.CompletePublication(true);
        var next = input with { Author = input.Author with { Activity = new() { ModelId = ContentModelProtocol.RecentId }, DocumentVersion = 2 }, RequestVersion = 2 };
        session.Reconcile(next.Author, input.Viewport);
        var old = session.Projection;
        var second = await registry.EvaluateAsync(next, default);
        Assert.True(session.Offer(second)); Assert.False(session.ApplyPending(next.Author, input.Viewport, true).Applied);
        Assert.True(session.ApplyPending(next.Author, input.Viewport, false).Applied);
        session.CompletePublication(false);
        Assert.Equal(1, session.ActiveVersion); Assert.Same(old, session.Projection);
        Assert.True(session.Offer(second with { Version = 3, AuthorDocumentVersion = 1 }));
        Assert.False(session.ApplyPending(next.Author, input.Viewport, false).Applied);
        Assert.Equal(1, session.ActiveVersion);
    }

    [Fact]
    public async Task Missing_or_incompatible_model_never_silently_selects_another()
    {
        await Assert.ThrowsAsync<ContentLayoutException>(() => ContentModelRegistry.BuiltIns().EvaluateAsync(Input("missing"), default));
        var model = new Model((_, _) => new([]));
        Assert.Throws<ArgumentException>(() => new ContentModelRegistry([model, model]));
        Assert.Throws<ContentLayoutException>(() => ContentModelProtocol.Validate(model.Descriptor with { ProtocolVersion = 2 }));
    }

    [Fact]
    public async Task Timeout_does_not_spawn_unbounded_workers_or_block_other_models()
    {
        using var release = new ManualResetEventSlim();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var blocked = new Model((_, _) => { try { Interlocked.Increment(ref calls); release.Wait(); return new([]); }
            finally { finished.TrySetResult(); } });
        var registry = new ContentModelRegistry([blocked, new ActivityContentModel()], TimeSpan.FromMilliseconds(100));
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => registry.EvaluateAsync(Input("test.model"), default));
            await Assert.ThrowsAsync<TimeoutException>(() => registry.EvaluateAsync(Input("test.model"), default));
            Assert.Equal(1, calls);
            Assert.NotNull(await registry.EvaluateAsync(Input(), default));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.EvaluateAsync(Input(), cancelled.Token));
        }
        finally { release.Set(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public void Old_options_default_to_builtin_and_new_choice_roundtrips_without_sql_migration()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<ContentActivityOptions>("{}")!;
        Assert.Equal(ContentModelProtocol.BuiltInId, old.ModelId);
        var author = Input(ContentModelProtocol.RecentId).Author;
        Assert.True(ContentLayoutPolicy.TryRead(ContentLayoutPolicy.Serialize(author), "A", out var restored, out _));
        Assert.Equal(author.Activity, restored!.Activity);
    }
}
