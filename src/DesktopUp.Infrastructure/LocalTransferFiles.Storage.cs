using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace DesktopUp.Infrastructure;

public static partial class LocalTransferFiles
{
    internal sealed record CopyMetadata(uint Attributes,long Created,long Modified);
    internal sealed class PinnedObject : IDisposable
    {
        private readonly IDisposable parents;
        internal SafeFileHandle Handle {get;}
        internal PinnedObject(string path,bool delete=false)
        {
            path=Normalize(path);parents=LockParents(path);
            try{Handle=Open(path,delete,pin:true);}catch{parents.Dispose();throw;}
        }
        internal LocalTransferObject Snapshot(string relative="")=>SnapshotHandle(Handle,relative);
        internal CopyMetadata Metadata=>MetadataOf(Handle);
        internal void Delete()
        {if(!SetFileInformationByHandle(Handle,4,[1],1))ThrowWin32();}
        public void Dispose(){Handle.Dispose();parents.Dispose();}
    }
    private static CopyMetadata MetadataOf(SafeFileHandle h)
    {var i=Information(h);return new(i.Attributes,((long)i.CreationHigh<<32)|i.CreationLow,((long)i.WriteHigh<<32)|i.WriteLow);}
    private static LocalTransferObject SnapshotHandle(SafeFileHandle h,string relative)
    {
        var i=Information(h);bool directory=(i.Attributes&0x10)!=0;long length=directory?0:((long)i.SizeHigh<<32)|i.SizeLow;
        if(length>MaximumFileBytes)throw new IOException("File budget exceeded.");
        string hash="";
        if(!directory)
        {
            using var sha=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[128*1024];long offset=0;
            while(offset<length){int n=RandomAccess.Read(h,buffer.AsSpan(0,(int)Math.Min(buffer.Length,length-offset)),offset);if(n==0)throw new IOException("File shortened.");sha.AppendData(buffer.AsSpan(0,n));offset+=n;}
            if(RandomAccess.GetLength(h)!=length)throw new IOException("File changed.");hash=Convert.ToHexString(sha.GetHashAndReset());
        }
        return new(relative,Id(h),length,hash,directory);
    }
    internal static void RequireCopyable(string path)
    {
        using var h=Open(path,false,pin:true);var info=Information(h);
        // Do not silently flatten hardlinks, ADS, sparse/compressed/encrypted/cloud files or
        // clear read-only permissions. This first adapter has a deliberately narrow matrix.
        if((info.Attributes&~(0x10u|0x20u|0x80u|0x2u|0x4u))!=0||((info.Attributes&0x10)==0&&info.Links!=1))
            throw new NotSupportedException("Storage copy requires ordinary writable local files/directories without special attributes or hardlinks.");
        nint streams=FindFirstStream(path,0,out var stream,0);
        if(streams==-1){if(Marshal.GetLastWin32Error()==38)return;ThrowWin32();}
        try
        {
            do{if(stream.Name!="::$DATA")throw new NotSupportedException("Alternate streams are not enabled for storage relocation.");}while(FindNextStream(streams,out stream));
            if(Marshal.GetLastWin32Error()!=38)ThrowWin32();
        }
        finally{FindClose(streams);}
    }
    internal static void CopyKeepingSource(string source,string target,LocalTransferObject expected,CopyMetadata metadata,
        Action<LocalTransferObject> created,Action? progress,CancellationToken token)
    {
        using var input=new PinnedObject(source);RequireCopyable(source);
        if(input.Snapshot(expected.RelativePath)!=expected||input.Metadata!=metadata)throw new IOException("Source changed before copy.");
        using var parents=LockParents(Normalize(target));token.ThrowIfCancellationRequested();
        using var output=new FileStream(target,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read,128*1024,FileOptions.WriteThrough);
        created(SnapshotHandle(output.SafeFileHandle,expected.RelativePath)); // Durable identity BEFORE writing payload.
        byte[] buffer=new byte[128*1024];long offset=0;
        while(offset<expected.Length)
        {
            token.ThrowIfCancellationRequested();int n=RandomAccess.Read(input.Handle,buffer.AsSpan(0,(int)Math.Min(buffer.Length,expected.Length-offset)),offset);
            if(n==0)throw new IOException("Source shortened during copy.");output.Write(buffer,0,n);offset+=n;output.Flush(true);progress?.Invoke();
        }
        token.ThrowIfCancellationRequested();
        // Handle-based metadata update cannot race a path replacement; ACLs inherit destination.
        byte[] basic=new byte[40];BitConverter.GetBytes(metadata.Created).CopyTo(basic,0);
        BitConverter.GetBytes(metadata.Modified).CopyTo(basic,16);BitConverter.GetBytes(metadata.Attributes).CopyTo(basic,32);
        if(!SetFileInformationByHandle(output.SafeFileHandle,0,basic,basic.Length))ThrowWin32();
        output.Flush(true);
        var observed=SnapshotHandle(output.SafeFileHandle,expected.RelativePath);
        if(observed.Length!=expected.Length||observed.Sha256!=expected.Sha256)throw new IOException("Copied bytes do not match; source retained.");
    }
    internal static bool IsExactPrefix(PinnedObject complete,PinnedObject partial)
    {
        long length=RandomAccess.GetLength(partial.Handle);if(length>RandomAccess.GetLength(complete.Handle))return false;
        byte[] a=new byte[128*1024],b=new byte[a.Length];long offset=0;
        while(offset<length)
        {
            int size=(int)Math.Min(a.Length,length-offset),na=RandomAccess.Read(complete.Handle,a.AsSpan(0,size),offset),nb=RandomAccess.Read(partial.Handle,b.AsSpan(0,size),offset);
            if(na!=size||nb!=size||!a.AsSpan(0,size).SequenceEqual(b.AsSpan(0,size)))return false;offset+=size;
        }
        return true;
    }
    internal static LocalTransferObject CreateOwnedDirectory(string path)
    {
        using var parents=LockParents(Normalize(path));
        if(!StorageCreateDirectory(path,0))ThrowWin32();
        using var pin=new PinnedObject(path);if(Directory.EnumerateFileSystemEntries(path).Any())throw new IOException("New directory is not empty.");return pin.Snapshot();
    }
    [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool StorageCreateDirectory(string path,nint security);
}
