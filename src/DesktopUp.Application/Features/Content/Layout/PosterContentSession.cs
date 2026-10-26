using DesktopUp.Domain;

namespace DesktopUp.Application;

[Flags]
public enum PosterContentInteraction
{
    None = 0, Drag = 1, Marquee = 2, FileAction = 4, Incoming = 8, NativeHover = 16,
    Editing = 32, CenterDraft = 64, Menu = 128, Transition = 256, Command = 512,
    Naming = 1024
}

public readonly record struct PosterContentWork(PosterInputSessionKey Session, long Serial);
public sealed record PosterContentPublication(ContentHeatApplication Heat,
    PosterPresentationParameters? Theme = null, string? ThemeError = null);

/// <summary>
/// Dispatcher-affine transient state for ONE registry incarnation. The host supplies interaction
/// transitions and executes accepted work; this owner has no window, dispatcher, timer or store.
/// Native capture/resources remain host-owned. Committed author state remains in the projection.
/// </summary>
public sealed class PosterContentSession(PosterInputSessionKey identity, long initialSourceVersion = 1) : IDisposable
{
    private readonly ContentHeatSession heat = new();
    private ContentThemeProposalBuffer? theme;
    private long serial;
    private PosterContentWork? queued;
    private PosterContentInteraction interactions;

    public PosterInputSessionKey Identity { get; } = identity;
    public bool IsClosed { get; private set; }
    public bool IsSuspended { get; private set; } = true;
    public bool IsSpaceRestricted { get; private set; }
    public bool AwaitingCenterRelease { get; private set; }
    public long HighestThemeVersion { get; private set; }
    public ContentLayoutDocument? Draft { get; private set; }
    public ContentLayoutDocument? DraftProjection { get; private set; }
    public ContentLayoutHistory History { get; } = new();
    public PosterOperatingMode Mode => Has(PosterContentInteraction.Editing) ? PosterOperatingMode.Editing : PosterOperatingMode.Using;
    public string? ActiveCommand { get; private set; }
    public bool HasGesture => Has(PosterContentInteraction.Drag | PosterContentInteraction.Marquee);
    public bool HasFileAction => Has(PosterContentInteraction.FileAction);
    public bool ProposalsProtected => IsClosed || IsSuspended || IsSpaceRestricted || interactions != PosterContentInteraction.None;
    public bool CanEditLayout => !IsClosed && !IsSuspended && !HasGesture && !Has(PosterContentInteraction.Transition);
    public bool CanRefreshSource => !IsClosed && !IsSuspended &&
        !Has(PosterContentInteraction.Transition | PosterContentInteraction.Command | PosterContentInteraction.Naming);
    public bool AtSpaceSafePoint => !HasGesture && !Has(PosterContentInteraction.Menu);
    public bool HasPendingHeat => heat.HasPending;
    public long? ActiveHeatVersion => heat.ActiveVersion;
    public bool HasContinuousHeat => heat.HasContinuousProjection;
    public ContentHeatProposal? ContinuousHeat => heat.ContinuousProposal;
    public void CompleteHeatPublication(bool accepted) => heat.CompletePublication(accepted);
    public bool HasBufferedTheme => theme?.HasPending == true;
    public bool HasPendingTheme(PosterPresentationParameters current) => theme is { } t && (t.HasPending || t.Current != current);
    public ContentLayoutDocument Effective(ContentLayoutDocument author) => DraftProjection ?? Draft ?? heat.Projection ?? author;

    public bool Has(PosterContentInteraction activity) => (interactions & activity) != 0;
    // Called at the resource owner's existing begin/end boundary, never by polling other partials.
    public void SetInteraction(PosterContentInteraction activity, bool active)
    {
        if (IsClosed) return;
        interactions = active ? interactions | activity : interactions & ~activity;
    }
    public void SetCommand(string? request)
    {
        if (IsClosed) return;
        ActiveCommand = request;
        SetInteraction(PosterContentInteraction.Command, request is not null);
    }
    public void SetSuspended(bool suspended)
    {
        if (IsClosed || IsSuspended == suspended) return;
        IsSuspended = suspended;
        if (suspended) { queued = null; ClearDraft(); }
    }
    public void SetSpaceRestricted(bool restricted) { if (!IsClosed) IsSpaceRestricted = restricted; }
    public bool SetCenterDraft(bool active)
    {
        SetInteraction(PosterContentInteraction.CenterDraft, active);
        if (IsClosed || active || !AwaitingCenterRelease) return false;
        AwaitingCenterRelease = false;
        return true;
    }
    public void SetDraft(ContentLayoutDocument? author, ContentLayoutDocument? projection)
    {
        if (IsClosed) return;
        Draft = author; DraftProjection = projection;
    }
    public void ClearDraft() { Draft = null; DraftProjection = null; }
    public ContentLayoutDocument ProjectEdit(ContentLayoutDocument author) => heat.ProjectEdit(author);
    public void Reconcile(ContentLayoutDocument author, ContentLayoutViewport viewport)
    {
        if (!IsClosed) heat.Reconcile(author, viewport);
    }
    public bool OfferHeat(ContentHeatProposal proposal, ContentLayoutDocument author, bool admissible) =>
        !IsClosed && admissible && proposal.PosterId == author.PosterId && proposal.ItemSetVersion == author.ItemSetVersion && heat.Offer(proposal);

    public bool OfferTheme(ContentThemeProposal proposal, ContentLayoutDocument author,
        ContentLayoutViewport viewport, PosterPresentationParameters current, bool readOnly)
    {
        if (IsClosed || readOnly || proposal is null || proposal.Version <= HighestThemeVersion ||
            PosterPresentationPolicy.Validate(proposal.Parameters) is not null) return false;
        theme ??= new(current);
        try { ValidateTheme(proposal.Parameters, author, Effective(author), viewport); }
        catch (ArgumentException) { return false; }
        if (!theme.Offer(proposal, ProposalsProtected)) return false;
        HighestThemeVersion = proposal.Version;
        return true;
    }
    public bool TryQueue(PosterPresentationParameters current, out PosterContentWork work)
    {
        work = default;
        if (IsClosed || IsSuspended || queued is not null || !(heat.HasPending || HasPendingTheme(current))) return false;
        work = new(Identity, checked(++serial));
        queued = work;
        return true;
    }
    public void CancelQueued(PosterContentWork work) { if (queued == work) queued = null; }
    public bool BeginDrain(PosterContentWork work)
    {
        if (IsClosed || queued != work) return false;
        queued = null;
        if (ProposalsProtected)
        {
            AwaitingCenterRelease = Has(PosterContentInteraction.CenterDraft);
            return false;
        }
        AwaitingCenterRelease = false;
        return true;
    }
    public PosterContentPublication Drain(ContentLayoutDocument author, ContentLayoutViewport viewport,
        PosterPresentationParameters current)
    {
        if (ProposalsProtected) return new(new(false));
        var applied = heat.HasPending ? heat.ApplyPending(author, viewport, false) : new ContentHeatApplication(false);
        if (theme is null) return new(applied);
        theme.ApplyPending(false);
        if (theme.Current == current) return new(applied);
        try
        {
            ValidateTheme(theme.Current, Effective(author), Effective(author), viewport);
            return new(applied, theme.Current);
        }
        catch (ArgumentException error)
        {
            theme = new(current);
            return new(applied, ThemeError: error.Message);
        }
    }
    public void RejectThemePresentation(PosterPresentationParameters current) => theme = IsClosed ? null : new(current);
    private static void ValidateTheme(PosterPresentationParameters parameters, ContentLayoutDocument author,
        ContentLayoutDocument effective, ContentLayoutViewport viewport)
    {
        var p = parameters.Preferences ?? new();
        ContentLayoutCommandPolicy.ValidateAuthoring(author, p.Scale, p.Spacing);
        ContentProjectionValidation.RequireWritable(effective, viewport.WithPresentation(parameters));
    }

    public long SourceVersion { get; private set; } = initialSourceVersion;
    public bool SourceRefreshPending { get; private set; }
    public PosterSourceRefresh SourceRefresh { get; private set; } = new();
    public PosterSourceRefresh BindSource()
    {
        SourceRefresh.Close();
        SourceRefresh = new();
        if (IsClosed) SourceRefresh.Close();
        SourceVersion = 0;
        return SourceRefresh;
    }
    public bool TryAcceptSource(long version)
    {
        if (IsClosed || version <= SourceVersion || version < 1) return false;
        if (Has(PosterContentInteraction.Transition | PosterContentInteraction.Command | PosterContentInteraction.Naming))
        { SourceRefreshPending = true; return false; }
        SourceRefreshPending = false; SourceVersion = version;
        return true;
    }
    public void Dispose()
    {
        if (IsClosed) return;
        IsClosed = true; IsSuspended = true;
        queued = null; AwaitingCenterRelease = false; interactions = PosterContentInteraction.None;
        ActiveCommand = null; ClearDraft(); History.Clear();
        heat.Close(); theme = null; HighestThemeVersion = 0;
        SourceRefresh.Close(); SourceRefreshPending = false;
    }
}
