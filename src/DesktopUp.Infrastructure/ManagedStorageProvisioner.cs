using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public sealed record StorageProvisionIntent(int FormatVersion,string ProfileIdentity,ManagedStorageRoot Root,Guid CollectionId);
public sealed class StorageProvisionPreview
{
    internal StorageProvisionPreview(StorageProvisionIntent intent,long contentVersion)
    { Intent=intent;ContentVersion=contentVersion;Fingerprint=StateRequestFingerprint.Compute(new {intent,contentVersion}); }
    public StorageProvisionIntent Intent { get; }
    public long ContentVersion { get; }
    public string Fingerprint { get; }
    public string CollectionPath => Path.Combine(Intent.Root.Locator,"collection-"+Intent.CollectionId.ToString("N"));
}
public enum StorageProvisionCheckpoint { IntentWritten,RootClaimed,DirectoryCreated,ChildClaimed,Registered }

// Root metadata is exclusive to a physical profile directory. Copying a DB/profile does
// not grant ownership of the old content root. No directory or unknown marker is deleted.
public sealed partial class ManagedStorageProvisioner(SqliteStateStore store)
{
    private const string RootMarker=".desktopup-storage-owner.json",ChildMarker=".desktopup-collection-owner.json";
    private sealed record ChildClaim(StorageProvisionIntent Intent,string Identity);
    private static readonly JsonSerializerOptions Json=new()
    { UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,MaxDepth=12 };
    public StorageProvisionPreview Preview(string selectedDirectory)
    {
        string root=LocalTransferFiles.Normalize(selectedDirectory);
        if(store.ProtectedTransferPath(root,true))throw new IOException("请选择安装、系统、缓存和临时目录之外的持久文件夹。");
        using var lease=LocalTransferFiles.LockDirectory(root);
        RejectAdoptedRoot(root);
        string identity=LocalTransferFiles.Identity(root),profile=store.TransferProfileIdentity;
        string file=IntentFile(root);
        var intent=File.Exists(file)?Read<StorageProvisionIntent>(file):new(1,profile,
            new(Guid.NewGuid(),root,identity,1,ManagedRootLocation.UserPersistent,true,true),Guid.NewGuid());
        Validate(intent,root,identity,profile);
        EnsureNotStopped(intent);
        string marker=Path.Combine(root,RootMarker);
        if(LocalTransferFiles.Exists(marker) && Read<StorageProvisionIntent>(marker)!=intent)
            throw new IOException("此目录属于其他配置或未知收纳记录，不能接管。请另选专用目录。");
        return new(intent,new SqliteStateCommandRepository(store).Read().ContentVersion);
    }
    public IReadOnlyList<StorageProvisionIntent> Pending()
    {
        if(!Directory.Exists(store.StorageProvisionDirectory))return [];
        var files=Directory.EnumerateFiles(store.StorageProvisionDirectory,"*.json").Take(257).ToArray();
        if(files.Length>256)throw new IOException("目录创建记录超出预算，保留原记录。");
        var stopped=Stopped().Select(x=>x.Intent.CollectionId).ToHashSet();
        return files.Select(Read<StorageProvisionIntent>).Where(x=>!stopped.Contains(x.CollectionId)).ToArray();
    }
    public RegisteredManagedStorage Confirm(StorageProvisionPreview preview,string confirmedFingerprint,
        Action<StorageProvisionCheckpoint>? checkpoint=null)
    {
        if(preview.Fingerprint!=confirmedFingerprint)throw new IOException("目录预览尚未确认。");
        var journal=new SqliteTransferJournal(store);
        using var executor=journal.AcquireExecutor();
        var intent=preview.Intent;
        EnsureNotStopped(intent);
        using var rootLease=LocalTransferFiles.LockDirectory(intent.Root.Locator);
        RejectAdoptedRoot(intent.Root.Locator);
        Validate(intent,intent.Root.Locator,LocalTransferFiles.Identity(intent.Root.Locator),store.TransferProfileIdentity);
        if(new SqliteStateCommandRepository(store).Read().ContentVersion!=preview.ContentVersion)
            throw new IOException("内容配置已变化，请重新确认目录。");
        if(store.ProtectedTransferPath(intent.Root.Locator,true))throw new IOException("不安全的持久目录。");
        // Persist the creation intent BEFORE claiming/creating anything outside the profile.
        store.PreserveTransferGuard();
        Directory.CreateDirectory(store.StorageProvisionDirectory);
        if(!File.Exists(IntentFile(intent.Root.Locator)) && Directory.EnumerateFiles(store.StorageProvisionDirectory,"*.json").Take(256).Count()>=256)
            throw new IOException("目录创建记录达到预算；保留现有记录，不再新增。");
        WriteOrMatch(IntentFile(intent.Root.Locator),intent);
        checkpoint?.Invoke(StorageProvisionCheckpoint.IntentWritten);
        WriteOrMatch(Path.Combine(intent.Root.Locator,RootMarker),intent);
        using var owner=ReadLease(Path.Combine(intent.Root.Locator,RootMarker));
        checkpoint?.Invoke(StorageProvisionCheckpoint.RootClaimed);
        string child=preview.CollectionPath;
        bool created=CreateDirectory(child,0);
        if(!created && Marshal.GetLastWin32Error()!=183)throw new IOException("无法建立专用收纳目录："+Marshal.GetLastWin32Error());
        using var childLease=LocalTransferFiles.LockDirectory(child);
        string childId=LocalTransferFiles.Identity(child),marker=Path.Combine(child,ChildMarker);
        checkpoint?.Invoke(StorageProvisionCheckpoint.DirectoryCreated);
        var claim=new ChildClaim(intent,childId);
        if(!created && !LocalTransferFiles.Exists(marker))
            throw new IOException("目录已存在但缺少创建完成凭证，保留现场。请另选目录，不能自动认领未知内容。");
        WriteOrMatch(marker,claim);
        using var childOwner=ReadLease(marker);
        checkpoint?.Invoke(StorageProvisionCheckpoint.ChildClaimed);
        var collection=new Collection(intent.CollectionId,"受管收纳",ContentCollectionKind.Managed,child,ContentStorageOwnership.Managed);
        journal.Register(intent.Root,collection,new(new Dictionary<string,long>(),ContentVersion:preview.ContentVersion));
        checkpoint?.Invoke(StorageProvisionCheckpoint.Registered);
        return new(intent.Root,collection);
    }
    private static void Validate(StorageProvisionIntent i,string root,string identity,string profile)
    {
        if(i.FormatVersion!=1 || i.ProfileIdentity!=profile || i.Root.Locator!=root || i.Root.ObjectIdentity!=identity ||
            i.CollectionId==Guid.Empty || ManagedStoragePolicy.Evaluate(i.Root)!=ManagedStorageDecision.Ready)
            throw new IOException("目录/配置身份或记录版本变化，保留现有记录；不自动迁移或接管。");
    }
    internal static void ValidateRegistration(SqliteStateStore store,ManagedStorageRoot root,Collection collection)
    {
        if(!LocalTransferFiles.Exists(Path.Combine(root.Locator,RootMarker)))
        {ManagedStorageAdoption.ValidateActive(store,root,collection);return;}
        RejectAdoptedRoot(root.Locator);
        var intent=Read<StorageProvisionIntent>(Path.Combine(root.Locator,RootMarker));
        Validate(intent,root.Locator,LocalTransferFiles.Identity(root.Locator),store.TransferProfileIdentity);
        if(intent.Root!=root || intent.CollectionId!=collection.Id || collection.StorageLocator!=Path.Combine(root.Locator,"collection-"+collection.Id.ToString("N")))
            throw new IOException("收纳集合与目录所有权记录不一致。");
        var claim=Read<ChildClaim>(Path.Combine(collection.StorageLocator,ChildMarker));
        if(claim.Intent!=intent || claim.Identity!=LocalTransferFiles.Identity(collection.StorageLocator))
            throw new IOException("收纳子目录已变化，不能自动接管。");
    }
    private static void RejectAdoptedRoot(string root)
    {
        if(LocalTransferFiles.Exists(Path.Combine(root,ManagedStorageAdoption.RootMarker)))
            throw new IOException("此目录已有显式认领记录；不能再作为新目录创建另一套所有权声明。");
    }
    private string IntentFile(string root)=>Path.Combine(store.StorageProvisionDirectory,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant())))+".json");
    private static FileStream ReadLease(string path)
    { _=LocalTransferFiles.Identity(path);return new(path,FileMode.Open,FileAccess.Read,FileShare.Read); }
    private static T Read<T>(string path)
    {
        using var stream=ReadLease(path);
        if(stream.Length>32768)throw new IOException("目录所有权记录超限。");
        return JsonSerializer.Deserialize<T>(stream,Json)??throw new IOException("空目录所有权记录。");
    }
    private static void WriteOrMatch<T>(string path,T data)
    {
        if(LocalTransferFiles.Exists(path))
        {
            if(!EqualityComparer<T>.Default.Equals(Read<T>(path),data))throw new IOException("目录记录冲突；原文保留。");
            return;
        }
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(data,Json);
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);
        stream.Write(bytes);stream.Flush(true);
    }
    [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CreateDirectory(string path,nint attributes);
}
