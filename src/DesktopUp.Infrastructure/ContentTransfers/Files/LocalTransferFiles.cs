using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Deliberately narrow local provider. No Shell extensions, recursive deletion, overwrite,
// cloud hydration, reparse traversal, network paths or directory cross-volume copy.
public sealed record LocalTransferObject(string RelativePath, string Identity, long Length, string Sha256, bool Directory);
public sealed record LocalTransferFilePlan(string Source, string Destination, string ParentIdentity,
    IReadOnlyList<LocalTransferObject> Objects, bool SameVolume);
public sealed record LocalTransferFileReceipt(string DestinationIdentity, string TreeHash)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LocalTransferObject>? OwnedCopies { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CleanupFinished { get; init; }
}
public enum LocalTransferCheckpoint { BeforeMove, AfterCopy, CopyRecorded, AfterMove, CopyProgress }
public static partial class LocalTransferFiles
{
    public const int MaximumTreeEntries = 4096;
    public const long MaximumFileBytes = 256L * 1024 * 1024;
    public const long MaximumTreeBytes = 1024L * 1024 * 1024;

    public static string Normalize(string path)
    {
        if (!LocalContentCompatibility.TryNormalize(path, false, out string value, out string reason))
            throw new IOException(reason);
        var drive = new DriveInfo(Path.GetPathRoot(value)!);
        if (drive.DriveType != DriveType.Fixed) throw new IOException("First executor supports fixed local volumes only.");
        return value;
    }
    public static IReadOnlyList<string> Ancestors(string path)
    {
        var result = new List<string>();
        for (var parent = Path.GetDirectoryName(Normalize(path)); parent is { Length: > 3 }; parent = Path.GetDirectoryName(parent))
            result.Add(Identity(parent));
        return result;
    }
    public static bool Contains(string parent, string child) =>
        child.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    public static string Identity(string path)
    {
        path = Normalize(path);
        using var chain = LockParents(path);
        using var handle = Open(path, false);
        return Id(handle);
    }
    internal static IDisposable LockDirectory(string path)
    {
        path=Normalize(path);
        var leases=LockParents(path);
        try
        {
            var handle=Open(path,false,attributesOnly:true); leases.Handles.Add(handle);
            if((Information(handle).Attributes&0x10)==0)throw new IOException("A directory is required.");
            return leases;
        }
        catch { leases.Dispose();throw; }
    }
    public static LocalTransferFilePlan Inspect(string source, string destination, CancellationToken cancellation = default,
        bool allowCrossVolumeTree = false)
    {
        source = Normalize(source); destination = Normalize(destination);
        using var sourceParents = LockParents(source);
        using var targetParents = LockParents(destination);
        using var sourceHandle = Open(source, false);
        using var parentHandle = Open(Path.GetDirectoryName(destination)!, false);
        if (Contains(source, destination)) throw new IOException("A directory cannot contain its own transfer destination.");
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) && Exists(destination))
            throw new IOException("Destination already exists; overwrite is forbidden.");
        var objects = Capture(source, cancellation);
        bool same = Volume(sourceHandle) == Volume(parentHandle);
        if (!same && objects[0].Directory && !allowCrossVolumeTree) throw new NotSupportedException("Cross-volume folders require the tree-copy executor.");
        if (!same)
            foreach(var obj in objects) RequireCopyable(Path.Combine(source,obj.RelativePath));
        return new(source, destination, Id(parentHandle), objects, same);
    }

    // Identity-changing copy is durable before source disposal. A failed/cancelled copy is
    // retained for recovery inspection; we never recursively clean an uncertain target.
    internal static LocalTransferFileReceipt Move(LocalTransferFilePlan plan,
        Action<LocalTransferFileReceipt> recordCopy, Action<LocalTransferCheckpoint>? checkpoint = null,
        CancellationToken cancellation = default)
    {
        using var sourceParents = LockParents(plan.Source);
        using var targetParents = LockParents(plan.Destination);
        using var parent = Open(Path.GetDirectoryName(plan.Destination)!, false, attributesOnly: true);
        using var source = Open(plan.Source, true);
        if (Id(parent) != plan.ParentIdentity || Id(source) != plan.Objects[0].Identity ||
            !Equivalent(plan.Objects, Capture(plan.Source, cancellation), true))
            throw new IOException("Source or destination changed; preview again.");
        if (string.Equals(plan.Source, plan.Destination, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Same-path operations must not invoke the file executor.");
        if (Exists(plan.Destination)) throw new IOException("Destination is no longer vacant.");
        cancellation.ThrowIfCancellationRequested();
        checkpoint?.Invoke(LocalTransferCheckpoint.BeforeMove);
        cancellation.ThrowIfCancellationRequested();
        if (plan.SameVolume)
        {
            Rename(source, plan.Destination); // Handle-based; replacement disabled.
            checkpoint?.Invoke(LocalTransferCheckpoint.AfterMove);
            var observed = Capture(plan.Destination, cancellation);
            if (!Equivalent(plan.Objects, observed, true)) throw new IOException("Moved tree changed; retain and inspect.");
            return new(observed[0].Identity, TreeHash(observed));
        }
        if (plan.Objects[0].Directory) throw new NotSupportedException("Directory cross-volume copy is not enabled.");
        RejectExtraStreams(plan.Source);
        // CopyFileEx preserves Windows file streams/attributes. Source handle denies writes and
        // deletion; target/ancestor leases below prevent replacement during verification.
        Exception? progressError=null;
        CopyProgressRoutine progress=(size,transferred,streamSize,streamTransferred,number,reason,sourceHandle,targetHandle,data)=>
        {
            // Ask for STOP, not CANCEL/pbCancel. Recovery still inspects the actual outcome:
            // a failed native copy may leave no destination or an incomplete one.
            // Never let a managed exception unwind through the native callback boundary.
            try
            {
                if(transferred>0&&transferred<size)checkpoint?.Invoke(LocalTransferCheckpoint.CopyProgress);
                return cancellation.IsCancellationRequested ? 2u : 0u;
            }
            catch(Exception e){progressError=e;return 2;}
        };
        bool copiedOk=CopyFileEx(plan.Source,plan.Destination,progress,0,0,1); // COPY_FILE_FAIL_IF_EXISTS
        int copyError=Marshal.GetLastWin32Error();
        GC.KeepAlive(progress);
        if(progressError is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(progressError).Throw();
        if(!copiedOk)
        {
            if(cancellation.IsCancellationRequested)throw new OperationCanceledException("Copy stopped: Win32 "+copyError,cancellation);
            throw new IOException("Win32 "+copyError+": "+new Win32Exception(copyError).Message);
        }
        checkpoint?.Invoke(LocalTransferCheckpoint.AfterCopy);
        if (!FlushFileBuffersForPath(plan.Destination)) ThrowWin32();
        using var target = Open(plan.Destination, false, pin: true);
        var copied = Capture(plan.Destination, cancellation);
        if (!Equivalent(plan.Objects, copied, false)) throw new IOException("Copy verification failed; source retained.");
        var receipt = new LocalTransferFileReceipt(copied[0].Identity, TreeHash(copied));
        recordCopy(receipt); // If journal persistence fails, the source is never deleted.
        checkpoint?.Invoke(LocalTransferCheckpoint.CopyRecorded);
        cancellation.ThrowIfCancellationRequested();
        byte[] disposition = [1];
        if (!SetFileInformationByHandle(source, 4, disposition, 1)) ThrowWin32();
        source.Dispose();
        checkpoint?.Invoke(LocalTransferCheckpoint.AfterMove);
        return receipt;
    }

    public static bool VerifyDestination(LocalTransferFilePlan plan, LocalTransferFileReceipt? receipt)
    {
        using var parents = LockParents(plan.Destination);
        if (!Exists(plan.Destination)) return false;
        var observed = Capture(plan.Destination, CancellationToken.None);
        var identity = receipt?.DestinationIdentity ?? (plan.SameVolume ? plan.Objects[0].Identity : null);
        return identity is not null && observed[0].Identity == identity &&
            Equivalent(plan.Objects, observed, plan.SameVolume) &&
            (receipt is null || TreeHash(observed) == receipt.TreeHash);
    }
    public static bool SourceIsOriginal(LocalTransferFilePlan plan)
    {
        using var parents = LockParents(plan.Source);
        return Exists(plan.Source) && Equivalent(plan.Objects, Capture(plan.Source, CancellationToken.None), true);
    }
    internal static IDisposable LockVerifiedDestination(LocalTransferFilePlan plan, LocalTransferFileReceipt? receipt)
    {
        var leases=LockParents(plan.Destination);
        try
        {
            foreach(var item in plan.Objects)
                leases.Handles.Add(Open(Path.Combine(plan.Destination,item.RelativePath),false,pin:true));
            if(!VerifyDestination(plan,receipt))throw new IOException("Destination changed before index commit.");
            return leases;
        }
        catch { leases.Dispose();throw; }
    }
    public static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        // Access denial must not be reported as absence.
    }
    public static string TreeHash(IEnumerable<LocalTransferObject> objects) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", objects.Select(o => $"{o.RelativePath.Length}:{o.RelativePath}:{o.Length}:{o.Sha256}:{o.Directory}")))));

    private static IReadOnlyList<LocalTransferObject> Capture(string path, CancellationToken cancellation)
    {
        var result = new List<LocalTransferObject>(); long total = 0;
        void Visit(string current, string relative)
        {
            cancellation.ThrowIfCancellationRequested();
            if (result.Count >= MaximumTreeEntries) throw new IOException("Transfer tree exceeds entry budget.");
            using var h = Open(current, false);
            var info = Information(h);
            bool directory = (info.Attributes & 0x10) != 0;
            long length = directory ? 0 : ((long)info.SizeHigh << 32) | info.SizeLow;
            if (length > MaximumFileBytes || (total += length) > MaximumTreeBytes) throw new IOException("Transfer exceeds byte budget.");
            string hash = "";
            if (!directory)
            {
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[128 * 1024]; long offset = 0;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int read = RandomAccess.Read(h, buffer, offset);
                    if (read == 0) break;
                    offset += read; sha.AppendData(buffer.AsSpan(0, read));
                    if (offset > MaximumFileBytes) throw new IOException("File grew beyond transfer budget.");
                }
                if (offset != length) throw new IOException("File changed during inspection.");
                hash = Convert.ToHexString(sha.GetHashAndReset());
            }
            result.Add(new(relative, Id(h), length, hash, directory));
            if (directory)
                foreach (var child in Directory.EnumerateFileSystemEntries(current).Order(StringComparer.OrdinalIgnoreCase))
                    Visit(child, Path.GetRelativePath(path, child));
        }
        Visit(path, "");
        return result.AsReadOnly();
    }
    private static bool Equivalent(IReadOnlyList<LocalTransferObject> a, IReadOnlyList<LocalTransferObject> b, bool identity) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.RelativePath == p.Second.RelativePath &&
            p.First.Directory == p.Second.Directory && p.First.Length == p.Second.Length &&
            p.First.Sha256 == p.Second.Sha256 && (!identity || p.First.Identity == p.Second.Identity));

    private sealed class HandleSet : IDisposable
    {
        public List<SafeFileHandle> Handles { get; } = [];
        public void Dispose() { for (int i = Handles.Count - 1; i >= 0; i--) Handles[i].Dispose(); }
    }
    private static HandleSet LockParents(string path)
    {
        var result = new HandleSet();
        try
        {
            string parent = Path.GetPathRoot(path)!;
            foreach (string segment in path[parent.Length..].Split('\\').SkipLast(1))
            { parent = Path.Combine(parent, segment); result.Handles.Add(Open(parent, false, attributesOnly: true)); }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    private static SafeFileHandle Open(string path, bool delete, bool attributesOnly = false, bool pin = false)
    {
        // Reject known remote-data placeholders before requesting data access. Recheck the
        // opened object's attributes as well; this does not relax the existing parent leases.
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Preserve this executor's existing IOException boundary, including missing
            // sources: the new metadata preflight must not change its callers' contract.
            throw new IOException("无法核实来源属性；未请求文件数据或执行移动。", error);
        }
        if (!LocalContentCompatibility.IsMaterialized(attributes))
            throw new IOException("重解析点或未完全落地的内容暂不支持；未读取或下载内容。");
        var handle = CreateFile(path, (attributesOnly ? 0x80u : 0x80000000u) | (delete ? 0x10000u : 0),
            attributesOnly ? 3u : delete || pin ? 1u : 5u, 0, 3, 0x02200000, 0);
        if (handle.IsInvalid) { handle.Dispose(); ThrowWin32(); }
        try
        {
            var info = Information(handle);
            if (delete && (info.Attributes & 1) != 0 && (info.Attributes & 0x10) == 0)
                throw new IOException("Read-only sources require an explicit permission decision; attributes are not cleared.");
            if (!LocalContentCompatibility.IsMaterialized((FileAttributes)info.Attributes) ||
                (info.Attributes & 0x4000u) != 0)
                throw new IOException("Reparse, offline and encrypted objects are not supported.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }
    private static FileInformation Information(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info)) ThrowWin32();
        return info;
    }
    private static byte[] FileId(SafeFileHandle handle)
    {
        byte[] data = new byte[24];
        if (!GetFileInformationByHandleEx(handle, 18, data, data.Length)) ThrowWin32();
        return data;
    }
    private static string Id(SafeFileHandle handle) => Convert.ToHexString(FileId(handle));
    private static ulong Volume(SafeFileHandle handle) => BitConverter.ToUInt64(FileId(handle));
    private static void Rename(SafeFileHandle handle, string destination)
    {
        byte[] name = Encoding.Unicode.GetBytes(destination);
        byte[] data = new byte[20 + name.Length]; // x64 FILE_RENAME_INFO; replace=false, root=NULL.
        BitConverter.GetBytes(name.Length).CopyTo(data, 16); name.CopyTo(data, 20);
        if (!SetFileInformationByHandle(handle, 3, data, data.Length)) ThrowWin32();
    }
    private static void ThrowWin32() => throw new IOException("Win32 "+Marshal.GetLastWin32Error()+": "+new Win32Exception(Marshal.GetLastWin32Error()).Message);
    private static void RejectExtraStreams(string path)
    {
        nint h = FindFirstStream(path, 0, out var stream, 0);
        if (h == -1) ThrowWin32();
        try
        {
            do { if (stream.Name != "::$DATA") throw new NotSupportedException("Cross-volume alternate streams need a separate verification gate."); }
            while (FindNextStream(h, out stream));
            if (Marshal.GetLastWin32Error() != 38) ThrowWin32(); // ERROR_HANDLE_EOF
        }
        finally { FindClose(h); }
    }
    private static bool FlushFileBuffersForPath(string path)
    {
        using var h = CreateFile(path, 0x40000000, 1, 0, 3, 0x00200000, 0);
        return !h.IsInvalid && FlushFileBuffers(h);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name; }
    [DllImport("kernel32.dll", EntryPoint="FindFirstStreamW", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern nint FindFirstStream(string path, int level, out StreamData data, uint flags);
    [DllImport("kernel32.dll", EntryPoint="FindNextStreamW", CharSet=CharSet.Unicode, SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FindNextStream(nint handle, out StreamData data);
    [DllImport("kernel32.dll")] private static extern bool FindClose(nint handle);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool FlushFileBuffers(SafeFileHandle handle);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, byte[] data, int size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, byte[] data, int size);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint CopyProgressRoutine(long total,long transferred,long streamSize,long streamTransferred,uint streamNumber,uint reason,nint source,nint target,nint data);
    [DllImport("kernel32.dll",EntryPoint="CopyFileExW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CopyFileEx(string source,string destination,CopyProgressRoutine progress,nint data,nint cancel,uint flags);
}

