namespace DesktopUp.Domain;

// These are adapter-supplied facts, not filesystem validation or a stored schema migration.
// Roots need resolved identities and a revision so disconnect/replacement cannot reuse a preview.
public enum ManagedRootLocation { UserPersistent, Install, Cache, Temporary, Unknown }
public sealed record ManagedStorageRoot(Guid Id, string Locator, string ObjectIdentity, long Version,
    ManagedRootLocation Location, bool Available, bool Writable, int FormatVersion = 1);
public enum ManagedStorageDecision { Ready, ChooseRoot, UnknownFormat, UnsafeLocation, Unavailable, Invalid }
public static class ManagedStoragePolicy
{
    public static ManagedStorageDecision Evaluate(ManagedStorageRoot? root)
    {
        if (root is null) return ManagedStorageDecision.ChooseRoot;
        if (root.FormatVersion != 1) return ManagedStorageDecision.UnknownFormat;
        if (root.Id == Guid.Empty || root.Version < 1 ||
            !ContentCatalogPolicy.Text(root.Locator, 32768) || !ContentCatalogPolicy.Text(root.ObjectIdentity, 8192))
            return ManagedStorageDecision.Invalid;
        if (root.Location != ManagedRootLocation.UserPersistent) return ManagedStorageDecision.UnsafeLocation;
        return root.Available && root.Writable ? ManagedStorageDecision.Ready : ManagedStorageDecision.Unavailable;
    }
}

// Payload readers classify BEFORE opening a shortcut target or rendering an icon.
// Physical path payloads win over duplicate virtual formats; this never materializes a stream.
public enum ContentDropPayload { Unsupported, PhysicalPaths, ShellApplication, VirtualFiles }
public static class ContentDropPayloadPolicy
{
    public static ContentDropPayload Classify(bool physicalPaths, bool shellApplication, bool virtualFiles) =>
        physicalPaths ? ContentDropPayload.PhysicalPaths :
        virtualFiles ? ContentDropPayload.VirtualFiles :
        shellApplication ? ContentDropPayload.ShellApplication : ContentDropPayload.Unsupported;
}

[Flags]
public enum ContentDropModifiers { None = 0, Control = 1, Shift = 2, Alt = 4 }
public enum ContentDropRoute { InternalLayout, IntoManagedStorage, ToDesktop, OtherPoster, OtherTarget }
public enum ContentDropAction { None, Layout, Move, Copy, Link }
public enum ContentDropDenial
{
    None, Invalid, UnsupportedRoute, UnsupportedSource, MissingCapability, ChooseStorage,
    StorageUnavailable, TargetUnavailable, OperationNotImplemented
}
public sealed record ContentDropDecision(ContentDropAction Intent, ContentDropDenial Denial)
{
    public bool Allowed => Denial == ContentDropDenial.None;
}
public static class ContentDropIntentPolicy
{
    // First batch exposes Move only. Reserved copy/link keys never fall back to Move.
    public static ContentDropDecision Resolve(ContentItem item, ContentDropRoute route,
        ContentDropModifiers modifiers, ManagedStorageRoot? root = null, bool desktopResolved = false)
    {
        ContentCatalogPolicy.ValidateItem(item);
        if (!Enum.IsDefined(route) || (modifiers & ~(ContentDropModifiers.Control | ContentDropModifiers.Shift | ContentDropModifiers.Alt)) != 0)
            return new(ContentDropAction.None, ContentDropDenial.Invalid);
        if (route == ContentDropRoute.InternalLayout) return new(ContentDropAction.Layout, ContentDropDenial.None);
        var intent = modifiers.HasFlag(ContentDropModifiers.Alt) ||
            modifiers.HasFlag(ContentDropModifiers.Control | ContentDropModifiers.Shift) ? ContentDropAction.Link :
            modifiers.HasFlag(ContentDropModifiers.Control) ? ContentDropAction.Copy : ContentDropAction.Move;
        if (route is ContentDropRoute.OtherPoster or ContentDropRoute.OtherTarget)
            return new(intent, ContentDropDenial.UnsupportedRoute);
        if (item.Kind is not (ContentKind.File or ContentKind.Folder or ContentKind.Shortcut))
            return new(intent, ContentDropDenial.UnsupportedSource);
        var operation = intent switch { ContentDropAction.Copy => ContentOperation.Copy, ContentDropAction.Link => ContentOperation.Link, _ => ContentOperation.Move };
        if (!ContentCapabilityPolicy.Evaluate(item, operation).Allowed)
            return new(intent, ContentDropDenial.MissingCapability);
        if (intent != ContentDropAction.Move) return new(intent, ContentDropDenial.OperationNotImplemented);
        if (route == ContentDropRoute.IntoManagedStorage)
        {
            var storage = ManagedStoragePolicy.Evaluate(root);
            if (storage != ManagedStorageDecision.Ready)
                return new(intent, storage == ManagedStorageDecision.ChooseRoot ? ContentDropDenial.ChooseStorage : ContentDropDenial.StorageUnavailable);
        }
        if (route == ContentDropRoute.ToDesktop && !desktopResolved) return new(intent, ContentDropDenial.TargetUnavailable);
        return new(intent, ContentDropDenial.None);
    }
}

