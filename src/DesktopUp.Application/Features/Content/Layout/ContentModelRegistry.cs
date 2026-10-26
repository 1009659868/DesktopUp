using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Composition-time trusted models, selected at runtime. Not a code-loading sandbox.</summary>
public sealed class ContentModelRegistry
{
    private sealed record Entry(IContentBehaviorModel Model, ContentModelDescriptor Descriptor, SemaphoreSlim Gate);
    private readonly IReadOnlyDictionary<(string Id, int Version), Entry> entries;
    private readonly TimeSpan budget;
    public IReadOnlyList<ContentModelDescriptor> Models { get; }

    public ContentModelRegistry(IEnumerable<IContentBehaviorModel> models, TimeSpan? computationBudget = null)
    {
        budget = computationBudget ?? TimeSpan.FromSeconds(2);
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(computationBudget));
        var registered = new Dictionary<(string, int), Entry>();
        foreach (var model in models)
        {
            var descriptor = model.Descriptor;
            ContentModelProtocol.Validate(descriptor);
            if (!registered.TryAdd((descriptor.Id, descriptor.Version), new(model, descriptor, new(1, 1))))
                throw new ArgumentException("Duplicate behavior model registration.", nameof(models));
        }
        entries = registered;
        Models = Array.AsReadOnly(registered.Values.Select(x => x.Descriptor).ToArray());
    }

    public static ContentModelRegistry BuiltIns() => new([new ActivityContentModel(), new RecentStripContentModel()]);

    public async Task<ContentHeatProposal> EvaluateAsync(ContentModelInput input, CancellationToken cancellation)
    {
        var options = input.Author.Activity;
        if (!entries.TryGetValue((options.ModelId, options.ModelVersion), out var entry) ||
            entry.Descriptor.ParameterVersion != options.ParameterVersion)
            throw new ContentLayoutException("The selected behavior model/version is unavailable.");
        cancellation.ThrowIfCancellationRequested();
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        var frozen = input with { Author = ContentLayoutPolicy.Freeze(input.Author), Items = Array.AsReadOnly(input.Items.ToArray()),
            CurrentLayout = input.CurrentLayout is { } layout ? layout with
            { Items = Array.AsReadOnly(layout.Items.ToArray()), Diagnostics = Array.AsReadOnly(layout.Diagnostics.ToArray()) } : null };
        // Posters share a bounded lane. Waiting callers time out without spawning more workers;
        // an ordinary short computation does not cause other posters' initial requests to be lost.
        if (!await entry.Gate.WaitAsync(budget, cancellation).ConfigureAwait(false))
            throw new TimeoutException("Behavior model is still computing; its previous picture is retained.");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var token = lifetime.Token;
        var work = Task.Run(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                return ContentModelProtocol.Adapt(entry.Descriptor, frozen, entry.Model.Evaluate(frozen, token));
            }
            finally { entry.Gate.Release(); }
        });
        // A timed-out worker can fault after its caller has left. Observe without retrying or scheduling UI work.
        _ = work.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        var remaining = budget - System.Diagnostics.Stopwatch.GetElapsedTime(started);
        try { return await work.WaitAsync(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, cancellation).ConfigureAwait(false); }
        catch { lifetime.Cancel(); throw; }
    }
}
