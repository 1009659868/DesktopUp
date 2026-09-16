using DesktopUp.Domain;

namespace DesktopUp.Application;

// Installed once by the composition root. This is a trusted host adapter, not a command payload.
// It must verify full catalog occupancy, effective-layout provenance, live generations and capture.
public interface ITitleRuntimeReadback
{
    bool ValidateTitleStart(TitleSpatialStart start, StateReadSet reads);
}
public interface ITitleSpatialCommitter
{
    void BindTitleRuntime(ITitleRuntimeReadback runtime);
    bool CheckpointTitle(TitleSpatialSession session, TitleRuntimeLease lease);
    TitleRuntimeLease? OpenTitle(TitleSpatialStart start, StateReadSet reads);
    StateCommandResult ExecuteTitle(TitleSpatialCommand command, TitleRuntimeLease lease);
}
// An opaque, issuer-bound, single-operation capability. JSON commands cannot mint this witness.
public sealed class TitleRuntimeLease : IDisposable
{
    internal TitleRuntimeLease(object issuer, TitleSpatialStart start, StateReadSet reads, PosterContentMetrics metrics,
        SpatialContinuityState? continuity)
    { Issuer = issuer; Start = start; Reads = reads.Freeze(); Metrics = metrics; InitialContinuity = continuity; }
    internal PosterContentMetrics Metrics { get; }
    internal SpatialContinuityState? InitialContinuity { get; }
    internal TitleProgressCheckpoint? Checkpoint { get; set; }
    internal object Issuer { get; }
    internal TitleSpatialStart Start { get; }
    internal StateReadSet Reads { get; }
    internal bool Closed { get; private set; }
    public void Dispose() => Closed = true;
    internal bool Matches(TitleSpatialCommand command) => !Closed && command.Trace.Start == Start &&
        ReferenceEquals(command.Trace.Checkpoint, Checkpoint) && command.RequestId == Start.Press.OperationId && SameReads(Reads, command.ReadSet);
    internal static bool SameReads(StateReadSet a, StateReadSet b) => a.CatalogVersion == b.CatalogVersion &&
        a.SettingsVersion == b.SettingsVersion && a.Posters.Count == b.Posters.Count &&
        a.Posters.All(p => b.Posters.TryGetValue(p.Key, out long version) && version == p.Value);
}

// One current verified prefix, not a linked list of past samples/checkpoints. Public fields are
// identity/diagnostics for the request fingerprint; state is internal and cannot be supplied by JSON.
public sealed class TitleProgressCheckpoint
{
    internal TitleProgressCheckpoint(TitleSpatialStart start, PosterContentMetrics metrics,
        SpatialContinuityState? initialContinuity, TitleSpatialState state, long segment, string digest)
    { Start = start; Metrics = metrics; InitialContinuity = initialContinuity; State = state; Segment = segment; Digest = digest; }
    public Guid Id { get; } = Guid.NewGuid();
    public long Segment { get; }
    public string Digest { get; }
    internal TitleSpatialStart Start { get; }
    internal PosterContentMetrics Metrics { get; }
    internal SpatialContinuityState? InitialContinuity { get; }
    internal TitleSpatialState State { get; }
}
