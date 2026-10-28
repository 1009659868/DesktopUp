using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class NativeFileDropTargetTests
{
    [Theory]
    [InlineData(0u,true,2u)]
    [InlineData(4u,true,2u)]
    [InlineData(8u,true,0u)]
    [InlineData(32u,true,0u)]
    [InlineData(2u,true,0u)]
    [InlineData(0u,false,0u)]
    public void NativeRegistrationAndCallbackLifecycleDoNotAuthorizeSourceCleanup(uint keys,bool enabled,uint expected)
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            nint window=0;
            try
            {
                window=CreateWindowEx(0,"STATIC","Owned drop test",0,0,0,10,10,0,0,0,0);
                Assert.NotEqual(0,window);
                var ledger=new Ledger();var log=new Log();int accepted=0;
                using(var target=new NativeFileDropTarget(window,()=>enabled,p=>{accepted++;Assert.Equal(@"G:\fixture.txt",Assert.Single(p));},ledger,log))
                {
                    var data=new PathsData([@"G:\fixture.txt"]);uint effect=3;
                    Assert.Equal(0,target.DragEnter(data,keys,default,ref effect));Assert.Equal(expected,effect);
                    effect=3;target.DragOver(keys,default,ref effect);Assert.Equal(expected,effect);
                    effect=3;target.Drop(data,keys,default,ref effect);
                    Assert.Equal(0u,effect);Assert.Equal(expected==2?1:0,accepted);
                    effect=3;target.Drop(data,keys,default,ref effect);Assert.Equal(expected==2?1:0,accepted);
                    target.DragLeave();
                }
                Assert.Empty(ledger.ActiveResources);Assert.Empty(log.Errors);
            }
            catch(Exception e){error=e;}
            finally{if(window!=0)DestroyWindow(window);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)),"owned STA did not exit");
        if(error is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    [Fact] public void PayloadBudgetAndUnsupportedFormatAreRefused()
    {
        Assert.Null(NativeFileDropTarget.ReadPaths(new PathsData([],false)));
        Assert.Null(NativeFileDropTarget.ReadPaths(new PathsData(Enumerable.Repeat(@"G:\x",101).ToArray())));
        Assert.Null(NativeFileDropTarget.ReadPaths(new PathsData([new string('x',8193)])));
    }
    private sealed class PathsData(string[] paths,bool supported=true):IDataObject
    {
        public int QueryGetData(ref FORMATETC f)=>supported && f.cfFormat==15?0:unchecked((int)0x80040064);
        public void GetData(ref FORMATETC f,out STGMEDIUM medium)
        {
            byte[] text=Encoding.Unicode.GetBytes(string.Join('\0',paths)+"\0\0");
            nint memory=GlobalAlloc(2,(nuint)(20+text.Length));Assert.NotEqual(0,memory);
            nint p=GlobalLock(memory);Assert.NotEqual(0,p);
            Marshal.Copy(new byte[20],0,p,20);Marshal.WriteInt32(p,0,20);Marshal.WriteInt32(p,16,1);
            Marshal.Copy(text,0,p+20,text.Length);GlobalUnlock(memory);
            medium=new(){tymed=TYMED.TYMED_HGLOBAL,unionmember=memory,pUnkForRelease=null};
        }
        public void GetDataHere(ref FORMATETC f,ref STGMEDIUM m)=>throw new NotSupportedException();
        public int GetCanonicalFormatEtc(ref FORMATETC a,out FORMATETC b){b=a;return 1;}
        public void SetData(ref FORMATETC f,ref STGMEDIUM m,bool release)=>throw new NotSupportedException();
        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)=>throw new NotSupportedException();
        public int DAdvise(ref FORMATETC f,ADVF flags,IAdviseSink sink,out int connection){connection=0;return unchecked((int)0x80040003);}
        public void DUnadvise(int connection){}
        public int EnumDAdvise(out IEnumSTATDATA? data){data=null;return unchecked((int)0x80040003);}
    }
    private sealed class Log:IStructuredLog
    {
        public List<string> Errors {get;}=[];
        public void Write(string level,string name,object? data=null){if(level is "warning" or "error")Errors.Add(name);}
    }
    private sealed class Ledger:IResourceLedger
    {
        private readonly HashSet<string> values=[];
        public IReadOnlyCollection<string> ActiveResources=>values;
        public IDisposable Acquire(string id,string kind){Assert.True(values.Add(id));return new Lease(()=>values.Remove(id));}
        public void WriteSnapshot(){}
        private sealed class Lease(Action end):IDisposable {public void Dispose()=>end();}
    }
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex,string name,string title,uint style,int x,int y,int w,int h,nint parent,nint menu,nint instance,nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
}
