using System.Text;
using System.Text.Json;

namespace DesktopUp.Setup;

internal sealed class UpgradeJournal
{
    public int Schema { get; set; } = 1;
    public required string Root { get; init; }
    public string? PreviousRoot { get; init; }
    public string Origin => PreviousRoot ?? Root;
    public required string Id { get; init; }
    public required string OldRelease { get; init; }
    public required string NewRelease { get; init; }
    public string? PreviousRegistryId { get; init; }
    public string? OldId => Schema == 1 ? OldRelease : PreviousRegistryId;
    public string NewId => Schema == 1 ? NewRelease : WindowsInstallRegistry.StableId;
    public string? OldStartup { get; init; }
    public string? NewStartup { get; init; }
    public string Phase { get; set; } = "Preparing";
    public required List<FileChange> OldFiles { get; init; }
    public required List<FileChange> NewFiles { get; init; }
    public required List<RegistryValue> OldRegistry { get; init; }
    public required List<RegistryValue> NewRegistry { get; set; }
}

// Backups remain on the old volume; staged program files remain on the new volume.
// No databases, collection files or return-content use cases enter this transaction.
internal sealed class UpgradeTransaction(IInstallRegistry registry, IInstallationLinks links,
    InstallationLocations locations, IInstallationStartup startup)
{
    internal Action<string>? Checkpoint { get; init; }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Execute(string source, string destination, bool desktop)
    {
        string root = InstallationFiles.Root(destination);
        if (File.Exists(locations.Pending)) throw new PendingUpgradeException("发现未完成安装事务，请先恢复；记录：" + locations.Pending);
        if (Directory.Exists(root) && Directory.EnumerateDirectories(root, InstallationFiles.WorkPrefix + "*").Any())
            throw new PendingUpgradeException("发现未完成安装记录，请先恢复：" + root);
        var existing = InstallationIdentity.Discover(registry);
        string origin = existing?.Root ?? root;
        if (!InstallationFiles.Same(root, origin) && (InstallationFiles.Within(root, origin) || InstallationFiles.Within(origin, root)))
            throw new IOException("新旧安装目录不能相互包含。");
        foreach (string path in new[] { root, origin }.Distinct(StringComparer.OrdinalIgnoreCase))
            if (Directory.Exists(path) && Directory.EnumerateDirectories(path, InstallationFiles.WorkPrefix + "*").Any())
                throw new PendingUpgradeException("发现尚未收尾的安装记录，请先恢复：" + path);
        InstallationFiles.Plain(source);
        using var build = JsonDocument.Parse(File.ReadAllText(InstallationFiles.PayloadPath(source, "app/preview-build.json")));
        string release = build.RootElement.GetProperty("Release").GetString()!;
        var version = InstallationFiles.Release(release);
        if (build.RootElement.GetProperty("Channel").GetString() != "Preview" ||
            (existing is not null && version < InstallationFiles.Release(existing.Release)))
            throw new IOException("仅支持同 Preview 通道，不能降级。");
        if (existing is not null && version == InstallationFiles.Release(existing.Release) && InstallationFiles.Same(root, origin))
            throw new IOException("此版本已安装；可选择新目录迁移，不会新增并行安装。");
        if (InstallationFiles.Within(source, root) || InstallationFiles.Within(root, source) ||
            InstallationFiles.Within(source, origin) || InstallationFiles.Within(origin, source))
            throw new IOException("安装载荷不能与新旧安装目录重叠。");

        var oldFiles = existing is null ? [] : InstallationFiles.ReadPayload(origin, installed: true);
        var newFiles = InstallationFiles.ReadPayload(source, installed: false);
        string oldRelease = existing?.Release ?? release;
        if (existing is not null)
        {
            AddOwnedLink(oldFiles, origin, oldRelease, "menu", "entry.lnk", Path.Combine(origin, "DesktopUp.exe"));
            AddOwnedLink(oldFiles, origin, oldRelease, "menu", "readme.lnk", Path.Combine(origin, "PREVIEW-README.txt"));
            if (InstallationMetadata.ReadIni(InstallationFiles.PayloadPath(origin, "app/preview-install.ini"), "DesktopShortcutCreated") == "1")
                AddOwnedLink(oldFiles, origin, oldRelease, "desktop", "entry.lnk", Path.Combine(origin, "DesktopUp.exe"));
        }
        var owned = oldFiles.Select(f => locations.Resolve(origin, oldRelease, f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existing is null || !InstallationFiles.Same(root, origin))
            foreach (string name in new[] { "app", "licenses", "DesktopUp.exe", "Uninstall.exe", "PREVIEW-README.txt" })
                if (File.Exists(Path.Combine(root, name)) || Directory.Exists(Path.Combine(root, name)))
                    throw new IOException("目标含未归属的程序路径：" + Path.Combine(root, name));
        foreach (var file in newFiles) RequireAvailable(locations.Resolve(root, release, file), owned);
        RequireAvailable(Path.Combine(root, "app", "preview-install.ini"), owned);
        foreach (var file in oldFiles)
        {
            string path = locations.Resolve(origin, oldRelease, file);
            InstallationFiles.RequireHash(path, file.SHA256);
            using var check = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        string? beforeStartup = startup.Read(), afterStartup = beforeStartup;
        if (existing is not null && string.Equals(beforeStartup, StartupCommand(origin), StringComparison.OrdinalIgnoreCase))
            afterStartup = StartupCommand(root);
        var journal = new UpgradeJournal { Schema = 2, Root = root, PreviousRoot = origin, Id = Guid.NewGuid().ToString("N"),
            OldRelease = oldRelease, NewRelease = release, PreviousRegistryId = existing?.Id, OldFiles = oldFiles, NewFiles = newFiles,
            OldRegistry = existing?.Registration ?? [], NewRegistry = [], OldStartup = beforeStartup, NewStartup = afterStartup };
        string work = Work(root, journal.Id);
        InstallationFiles.Plain(locations.StateDirectory); Directory.CreateDirectory(locations.StateDirectory);
        Directory.CreateDirectory(Path.Combine(work, "new"));
        Directory.CreateDirectory(Path.Combine(Work(origin, journal.Id), "old"));
        try
        {
            Save(work, journal); WritePending(journal);
            for (int i = 0; i < newFiles.Count; i++)
                InstallationFiles.CopyVerified(locations.Resolve(source, release, newFiles[i]), Slot(work, "new", i), newFiles[i].SHA256);
            string marker = Slot(work, "new", newFiles.Count);
            File.WriteAllText(marker, $"[Install]\r\nChannel=DesktopUp.Preview\r\nRelease={release}\r\nPath={root}\r\nDesktopShortcutCreated={(desktop ? 1 : 0)}\r\n", Encoding.Unicode);
            newFiles.Add(new("payload", "app/preview-install.ini", InstallationFiles.Digest(marker)));
            PrepareLink(newFiles, work, root, release, "menu", "entry.lnk", Path.Combine(root, "DesktopUp.exe"), owned);
            PrepareLink(newFiles, work, root, release, "menu", "readme.lnk", Path.Combine(root, "PREVIEW-README.txt"), owned);
            if (desktop) PrepareLink(newFiles, work, root, release, "desktop", "entry.lnk", Path.Combine(root, "DesktopUp.exe"), owned);
            journal.NewRegistry = InstallationMetadata.Registration(root, release, newFiles.Select((_, i) => new FileInfo(Slot(work, "new", i)).Length).Sum());
            journal.Phase = "Prepared"; Save(work, journal);
            Checkpoint?.Invoke("prepared");
            RequireOriginalRegistration(journal);
            if (startup.Read() != journal.OldStartup) throw new IOException("准备期间自启动设置变化，未覆盖。");
            for (int i = 0; i < oldFiles.Count; i++)
            {
                Move(locations.Resolve(origin, oldRelease, oldFiles[i]), OldSlot(journal, i), oldFiles[i].SHA256);
                Checkpoint?.Invoke("old:" + i);
            }
            for (int i = 0; i < newFiles.Count; i++)
            {
                Move(Slot(work, "new", i), locations.Resolve(root, release, newFiles[i]), newFiles[i].SHA256);
                Checkpoint?.Invoke("new:" + i);
            }
            RequireOriginalRegistration(journal);
            startup.Replace(journal.OldStartup, journal.NewStartup);
            Checkpoint?.Invoke("startup-updated");
            registry.Write(journal.NewId, journal.NewRegistry);
            Checkpoint?.Invoke("registered-new");
            if (journal.OldId is not null && journal.OldId != journal.NewId) registry.Remove(journal.OldId);
            Checkpoint?.Invoke("removed-old-registration");
            RequireCommitted(journal);
            journal.Phase = "Committed"; Save(work, journal);
            Checkpoint?.Invoke("committed");
        }
        catch (Exception error)
        {
            if (journal.Phase == "Committed") throw new PendingUpgradeException("登记已切换，请恢复收尾；记录：" + work + "。" + error.Message);
            try
            {
                if (journal.Phase == "Preparing") CleanPreparing(work, journal);
                else Rollback(work, journal);
            }
            catch (Exception recovery) { throw new IOException($"安装停止；恢复未完成，请保留 {work}。原因为：{error.Message}；恢复：{recovery.Message}", error); }
            throw new IOException("安装未完成，原文件与登记已保留：" + error.Message, error);
        }
        try { RemoveEmptyOldDirectories(journal); Cleanup(work, journal); }
        catch (Exception e) { throw new PendingUpgradeException("程序已切换，备份收尾尚未完成；请保留记录并执行恢复收尾。" + e.Message); }
        return "安装完成；当前用户仅保留此安装登记，配置、收纳内容及自启动状态保持。";
    }

    public void Recover(string destination)
    {
        string root = InstallationFiles.Root(destination);
        if (File.Exists(locations.Pending)) root = InstallationFiles.Root(InstallationMetadata.ReadIni(locations.Pending, "Path"));
        foreach (string work in Directory.EnumerateDirectories(root, InstallationFiles.WorkPrefix + "*"))
        {
            InstallationFiles.Plain(work);
            string file = Path.Combine(work, "journal.json");
            if (!File.Exists(file) || new FileInfo(file).Length > 8 * 1024 * 1024) throw new IOException("安装记录缺失或过大，保留现场：" + work);
            var j = JsonSerializer.Deserialize<UpgradeJournal>(File.ReadAllText(file)) ?? throw new IOException("安装记录为空。");
            ValidateJournal(work, root, j);
            if (j.Phase == "Preparing") CleanPreparing(work, j);
            else if (j.Phase == "Committed") { RequireCommitted(j); RemoveEmptyOldDirectories(j); Cleanup(work, j); }
            else if (j.Phase == "RolledBack") { RequireOriginalRegistration(j); Cleanup(work, j); }
            else if (j.Phase == "Prepared") Rollback(work, j);
            else throw new IOException("安装记录阶段无效。");
        }
        if (File.Exists(locations.Pending)) throw new IOException("仍有未完成的安装记录，未忽略：" + locations.Pending);
    }

    private void ValidateJournal(string work, string root, UpgradeJournal j)
    {
        _ = InstallationFiles.Root(j.Origin);
        if (j.Schema is not (1 or 2) || !Guid.TryParseExact(j.Id, "N", out _) || !InstallationFiles.Same(j.Root, root) ||
            !InstallationFiles.Same(work, Work(root, j.Id)) || InstallationFiles.Release(j.NewRelease) < InstallationFiles.Release(j.OldRelease) ||
            (j.OldId is not null && j.OldId != WindowsInstallRegistry.StableId && j.OldId != j.OldRelease) ||
            (j.OldId is not null && !InstallationFiles.Same(InstallationMetadata.Text(j.OldRegistry, "InstallLocation") ?? "", j.Origin)) ||
            (j.Phase != "Preparing" && !InstallationFiles.Same(InstallationMetadata.Text(j.NewRegistry, "InstallLocation") ?? "", root)) ||
            (j.OldId is null && (j.OldFiles.Count != 0 || j.OldRegistry.Count != 0)) ||
            (!InstallationFiles.Same(root, j.Origin) && (InstallationFiles.Within(root, j.Origin) || InstallationFiles.Within(j.Origin, root))))
            throw new IOException("安装记录身份不一致，未恢复。");
        foreach (var (files, path, release) in new[] { (j.OldFiles, j.Origin, j.OldRelease), (j.NewFiles, root, j.NewRelease) })
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var change in files)
                if (!seen.Add(locations.Resolve(path, release, change)) || change.SHA256.Length != 64 || change.SHA256.Any(c => !char.IsAsciiHexDigit(c)))
                    throw new IOException("安装记录文件清单无效。");
        }
        if (j.Schema == 2 && j.OldStartup != j.NewStartup &&
            (!string.Equals(j.OldStartup, StartupCommand(j.Origin), StringComparison.OrdinalIgnoreCase) || j.NewStartup != StartupCommand(root)))
            throw new IOException("安装记录自启动目标无效。");
        if (j.Schema == 2 && File.Exists(locations.Pending) &&
            (InstallationMetadata.ReadIni(locations.Pending, "Id") != j.Id || !InstallationFiles.Same(InstallationMetadata.ReadIni(locations.Pending, "Path"), root)))
            throw new IOException("安装恢复索引不一致。");
    }

    private void RequireOriginalRegistration(UpgradeJournal j)
    {
        var ids = registry.Ids();
        if (ids.Count != (j.OldId is null ? 0 : 1) || (j.OldId is not null &&
            (ids[0] != j.OldId || !InstallationMetadata.Equal(registry.Read(j.OldId), j.OldRegistry))))
            throw new IOException("安装登记已变化，停止替换。");
    }
    private void RequireCommitted(UpgradeJournal j)
    {
        if (!registry.Ids().SequenceEqual(new[] { j.NewId }) || !InstallationMetadata.Equal(registry.Read(j.NewId), j.NewRegistry))
            throw new IOException("唯一安装登记未完成或已变化。");
        foreach (var f in j.NewFiles) InstallationFiles.RequireHash(locations.Resolve(j.Root, j.NewRelease, f), f.SHA256);
        if (j.Schema == 2 && startup.Read() != j.NewStartup) throw new IOException("自启动设置已变化，保留恢复记录。");
    }
    private void Rollback(string work, UpgradeJournal j)
    {
        var current = registry.Read(j.NewId);
        bool stillOld = j.OldId == j.NewId && InstallationMetadata.Equal(current, j.OldRegistry);
        if (current is not null && !stillOld)
        {
            // An in-place fixed-key write may stop between values. Only the captured
            // old/new values are ours to restore; any unrelated value stops recovery.
            var allowed = j.OldId == j.NewId ? j.NewRegistry.Concat(j.OldRegistry) : j.NewRegistry;
            if (current.Any(v => !allowed.Any(expected => InstallationMetadata.Equal([v], [expected]))))
                throw new IOException("新版登记被其他程序修改，未删除。");
            registry.Remove(j.NewId);
        }
        var untouched = j.OldFiles.Select((file, index) => (file, index))
            .Where(pair => !File.Exists(OldSlot(j, pair.index)))
            .Select(pair => locations.Resolve(j.Origin, j.OldRelease, pair.file)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = j.NewFiles.Count - 1; i >= 0; i--)
        {
            string slot = Slot(work, "new", i), target = locations.Resolve(j.Root, j.NewRelease, j.NewFiles[i]);
            if (!File.Exists(slot)) Move(target, slot, j.NewFiles[i].SHA256, recovery: true);
            else if (File.Exists(target) && !untouched.Contains(target))
            { InstallationFiles.RequireHash(target, j.NewFiles[i].SHA256); File.Delete(target); }
        }
        for (int i = 0; i < j.OldFiles.Count; i++)
        {
            string target = locations.Resolve(j.Origin, j.OldRelease, j.OldFiles[i]);
            if (File.Exists(OldSlot(j, i))) Move(OldSlot(j, i), target, j.OldFiles[i].SHA256, recovery: true);
            else InstallationFiles.RequireHash(target, j.OldFiles[i].SHA256);
        }
        if (j.Schema == 2 && j.OldStartup != j.NewStartup)
        {
            string? value = startup.Read();
            if (value == j.NewStartup) startup.Replace(j.NewStartup, j.OldStartup);
            else if (value != j.OldStartup) throw new IOException("自启动设置变化，未强行恢复。");
        }
        if (j.OldId is not null)
        {
            var old = registry.Read(j.OldId);
            if (old is not null && !InstallationMetadata.Equal(old, j.OldRegistry)) throw new IOException("旧登记变化，未覆盖。");
            if (old is null) registry.Write(j.OldId, j.OldRegistry);
        }
        RequireOriginalRegistration(j);
        j.Phase = "RolledBack"; Save(work, j); Cleanup(work, j);
    }

    private static string StartupCommand(string root) => $"\"{Path.Combine(root, "DesktopUp.exe")}\" --autostart";
    private static string Work(string root, string id) => Path.Combine(root, InstallationFiles.WorkPrefix + id);
    private static string OldSlot(UpgradeJournal j, int i) => Slot(Work(j.Origin, j.Id), "old", i);
    private static string Slot(string work, string area, int index) => Path.Combine(work, area, index.ToString("D6"));
    private static void Move(string source, string destination, string hash, bool recovery = false)
    {
        InstallationFiles.RequireHash(source, hash); InstallationFiles.Plain(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (recovery && File.Exists(destination)) { InstallationFiles.RequireHash(destination, hash); File.Delete(source); }
        else File.Move(source, destination, overwrite: false);
        InstallationFiles.RequireHash(destination, hash);
    }
    private static void RequireAvailable(string path, HashSet<string> old)
    {
        if (Directory.Exists(path) || (File.Exists(path) && !old.Contains(path))) throw new IOException("程序路径与未归属文件冲突，未覆盖：" + path);
    }
    private void AddOwnedLink(List<FileChange> files, string root, string release, string area, string name, string target)
    {
        var link = new FileChange(area, name, ""); string path = locations.Resolve(root, release, link);
        if (InstallationFiles.Same(links.Target(path) ?? "", target)) files.Add(link with { SHA256 = InstallationFiles.Digest(path) });
    }
    private void PrepareLink(List<FileChange> files, string work, string root, string release, string area, string name, string target, HashSet<string> owned)
    {
        var change = new FileChange(area, name, ""); RequireAvailable(locations.Resolve(root, release, change), owned);
        string temporary = Slot(work, "new", files.Count) + ".lnk";
        links.Create(temporary, target); File.Move(temporary, Slot(work, "new", files.Count));
        files.Add(change with { SHA256 = InstallationFiles.Digest(Slot(work, "new", files.Count)) });
    }
    private static void Save(string work, UpgradeJournal journal)
    {
        string next = Path.Combine(work, "journal.next");
        using (var stream = new FileStream(next, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, journal, JsonOptions); stream.Flush(flushToDisk: true); }
        File.Move(next, Path.Combine(work, "journal.json"), overwrite: true);
    }
    private void WritePending(UpgradeJournal j)
    {
        InstallationFiles.Plain(locations.Pending);
        using var file = new FileStream(locations.Pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] data = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes($"[Install]\r\nPath={j.Root}\r\nId={j.Id}\r\n")).ToArray();
        file.Write(data); file.Flush(true);
    }
    private void RemovePending(UpgradeJournal j)
    {
        if (j.Schema == 1 || !File.Exists(locations.Pending)) return;
        if (InstallationMetadata.ReadIni(locations.Pending, "Id") != j.Id || !InstallationFiles.Same(InstallationMetadata.ReadIni(locations.Pending, "Path"), j.Root))
            throw new IOException("安装恢复索引已变化，未删除。");
        File.Delete(locations.Pending);
    }
    private void Cleanup(string work, UpgradeJournal j)
    {
        foreach (var (files, area, basePath) in new[] { (j.OldFiles, "old", Work(j.Origin, j.Id)), (j.NewFiles, "new", work) })
        {
            for (int i = 0; i < files.Count; i++)
            {
                string path = Slot(basePath, area, i);
                if (File.Exists(path)) { InstallationFiles.RequireHash(path, files[i].SHA256); File.Delete(path); }
            }
            if (Directory.Exists(Path.Combine(basePath, area))) Directory.Delete(Path.Combine(basePath, area));
        }
        if (!InstallationFiles.Same(j.Root, j.Origin) && Directory.Exists(Work(j.Origin, j.Id))) Directory.Delete(Work(j.Origin, j.Id));
        RemovePending(j);
        File.Delete(Path.Combine(work, "journal.next")); File.Delete(Path.Combine(work, "journal.json")); Directory.Delete(work);
    }
    private void CleanPreparing(string work, UpgradeJournal j)
    {
        string staged = Path.Combine(work, "new");
        if (Directory.Exists(staged))
            foreach (string path in Directory.EnumerateFiles(staged))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), @"^\d{6}(\.lnk)?$")) throw new IOException("准备目录含未知文件，保留现场。");
                InstallationFiles.Plain(path); File.Delete(path);
            }
        if (Directory.Exists(staged)) Directory.Delete(staged);
        string backup = Path.Combine(Work(j.Origin, j.Id), "old");
        if (Directory.Exists(backup)) Directory.Delete(backup);
        if (!InstallationFiles.Same(j.Root, j.Origin) && Directory.Exists(Work(j.Origin, j.Id))) Directory.Delete(Work(j.Origin, j.Id));
        RemovePending(j);
        File.Delete(Path.Combine(work, "journal.next")); File.Delete(Path.Combine(work, "journal.json")); Directory.Delete(work);
    }
    private void RemoveEmptyOldDirectories(UpgradeJournal j)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in j.OldFiles)
        {
            string? parent = Path.GetDirectoryName(locations.Resolve(j.Origin, j.OldRelease, file));
            if (file.Area == "desktop") continue;
            if (file.Area == "menu") { directories.Add(parent!); continue; }
            while (parent is not null && InstallationFiles.Within(parent, j.Origin) && !InstallationFiles.Same(parent, j.Origin))
            { directories.Add(parent); parent = Path.GetDirectoryName(parent); }
        }
        foreach (string directory in directories.OrderByDescending(d => d.Length))
        {
            InstallationFiles.Plain(directory);
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
        if (!InstallationFiles.Same(j.Origin, j.Root) && Directory.Exists(j.Origin) && !Directory.EnumerateFileSystemEntries(j.Origin).Any()) Directory.Delete(j.Origin);
    }
}

internal sealed class PendingUpgradeException(string message) : IOException(message);
