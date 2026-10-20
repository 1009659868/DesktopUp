using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

internal static class ContentCompatibilityReviewFixture
{
    internal static void Create(string profile)
    {
        // Reuse the accepted four-poster fixture and its owned-path validation.
        CrossPosterReviewFixture.Create(profile);
        string review = Path.GetDirectoryName(Path.GetFullPath(profile))!;
        string targets = Path.Combine(review, "shortcut-targets");
        Directory.CreateDirectory(targets);
        string target = Path.Combine(targets, "target-kept.txt");
        File.WriteAllText(target, "05-13 owned target. Moving a shortcut must not move or change this file.");
        string incoming = Path.Combine(review, "incoming-files");
        File.WriteAllText(Path.Combine(incoming, "中文 空格.txt"), "05-13 owned Unicode sample");
        File.WriteAllText(Path.Combine(incoming, "本地入口.url"), "[InternetShortcut]\r\nURL=" + new Uri(target).AbsoluteUri + "\r\n");
        string folder = Path.Combine(incoming, "folder-named.lnk");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "child.txt"), "05-13 folder, not a shortcut");
        using var store = new SqliteStateStore(new UserDataPaths(profile));
        if (!store.Initialize().Succeeded) throw new IOException("Owned compatibility profile could not reopen.");
        var state = new SqliteStateCommandRepository(store).Read();
        var storage = new SqliteTransferJournal(store).StorageLocations().Single();
        string poster = BuiltInStateIdentities.PrimaryPosterId;
        new ContentCreationFixture(store).Create(poster, new(storage, state.ContentVersion,
            state.Catalog.Posters.Single(p => p.Poster.PosterId == poster).EntityVersion),
            NewContentKind.Shortcut, "本地快捷方式.lnk", target);
        File.WriteAllText(Path.Combine(review, ".compatibility-fixture"), "owned");
    }
}
