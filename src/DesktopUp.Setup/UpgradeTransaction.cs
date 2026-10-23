using System.Text;
using System.Text.Json;

namespace DesktopUp.Setup;

internal sealed class UpgradeJournal
{
    public int Schema { get; set; } = 1;
    public required string Root { get; init; }
    public required string Id { get; init; }
    public required string OldRelease { get; init; }
    public required string NewRelease { get; init; }
    public string Phase { get; set; } = "Prepared";
    public required List<FileChange> OldFiles { get; init; }
    public required List<FileChange> NewFiles { get; init; }
    public required List<RegistryValue> OldRegistry { get; init; }
    public required List<RegistryValue> NewRegistry { get; init; }
}

// The journal describes exact files, never whole directories. New files are prepared
// on the destination volume; the old app is not uninstalled and no database is opened.
internal sealed class UpgradeTransaction(IInstallRegistry registry, IInstallationLinks links, InstallationLocations locations)
{
    internal Action<string>? Checkpoint { get; init; } // In-process deterministic tests; no environment/CLI fault switch.
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Execute(string source, string destination, bool desktop)
    {
        string root = InstallationFiles.Root(destination);
        InstallationFiles.Plain(source);
        if (Directory.EnumerateDirectories(root, InstallationFiles.WorkPrefix + "*").Any())
            throw new PendingUpgradeException("发现尚未收尾的升级记录，请先恢复：" + root);
        string marker = InstallationFiles.PayloadPath(root, "app/preview-install.ini");
        string oldRelease = InstallationMetadata.ReadIni(marker, "Release");
        if (InstallationMetadata.ReadIni(marker, "Channel") != "DesktopUp.Preview" ||
            !InstallationFiles.Same(InstallationMetadata.ReadIni(marker, "Path"), root))
            throw new IOException("安装标记与目录不一致，未覆盖。");
        using var build = JsonDocument.Parse(File.ReadAllText(InstallationFiles.PayloadPath(source, "app/preview-build.json")));
        string release = build.RootElement.GetProperty("Release").GetString()!;
        if (build.RootElement.GetProperty("Channel").GetString() != "Preview" ||
            InstallationFiles.Release(release) <= InstallationFiles.Release(oldRelease))
            throw new IOException("仅支持同 Preview 通道的较新版本；未重装或降级。");
        var oldRegistry = registry.Read(oldRelease) ?? throw new IOException("旧版没有当前用户安装登记。");
        if (!InstallationFiles.Same(InstallationMetadata.Text(oldRegistry, "InstallLocation") ?? "", root) ||
            InstallationMetadata.Text(oldRegistry, "DisplayVersion") != oldRelease ||
            InstallationMetadata.Text(oldRegistry, "UninstallString") != $"\"{root}\\Uninstall.exe\"" || registry.Read(release) is not null)
            throw new IOException("版本/位置登记不一致或新版已安装，未覆盖。");
        var oldFiles = InstallationFiles.ReadPayload(root, installed: true);
        var newFiles = InstallationFiles.ReadPayload(source, installed: false);
        AddOwnedLink(oldFiles, root, oldRelease, "menu", "entry.lnk", Path.Combine(root, "DesktopUp.exe"));
        AddOwnedLink(oldFiles, root, oldRelease, "menu", "readme.lnk", Path.Combine(root, "PREVIEW-README.txt"));
        if (InstallationMetadata.ReadIni(marker, "DesktopShortcutCreated") == "1")
            AddOwnedLink(oldFiles, root, oldRelease, "desktop", "entry.lnk", Path.Combine(root, "DesktopUp.exe"));
        // Validate the entire replacement set before any old file is moved.
        var owned = oldFiles.Select(f => locations.Resolve(root, oldRelease, f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in newFiles) RequireAvailable(locations.Resolve(root, release, file), owned);
        RequireAvailable(Path.Combine(root, "app", "preview-install.ini"), owned);
        foreach (var file in oldFiles)
        {
            string path = locations.Resolve(root, oldRelease, file);
            InstallationFiles.RequireHash(path, file.SHA256);
            using var check = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        string id = Guid.NewGuid().ToString("N"), work = Path.Combine(root, InstallationFiles.WorkPrefix + id);
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(Path.Combine(work, "new"));
        Directory.CreateDirectory(Path.Combine(work, "old"));
        var journal = new UpgradeJournal { Root = root, Id = id, OldRelease = oldRelease, NewRelease = release, Phase = "Preparing",
            OldFiles = oldFiles, NewFiles = newFiles, OldRegistry = oldRegistry, NewRegistry = InstallationMetadata.Registration(root, release, 0) };
        try
        {
            Save(work, journal);
            for (int i = 0; i < newFiles.Count; i++)
                InstallationFiles.CopyVerified(locations.Resolve(source, release, newFiles[i]), Slot(work, "new", i), newFiles[i].SHA256);
            string newMarker = Slot(work, "new", newFiles.Count);
            File.WriteAllText(newMarker, $"[Install]\r\nChannel=DesktopUp.Preview\r\nRelease={release}\r\nPath={root}\r\nDesktopShortcutCreated={(desktop ? 1 : 0)}\r\n", Encoding.Unicode);
            newFiles.Add(new("payload", "app/preview-install.ini", InstallationFiles.Digest(newMarker)));
            PrepareLink(newFiles, work, root, release, "menu", "entry.lnk", Path.Combine(root, "DesktopUp.exe"), owned);
            PrepareLink(newFiles, work, root, release, "menu", "readme.lnk", Path.Combine(root, "PREVIEW-README.txt"), owned);
            if (desktop) PrepareLink(newFiles, work, root, release, "desktop", "entry.lnk", Path.Combine(root, "DesktopUp.exe"), owned);
            journal = new() { Root = root, Id = id, OldRelease = oldRelease, NewRelease = release,
                OldFiles = oldFiles, NewFiles = newFiles, OldRegistry = oldRegistry,
                NewRegistry = InstallationMetadata.Registration(root, release, newFiles.Select((_, i) => new FileInfo(Slot(work, "new", i)).Length).Sum()) };
            Save(work, journal);
            Checkpoint?.Invoke("prepared");
            if (!InstallationMetadata.Equal(registry.Read(oldRelease), oldRegistry) || registry.Read(release) is not null)
                throw new IOException("准备期间安装登记变化，已停止。");
            for (int i = 0; i < oldFiles.Count; i++)
            {
                Move(locations.Resolve(root, oldRelease, oldFiles[i]), Slot(work, "old", i), oldFiles[i].SHA256);
                Checkpoint?.Invoke("old:" + i);
            }
            for (int i = 0; i < newFiles.Count; i++)
            {
                Move(Slot(work, "new", i), locations.Resolve(root, release, newFiles[i]), newFiles[i].SHA256);
                Checkpoint?.Invoke("new:" + i);
            }
            if (!InstallationMetadata.Equal(registry.Read(oldRelease), oldRegistry) || registry.Read(release) is not null)
                throw new IOException("替换期间安装登记变化，已停止。");
            registry.Write(release, journal.NewRegistry);
            Checkpoint?.Invoke("registered-new");
            registry.Remove(oldRelease);
            Checkpoint?.Invoke("removed-old-registration");
            if (!InstallationMetadata.Equal(registry.Read(release), journal.NewRegistry)) throw new IOException("新版安装登记未完成。");
            journal.Phase = "Committed"; Save(work, journal);
        }
        catch (Exception error)
        {
            if (journal.Phase != "Preparing")
            {
                try { Rollback(work, journal); }
                catch (Exception recovery) { throw new IOException($"升级停止；恢复未完成，请保留 {work}。原因为：{error.Message}；恢复：{recovery.Message}", error); }
            }
            else CleanPreparing(work);
            throw new IOException("升级未完成，旧版已保留：" + error.Message, error);
        }
        try { Cleanup(work, journal); RemoveEmptyOldDirectories(journal); }
        catch (Exception e) { return "升级已完成，备份暂未清理；请保留记录 " + work + "。" + e.Message; }
        return "升级完成；配置、收纳内容及自启动状态保持。";
    }

    public void Recover(string destination)
    {
        string root = InstallationFiles.Root(destination);
        foreach (string work in Directory.EnumerateDirectories(root, InstallationFiles.WorkPrefix + "*"))
        {
            InstallationFiles.Plain(work);
            string file = Path.Combine(work, "journal.json");
            if (!File.Exists(file)) throw new IOException("发现未完成准备的目录，请保留现场：" + work);
            if (new FileInfo(file).Length > 8 * 1024 * 1024) throw new IOException("升级记录过大。");
            var journal = JsonSerializer.Deserialize<UpgradeJournal>(File.ReadAllText(file)) ?? throw new IOException("升级记录为空。");
            if (journal.Schema != 1 || !Guid.TryParseExact(journal.Id, "N", out _) ||
                !InstallationFiles.Same(journal.Root, root) || !InstallationFiles.Same(work, Path.Combine(root, InstallationFiles.WorkPrefix + journal.Id)) ||
                InstallationFiles.Release(journal.NewRelease) <= InstallationFiles.Release(journal.OldRelease) ||
                !InstallationFiles.Same(InstallationMetadata.Text(journal.OldRegistry, "InstallLocation") ?? "", root) ||
                !InstallationFiles.Same(InstallationMetadata.Text(journal.NewRegistry, "InstallLocation") ?? "", root))
                throw new IOException("升级记录身份不一致，未恢复。");
            foreach (var change in journal.OldFiles) _ = locations.Resolve(root, journal.OldRelease, change);
            foreach (var change in journal.NewFiles) _ = locations.Resolve(root, journal.NewRelease, change);
            if (journal.Phase == "Preparing") CleanPreparing(work);
            else if (journal.Phase is "Committed" or "RolledBack") Cleanup(work, journal);
            else if (journal.Phase == "Prepared") Rollback(work, journal);
            else throw new IOException("升级记录阶段无效。");
        }
    }

    private void Rollback(string work, UpgradeJournal journal)
    {
        var current = registry.Read(journal.NewRelease);
        if (current is not null)
        {
            if (current.Any(v => !journal.NewRegistry.Any(expected => InstallationMetadata.Equal([v], [expected]))))
                throw new IOException("新版登记被其他程序修改，未删除。");
            registry.Remove(journal.NewRelease);
        }
        for (int i = journal.NewFiles.Count - 1; i >= 0; i--)
        {
            if (File.Exists(Slot(work, "new", i))) continue; // Not published (or already rolled back).
            var change = journal.NewFiles[i];
            Move(locations.Resolve(journal.Root, journal.NewRelease, change), Slot(work, "new", i), change.SHA256);
        }
        for (int i = 0; i < journal.OldFiles.Count; i++)
        {
            var change = journal.OldFiles[i];
            string target = locations.Resolve(journal.Root, journal.OldRelease, change);
            if (File.Exists(Slot(work, "old", i))) Move(Slot(work, "old", i), target, change.SHA256);
            else InstallationFiles.RequireHash(target, change.SHA256);
        }
        var old = registry.Read(journal.OldRelease);
        if (old is not null && !InstallationMetadata.Equal(old, journal.OldRegistry)) throw new IOException("旧登记变化，未覆盖。");
        if (old is null) registry.Write(journal.OldRelease, journal.OldRegistry);
        journal.Phase = "RolledBack"; Save(work, journal);
        Cleanup(work, journal);
    }

    private static void Move(string source, string destination, string hash)
    {
        InstallationFiles.RequireHash(source, hash); InstallationFiles.Plain(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination, overwrite: false);
        InstallationFiles.RequireHash(destination, hash);
    }
    private static void RequireAvailable(string path, HashSet<string> old)
    {
        if (Directory.Exists(path) || (File.Exists(path) && !old.Contains(path)))
            throw new IOException("新版程序路径与未归属文件冲突，未覆盖：" + path);
    }
    private void AddOwnedLink(List<FileChange> files, string root, string release, string area, string name, string target)
    {
        var link = new FileChange(area, name, ""); string path = locations.Resolve(root, release, link);
        if (InstallationFiles.Same(links.Target(path) ?? "", target)) files.Add(link with { SHA256 = InstallationFiles.Digest(path) });
    }
    private void PrepareLink(List<FileChange> files, string work, string root, string release, string area, string name, string target, HashSet<string> owned)
    {
        var change = new FileChange(area, name, "");
        RequireAvailable(locations.Resolve(root, release, change), owned);
        string temporary = Slot(work, "new", files.Count) + ".lnk";
        links.Create(temporary, target); File.Move(temporary, Slot(work, "new", files.Count));
        files.Add(change with { SHA256 = InstallationFiles.Digest(Slot(work, "new", files.Count)) });
    }
    private static string Slot(string work, string area, int index) => Path.Combine(work, area, index.ToString("D6"));
    private static void Save(string work, UpgradeJournal journal)
    {
        string next = Path.Combine(work, "journal.next");
        using (var stream = new FileStream(next, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, journal, JsonOptions); stream.Flush(flushToDisk: true); }
        File.Move(next, Path.Combine(work, "journal.json"), overwrite: true);
    }
    private static void Cleanup(string work, UpgradeJournal journal)
    {
        foreach (var pair in new[] { ("old", journal.OldFiles), ("new", journal.NewFiles) })
            for (int i = 0; i < pair.Item2.Count; i++)
            {
                string path = Slot(work, pair.Item1, i);
                if (!File.Exists(path)) continue;
                InstallationFiles.RequireHash(path, pair.Item2[i].SHA256); File.Delete(path);
            }
        // Only empty directories, never recursive removal of an installation or user tree.
        foreach (string area in new[] { "old", "new" })
            if (Directory.Exists(Path.Combine(work, area))) Directory.Delete(Path.Combine(work, area));
        File.Delete(Path.Combine(work, "journal.next")); File.Delete(Path.Combine(work, "journal.json")); Directory.Delete(work);
    }
    private static void CleanPreparing(string work)
    {
        // No old files have moved and no registrations changed in this phase.
        if (Directory.Exists(Path.Combine(work, "new")))
            foreach (string path in Directory.EnumerateFiles(Path.Combine(work, "new")))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), @"^\d{6}(\.lnk)?$"))
                    throw new IOException("准备目录含未知文件，保留现场。");
                InstallationFiles.Plain(path); File.Delete(path);
            }
        foreach (string area in new[] { "new", "old" })
            if (Directory.Exists(Path.Combine(work, area))) Directory.Delete(Path.Combine(work, area));
        File.Delete(Path.Combine(work, "journal.next")); File.Delete(Path.Combine(work, "journal.json")); Directory.Delete(work);
    }
    private void RemoveEmptyOldDirectories(UpgradeJournal journal)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in journal.OldFiles)
        {
            string? parent = Path.GetDirectoryName(locations.Resolve(journal.Root, journal.OldRelease, file));
            if (file.Area == "desktop") continue;
            if (file.Area == "menu") { directories.Add(parent!); continue; }
            while (parent is not null && InstallationFiles.Within(parent, journal.Root) && !InstallationFiles.Same(parent, journal.Root))
            { directories.Add(parent); parent = Path.GetDirectoryName(parent); }
        }
        foreach (string directory in directories.OrderByDescending(d => d.Length))
        {
            InstallationFiles.Plain(directory);
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
    }
}

internal sealed class PendingUpgradeException(string message) : IOException(message);
