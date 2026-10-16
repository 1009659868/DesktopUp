namespace DesktopUp.Domain;

public enum ContentOperation { Reference, Open, Reveal, Copy, Move, Link, Rename, Recycle, Enumerate, Materialize }
public enum ContentTransferRole { None, CopySource, MoveSource, Destination, DestinationFolder }
public enum ContentCapabilityDenial
{
    None, UnknownType, Unavailable, NotAdvertised, ProtectedApplication,
    EphemeralSource, TransferInProgress, InvalidContext
}
public sealed record ContentCapabilityDecision(bool Allowed, ContentCapabilityDenial Denial);
public static class ContentCapabilityPolicy
{
    // Provider capabilities are necessary, not sufficient. This is not a permission bypass
    // or a file-operation authorization; journal/confirmation/CAS remain later gates.
    public static ContentCapabilityDecision Evaluate(ContentItem item, ContentOperation operation,
        ContentTransferRole transfer = ContentTransferRole.None)
    {
        ContentCatalogPolicy.ValidateItem(item);
        ContentCapabilityDecision Deny(ContentCapabilityDenial why) => new(false, why);
        if (!Enum.IsDefined(operation) || !Enum.IsDefined(transfer)) return Deny(ContentCapabilityDenial.InvalidContext);
        if (item.Kind == ContentKind.Unknown) return Deny(ContentCapabilityDenial.UnknownType);
        if (item.Kind == ContentKind.VirtualStream && operation != ContentOperation.Materialize)
            return Deny(ContentCapabilityDenial.EphemeralSource);
        if (item.Kind == ContentKind.Application &&
            operation is ContentOperation.Copy or ContentOperation.Move or ContentOperation.Rename or ContentOperation.Recycle)
            return Deny(ContentCapabilityDenial.ProtectedApplication);
        if (item.Kind == ContentKind.ShellItem &&
            operation is ContentOperation.Copy or ContentOperation.Move or ContentOperation.Rename or ContentOperation.Recycle)
            return Deny(ContentCapabilityDenial.NotAdvertised);
        // An unavailable but stable entry may still be referenced without querying or touching its source.
        if (item.Availability != ContentAvailability.Available && operation != ContentOperation.Reference)
            return Deny(ContentCapabilityDenial.Unavailable);
        if (item.Availability is ContentAvailability.RestrictedProtocol or ContentAvailability.ParseFailed or ContentAvailability.Unsupported)
            return Deny(ContentCapabilityDenial.Unavailable);
        bool mutates = operation is ContentOperation.Move or ContentOperation.Rename or ContentOperation.Recycle;
        if (transfer != ContentTransferRole.None && (mutates ||
            operation is ContentOperation.Open or ContentOperation.Copy or ContentOperation.Link or ContentOperation.Enumerate &&
                transfer is ContentTransferRole.MoveSource or ContentTransferRole.Destination))
            return Deny(ContentCapabilityDenial.TransferInProgress);
        var required = operation switch
        {
            ContentOperation.Reference => ContentCapability.Reference, ContentOperation.Open => ContentCapability.Open,
            ContentOperation.Reveal => ContentCapability.Reveal, ContentOperation.Copy => ContentCapability.Copy,
            ContentOperation.Move => ContentCapability.Move, ContentOperation.Link => ContentCapability.Link,
            ContentOperation.Rename => ContentCapability.Rename, ContentOperation.Recycle => ContentCapability.Recycle,
            ContentOperation.Enumerate => ContentCapability.Enumerate, ContentOperation.Materialize => ContentCapability.Materialize,
            _ => ContentCapability.None
        };
        return (item.Capabilities & required) == required ? new(true, ContentCapabilityDenial.None)
            : Deny(ContentCapabilityDenial.NotAdvertised);
    }
}
