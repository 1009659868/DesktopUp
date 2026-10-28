using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>
/// Application-side policy for the two per-poster switches introduced by the single-axis revision:
/// the flow axis and the optional heat-driven size. Both changes go through the ordinary content
/// layout command (one semantic version, full read set CAS); this type only builds and pre-checks the
/// desired document so the UI never has to invent version arithmetic.
/// </summary>
public static class ContentLayoutFlowPolicy
{
    /// <summary>True when the axis can be applied without dropping any authored position override.
    /// A failing result is reported as a reason, never resolved by silently clearing the user's pins.</summary>
    public static string? CanSetFlowAxis(ContentLayoutDocument document, ContentFlowAxis axis,
        double scale = 1, double spacing = 1)
    {
        if (document is null) return "A content layout document is required.";
        if (document.FlowAxis == axis) return null;
        var candidate = document with { FlowAxis = axis };
        if (ContentLayoutPolicy.Validate(candidate) is { } invalid) return invalid;
        try { ContentLayoutCommandPolicy.ValidateAuthoring(candidate, scale, spacing); }
        catch (ArgumentException failure) { return failure.Message; }
        return null;
    }

    public static ContentLayoutEditResult SetFlowAxis(ContentLayoutDocument document, ContentFlowAxis axis,
        double scale = 1, double spacing = 1)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            if (!Enum.IsDefined(axis)) throw new ContentLayoutException("Unknown content flow axis.");
            // A no-op switch must not consume a document version or create an empty undo entry.
            if (document.FlowAxis == axis) return new(true, document);
            // Axis and loop are independent author choices. Physical positions and both user-mode
            // and return-state records remain intact; incompatible legacy constraints are rejected.
            var candidate = document with { FlowAxis = axis, DocumentVersion = checked(document.DocumentVersion + 1) };
            if (ContentLayoutPolicy.Validate(candidate) is { } invalid) throw new ContentLayoutException(invalid);
            // Pinned cards keep their pin: a pin that no longer fits is reported, not relocated and
            // not unpinned behind the user's back.
            ContentLayoutCommandPolicy.ValidateAuthoring(candidate, scale, spacing);
            return new(true, ContentLayoutPolicy.Freeze(candidate));
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        { return new(false, document, error.Message); }
    }

    /// <summary>Opt-in heat-driven sizing. Ordering/ranking by heat is not implemented: this switch
    /// only controls whether the heat tier may influence card size.</summary>
    public static ContentLayoutEditResult SetHeatDrivenSize(ContentLayoutDocument document, bool enabled,
        double scale = 1, double spacing = 1)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            if (document.AllowHeatDrivenSize == enabled) return new(true, document);
            var candidate = document with
            { AllowHeatDrivenSize = enabled, DocumentVersion = checked(document.DocumentVersion + 1) };
            if (ContentLayoutPolicy.Validate(candidate) is { } invalid) throw new ContentLayoutException(invalid);
            ContentLayoutCommandPolicy.ValidateAuthoring(candidate, scale, spacing);
            return new(true, ContentLayoutPolicy.Freeze(candidate));
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        { return new(false, document, error.Message); }
    }

    /// <summary>Independent loop switch: it changes only whether the main axis wraps, never the axis
    /// itself, card sizes, order or the squeezing/wrapping limits.</summary>
    public static ContentLayoutEditResult SetLoopScroll(ContentLayoutDocument document, bool enabled,
        double scale = 1, double spacing = 1)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            if (document.LoopScroll == enabled) return new(true, document);
            var candidate = document with { LoopScroll = enabled, DocumentVersion = checked(document.DocumentVersion + 1) };
            if (ContentLayoutPolicy.Validate(candidate) is { } invalid) throw new ContentLayoutException(invalid);
            ContentLayoutCommandPolicy.ValidateAuthoring(candidate, scale, spacing);
            return new(true, ContentLayoutPolicy.Freeze(candidate));
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        { return new(false, document, error.Message); }
    }
}
