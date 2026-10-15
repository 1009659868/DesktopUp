using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum IncomingDropArea { Frame, Content }
public sealed record IncomingDropLocation(IncomingDropArea Area, double? ContentX = null,
    double? ContentY = null, long LoopCycle = 0);

// Every value affecting the offered landing is part of the witness, including navigation.
// This is transient input state, never a durable file receipt or permission to delete a source.
public sealed record IncomingDropSurface(Guid RunId, string PosterId, long EntityVersion,
    long LayoutRevision, long ProjectionGeneration, DesktopPixelRect Frame,
    double ScaleX, double ScaleY, ContentRect ContentViewport, ContentFlowAxis Axis,
    double ScrollX = 0, double ScrollY = 0, double LoopPeriod = 0)
{
    public bool SupportsCopy { get; init; }
    public IncomingDropLocation? Map(PointerScreenPoint point)
    {
        var v = ContentViewport;
        if (RunId == Guid.Empty || string.IsNullOrWhiteSpace(PosterId) || EntityVersion < 0 || LayoutRevision <= 0 ||
            ProjectionGeneration < 0 || !Frame.IsUsable || !Enum.IsDefined(Axis) ||
            new[] { ScaleX, ScaleY, v.X, v.Y, v.Width, v.Height, ScrollX, ScrollY, LoopPeriod }.Any(x => !double.IsFinite(x)) ||
            ScaleX <= 0 || ScaleY <= 0 || v.X < 0 || v.Y < 0 || v.Width < 0 || v.Height < 0 ||
            v.Right > Frame.Width / ScaleX || v.Bottom > Frame.Height / ScaleY ||
            ScrollX < 0 || ScrollY < 0 || LoopPeriod < 0 || LoopPeriod is > 0 and < 1 ||
            Math.Max(Math.Max(ScrollX, ScrollY), LoopPeriod) > 16_000_000 ||
            (Axis == ContentFlowAxis.Vertical ? ScrollX != 0 : ScrollY != 0) ||
            point.X < Frame.X || point.Y < Frame.Y ||
            (long)point.X >= (long)Frame.X + Frame.Width || (long)point.Y >= (long)Frame.Y + Frame.Height)
            return null;
        (double X, double Y) local;
        try { local = ContentScreenCoordinates.ToViewport(point.X, point.Y, Frame.X, Frame.Y, ScaleX, ScaleY, v.X, v.Y); }
        catch (ArgumentException) { return null; }
        // Title, chrome and collapsed poster have no grid coordinates. Never clamp to a fake cell.
        if (!new ContentRect(0, 0, v.Width, v.Height).Contains(local.X, local.Y)) return new(IncomingDropArea.Frame);
        double x = local.X + ScrollX, y = local.Y + ScrollY;
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Max(x, y) > 16_000_000) return null;
        long cycle = 0;
        if (LoopPeriod > 0)
        {
            double main = Axis == ContentFlowAxis.Vertical ? y : x;
            cycle = checked((long)Math.Floor(main / LoopPeriod));
            if (Axis == ContentFlowAxis.Vertical) y = ContentLayoutFlow.NormalizeScroll(main, LoopPeriod);
            else x = ContentLayoutFlow.NormalizeScroll(main, LoopPeriod);
        }
        return new(IncomingDropArea.Content, x, y, cycle);
    }
}

public sealed record IncomingDropPreview(long Session, IncomingDropSurface Surface,
    PointerScreenPoint ScreenPoint, IncomingDropLocation Location, int Count)
{ public uint Effect { get; init; } = 2; }
public sealed record IncomingDropTicket(IncomingDropPreview Preview, IReadOnlyList<string> Paths);

/// <summary>No I/O during OLE hover. A release consumes one immutable payload, only against
/// the last offered geometry. A changed surface must first publish a new Over preview.</summary>
public sealed class IncomingDropSession
{
    private string[]? paths;
    private long generation;
    private (Guid RunId, string PosterId)? owner;
    public IncomingDropPreview? Preview { get; private set; }
    public bool Active => paths is not null;

    public IncomingDropPreview? Enter(IReadOnlyList<string>? input, IncomingDropSurface? surface,
        PointerScreenPoint point, uint keys, uint offeredEffects)
    {
        Leave(); generation++;
        if (input is null || input.Count is < 1 or > 100 ||
            input.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 8192 || x.Any(char.IsControl)) ||
            input.Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Count) return null;
        paths = input.ToArray();
        return Over(surface, point, keys, offeredEffects);
    }

    public IncomingDropPreview? Over(IncomingDropSurface? surface, PointerScreenPoint point, uint keys, uint offeredEffects)
    {
        if (Active && surface is not null)
        {
            var identity = (surface.RunId, surface.PosterId);
            if (owner is { } previous && previous != identity) { Leave(); return null; }
            owner = identity;
        }
        uint effect=Effect(keys,offeredEffects,surface?.SupportsCopy==true);
        Preview = Active && effect!=0 && surface?.Map(point) is { } location
            ? new(generation, surface, point, location, paths!.Length) { Effect=effect } : null;
        return Preview;
    }

    public IncomingDropTicket? Drop(IReadOnlyList<string>? input, IncomingDropSurface? surface,
        PointerScreenPoint point, uint keys, uint offeredEffects)
    {
        var previous = Preview;
        var owned = paths;
        Leave(); // End ownership before any callback/reentrancy/await.
        uint effect=Effect(keys,offeredEffects,surface?.SupportsCopy==true);
        if (owned is null || previous is null || previous.Surface != surface || effect==0 ||
            input is null || !owned.SequenceEqual(input, StringComparer.OrdinalIgnoreCase) || surface?.Map(point) is not { } location)
            return null;
        return new(previous with { ScreenPoint = point, Location = location, Effect=effect }, Array.AsReadOnly(owned));
    }

    public void Leave() { paths = null; Preview = null; owner = null; }
    public static bool MoveAllowed(uint keys, uint offeredEffects) => (offeredEffects & 2) != 0 && (keys & (8u | 32u | 2u)) == 0;
    private static uint Effect(uint keys,uint offered,bool copy) => copy && (keys&8)!=0 && (keys&(4u|32u|2u))==0
        ? (offered&1) : MoveAllowed(keys,offered)?2u:0;
}
