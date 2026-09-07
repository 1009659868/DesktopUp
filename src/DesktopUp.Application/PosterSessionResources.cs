namespace DesktopUp.Application;

public sealed class PosterSessionLedger(IResourceLedger shared, string posterId, long epoch) : IResourceLedger
{
    private readonly string _prefix = $"poster/{Uri.EscapeDataString(posterId)}/{epoch}/";
    public IReadOnlyCollection<string> ActiveResources => shared.ActiveResources
        .Where(id => id.StartsWith(_prefix, StringComparison.Ordinal)).ToArray();
    public IDisposable Acquire(string resourceId, string kind) => shared.Acquire(_prefix + resourceId, kind);
    public void WriteSnapshot() => shared.WriteSnapshot();
}

public sealed class PosterSessionLog(IStructuredLog shared, string posterId, long epoch) : IStructuredLog
{
    public void Write(string level, string eventName, object? data = null) =>
        shared.Write(level, eventName, new { posterId, sessionEpoch = epoch, detail = data });
}
