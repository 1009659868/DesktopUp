namespace DesktopUp.Domain;

[Flags]
public enum ContentModelCapabilities { None = 0, Ranking = 1, Scaling = 2, Position = 4, Transition = 8 }
public enum ContentModelTransitionKind { None, Fade }
public enum ContentModelEasing { Linear, EaseOut }
public sealed record ContentModelTransition(ContentModelTransitionKind Kind, int DurationMs = 160,
    ContentModelEasing Easing = ContentModelEasing.EaseOut);
public sealed record ContentModelPosition(int Column, int Row);
public sealed record ContentModelDescriptor(string Id, int Version, string Name, ContentModelCapabilities Capabilities,
    int ProtocolVersion = 1, int ParameterVersion = 1);
public sealed record ContentModelInput(ContentLayoutDocument Author, IReadOnlyList<ContentActivityInput> Items,
    ContentLayoutViewport Viewport, DateTimeOffset Now, long RequestVersion)
{
    // Optional last published geometry; carries its own author/member/generation witnesses.
    // Models may inspect it, but it does not grant permission to reuse stale output.
    public ContentLayoutSnapshot? CurrentLayout { get; init; }
}
public sealed record ContentModelItem(string ItemId, int? Rank = null, double? RelativeScale = null,
    ContentModelPosition? Position = null, string Reason = "model");
public sealed record ContentModelOutput(IReadOnlyList<ContentModelItem> Items, ContentModelTransition? Transition = null);

/// <summary>Trusted, side-effect-free model. The host owns facts, scheduling, validation and presentation.</summary>
public interface IContentBehaviorModel
{
    ContentModelDescriptor Descriptor { get; }
    ContentModelOutput Evaluate(ContentModelInput input, CancellationToken cancellation);
}

public static class ContentModelProtocol
{
    public const int Version = 1;
    public const string BuiltInId = "desktopup.activity";
    public const string RecentId = "desktopup.recent-strip";
    public const ContentModelCapabilities All = ContentModelCapabilities.Ranking | ContentModelCapabilities.Scaling |
        ContentModelCapabilities.Position | ContentModelCapabilities.Transition;

    public static bool ValidId(string? id) => id is { Length: > 0 and <= 80 } &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    public static void Validate(ContentModelDescriptor descriptor)
    {
        if (descriptor is null || !ValidId(descriptor.Id) || descriptor.Version < 1 || descriptor.ProtocolVersion != Version ||
            descriptor.ParameterVersion != 1 || string.IsNullOrWhiteSpace(descriptor.Name) || descriptor.Name.Length > 100 ||
            (descriptor.Capabilities & ~All) != 0)
            throw new ContentLayoutException("Unsupported behavior model descriptor.");
    }

    public static void Validate(ContentModelTransition? transition)
    {
        if (transition is not null && (!Enum.IsDefined(transition.Kind) || !Enum.IsDefined(transition.Easing) ||
            transition.DurationMs is < 0 or > 300))
            throw new ContentLayoutException("Unsupported model transition.");
    }

    // Normalize optional dimensions once, at the host boundary; models never choose proposal identity.
    public static ContentHeatProposal Adapt(ContentModelDescriptor descriptor, ContentModelInput input, ContentModelOutput output)
    {
        Validate(descriptor);
        ArgumentNullException.ThrowIfNull(output);
        Validate(output.Transition);
        if (output.Items is null || output.Items.Count > input.Author.Placements.Count ||
            output.Transition is not null && !descriptor.Capabilities.HasFlag(ContentModelCapabilities.Transition))
            throw new ContentLayoutException("Model output exceeds its declared capabilities.");
        var members = input.Author.Placements.ToDictionary(x => x.ItemId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<ContentHeatItem>(output.Items.Count);
        foreach (var item in output.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ItemId) || !members.TryGetValue(item.ItemId, out var author) || !seen.Add(item.ItemId) ||
                item.Rank is < 0 or > ContentLayoutPolicy.MaximumItems ||
                item.RelativeScale is { } scale && (!double.IsFinite(scale) || scale is < .5 or > 3) ||
                item.Position is { } position && (position.Column is < 0 or > 65535 || position.Row is < 0 or > 65535) ||
                item.Reason is null || item.Reason.Length > 512 ||
                item.Rank.HasValue && !descriptor.Capabilities.HasFlag(ContentModelCapabilities.Ranking) ||
                item.RelativeScale.HasValue && !descriptor.Capabilities.HasFlag(ContentModelCapabilities.Scaling) ||
                item.Position is not null && !descriptor.Capabilities.HasFlag(ContentModelCapabilities.Position))
                throw new ContentLayoutException("Invalid model item or undeclared capability.");
            items.Add(new(item.ItemId, item.Rank ?? author.Order, item.RelativeScale ?? 1, item.Reason)
            { AdjustSize = item.RelativeScale.HasValue, Position = item.Position });
        }
        return new(input.Author.PosterId, input.Author.ItemSetVersion, input.RequestVersion,
            Array.AsReadOnly(items.ToArray()), ContentLayoutProposals.ContinuousMappingVersion)
        { AuthorDocumentVersion = input.Author.DocumentVersion, Model = descriptor, Transition = output.Transition };
    }
}
