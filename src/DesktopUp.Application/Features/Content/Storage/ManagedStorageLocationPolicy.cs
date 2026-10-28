using System.Security.Cryptography;
using System.Text;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum StorageProfileKind { Daily, Preview, Isolated }
public sealed record ManagedStorageSelection(string ProfileIdentity, long Version, Guid? ActiveRootId = null, int FormatVersion = 1);
public sealed record StorageRootObservation(ManagedStorageRoot Root, bool OwnershipVerified);
public enum StorageLocationAction { UseActive, SelectSoleLegacyRoot, ProposeDefault, ChooseLegacyRoot, Refuse }
public sealed record StorageLocationDecision(StorageLocationAction Action, ManagedStorageRoot? Root = null, string? Reason = null);

// D2 pure proposals. Neither selecting an existing root nor calculating a default path creates,
// claims, registers or migrates a directory. The filesystem adapter must verify it again.
public static class ManagedStorageLocationPolicy
{
    public static StorageLocationDecision Resolve(string profileIdentity, ManagedStorageSelection selection,
        IReadOnlyList<StorageRootObservation> registered)
    {
        if (string.IsNullOrWhiteSpace(profileIdentity) || selection.ProfileIdentity != profileIdentity ||
            selection.FormatVersion != 1 || selection.Version < 0 || registered.Count > 256 ||
            registered.Any(r => r.Root.Id == Guid.Empty) ||
            registered.Select(r => r.Root.Id).Distinct().Count() != registered.Count)
            return new(StorageLocationAction.Refuse, Reason: "InvalidOrForeignSelection");
        StorageLocationDecision Use(StorageRootObservation observation, StorageLocationAction action) =>
            observation.OwnershipVerified && ManagedStoragePolicy.Evaluate(observation.Root) == ManagedStorageDecision.Ready
                ? new(action, observation.Root)
                : new(StorageLocationAction.Refuse, Reason: "RootUnavailableOrNotOwned");
        if (selection.ActiveRootId is { } active)
        {
            var found = registered.SingleOrDefault(r => r.Root.Id == active);
            // Do not route new files to a different root just because the selected disk is missing.
            return found is null ? new(StorageLocationAction.Refuse, Reason: "ActiveRootMissing") : Use(found, StorageLocationAction.UseActive);
        }
        return registered.Count switch
        {
            0 => new(StorageLocationAction.ProposeDefault),
            1 => Use(registered[0], StorageLocationAction.SelectSoleLegacyRoot),
            _ => new(StorageLocationAction.ChooseLegacyRoot, Reason: "DoNotGuessOrMergeLegacyRoots")
        };
    }

    public static string ProposeDefaultPath(string profileIdentity, StorageProfileKind kind,
        string localApplicationData, string? isolatedStorageBase, IReadOnlyList<string> protectedDirectories)
    {
        if (string.IsNullOrWhiteSpace(profileIdentity) || profileIdentity.Length > 8192 ||
            profileIdentity.Any(char.IsControl) || !Enum.IsDefined(kind))
            throw new ArgumentException("A stable physical profile identity and known profile kind are required.");
        // Isolated runs MUST supply their own location. There is no fallback to a daily directory.
        string basePath = kind == StorageProfileKind.Isolated
            ? Canonical(isolatedStorageBase ?? throw new ArgumentException("Isolated storage base is required."))
            : Path.Combine(Canonical(localApplicationData), "DesktopUp", "Storage");
        string channel = kind.ToString().ToLowerInvariant();
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profileIdentity))).ToLowerInvariant();
        string candidate = Path.Combine(basePath, channel, identity);
        foreach (string directory in protectedDirectories)
        {
            string forbidden = Canonical(directory);
            if (Overlaps(candidate, forbidden)) throw new ArgumentException("Storage overlaps a protected directory.");
        }
        return candidate;
    }

    // Lexical checks only. Reparse points, volume identity, ownership and access need native leases.
    private static string Canonical(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 8192 || path.Any(char.IsControl) ||
            path.Length < 4 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] is not ('/' or '\\') ||
            path.AsSpan(2).Contains(':') || path.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0 ||
            path.Split(['/', '\\']).Skip(1).Any(p => p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.')))
            throw new ArgumentException("An explicit unambiguous local directory is required.");
        string result = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (result.Length <= 3) throw new ArgumentException("A volume root is not a storage base.");
        return result;
    }
    private static bool Overlaps(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
        a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
