namespace DesktopUp.Domain;

/// <summary>ReadOnlyOverflow requires an explicit eligibility decision by the caller. An
/// insufficient-space snapshot alone never switches ordinary browsing into two axes.</summary>
public enum ContentBrowseMode { SingleAxis, ReadOnlyOverflow }

public readonly record struct ContentBrowseOffsets(double ScrollXDip, double ScrollYDip);

/// <summary>A transient browsing witness, not authored layout or an editing lease. The internal
/// and viewport points use physical X/Y, so changing the flow axis never rotates a card.</summary>
public sealed record ContentBrowseAnchor(string ItemId, int PreviousReadOrder,
    double InsideXDip, double InsideYDip, double ViewportXDip, double ViewportYDip);
