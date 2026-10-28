namespace DesktopUp.Application;

// Source identity/version and catalog are frozen when the dialog opens, not when it closes.
public sealed record ControlCenterCatalogRequest(string RequestId, long Revision, StateReadSet Reads,
    string? CloneSourceId, string Title)
{
    public StateCommandSource Source { get; init; } = StateCommandSource.ControlCenter;
}
