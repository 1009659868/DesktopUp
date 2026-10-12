using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopUp;

public partial class App
{
    private async void RunIncomingProbe(string dataRoot)
    {
        IDisposable? lease=null;
        try
        {
            string repo=AppContext.BaseDirectory;
            while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository fixture required.");
            string root=Path.GetDirectoryName(Path.GetFullPath(dataRoot))!;
            if(!LocalTransferFiles.Contains(Path.Combine(repo,"out","drop-d3"),root)||Path.GetFileName(dataRoot)!="profile"||
                Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT")!=dataRoot||
                !File.Exists(Path.Combine(root,".incoming-fixture"))||File.ReadAllText(Path.Combine(root,".incoming-fixture"))!="owned")
                throw new IOException("Owned isolated ingress fixture required.");
            lease=_ledger!.Acquire("fixture.incoming","production-command");await _transferDiscovery;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!_spatialRuntime!.IncomingProbeReady)
            {if(watch.Elapsed.TotalSeconds>20)throw new IOException("Incoming surface not ready.");await Task.Delay(100);}
            var repository=new SqliteStateCommandRepository(_services!.GetRequiredService<SqliteStateStore>());
            if(_startupIntent.Arguments.Contains("--incoming-restart"))
            {
                var current=repository.Read();
                if(current.ContentCatalog.PosterItems.Count!=4 || current.ContentCatalog.Items.Any(x=>!LocalTransferFiles.Exists(x.Target.Locator)))
                    throw new IOException("Incoming references did not survive restart.");
            }
            else
            {
                if(repository.Read().ContentCatalog.PosterItems.Count!=0)throw new IOException("Fresh empty fixture required.");
                string source=Path.Combine(root,"sources");Directory.CreateDirectory(source);
                string Make(string name){string p=Path.Combine(source,name);using var f=new StreamWriter(new FileStream(p,FileMode.CreateNew));f.Write("D3 self-created test file.");return p;}
                var center=_controlCenter!.WindowForPortableProbe;
                await _spatialRuntime.IncomingProbe([Make("first.txt")],frame:true);
                if(repository.Read().ContentCatalog.PosterItems.Count!=1)throw new IOException("Frame ingress did not add one item.");
                var storage=await Task.Run(()=>_storageLocation!.EnsureReady());
                File.WriteAllText(Path.Combine(storage.Collection.StorageLocator!,"occupied.txt"),"other owner");
                string one=Make("second.txt"),collision=Make("occupied.txt"),two=Make("third.txt");
                await _spatialRuntime.IncomingProbe([one,collision,two]);
                if(repository.Read().ContentCatalog.PosterItems.Count!=3||File.Exists(one)||File.Exists(two)||!File.Exists(collision))
                    throw new IOException("Partial-batch ingress did not preserve failure and publish successes.");
                if(File.ReadAllText(Path.Combine(storage.Collection.StorageLocator!,"occupied.txt"))!="other owner")throw new IOException("Collision overwritten.");
                var external=Directory.CreateTempSubdirectory("DesktopUp-D3-App-");
                string folder=Path.Combine(external.FullName,"folder");Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"child.txt"),"owned cross-volume fixture");
                await _spatialRuntime.IncomingProbe([folder],manual:true);
                if(repository.Read().ContentCatalog.PosterItems.Count!=4||Directory.Exists(folder))throw new IOException("Cross-volume manual ingress failed.");
                if(!ReferenceEquals(center,_controlCenter.WindowForPortableProbe))throw new IOException("Ordinary incoming files opened control center.");
                await _spatialRuntime.IncomingCollapsedProbe();
                // The unique empty temp parent remains as a trace; no user files or broad deletion.
            }
            _log!.Write("info","contract.incoming_passed",new{restart=_startupIntent.Arguments.Contains("--incoming-restart"),items=4,nativeMouse=false});
        }
        catch(Exception e){Environment.ExitCode=1;_log?.Write("error","contract.incoming_failed",new{e.Message,e.StackTrace});}
        finally{lease?.Dispose();ExitApplication();}
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal bool IncomingProbeReady=>TransfersAvailable&&_windows.Values.Any(x=>x.IncomingProbeReady);
    internal async Task IncomingProbe(string[] paths,bool frame=false,bool manual=false)
    {
        var window=_windows.Values.Single();
        if(manual && window.EditContent(new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual}) is {} error)throw new IOException(error);
        var ticket=window.IncomingProbeTicket(paths,frame);
        ReceiveFiles(window,ticket,window.IncomingProbeLanding(ticket));await _fileTransferTask;
    }
    internal async Task IncomingCollapsedProbe()
    {
        var window=_windows.Values.Single();
        foreach(bool collapsed in new[]{true,false})
        {
            if(!state.TryGetCurrent(out var current))throw new IOException("No projection.");
            var target=current!.PostersById[window.PosterId!];var completion=new TaskCompletionSource<StateCommandResult>();
            SetCollapsed(new("ingress-posture-"+Guid.NewGuid(),StateCommandSource.Poster,PosterStateCommandKind.SetCollapsed,
                current.Revision,window.PosterId!,target.Poster with {IsCollapsed=collapsed})
                {ReadSet=StateReadSet.ForPoster(window.PosterId!,target.EntityVersion)},result=>completion.SetResult(result));
            var result=await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if(!result.Succeeded || !window.IncomingProbeReady)throw new IOException("Collapsed incoming surface unavailable: "+result.Error);
            var ticket=window.IncomingProbeTicket([@"Z:\probe-only-not-executed.txt"],frame:true);
            if(ticket.Preview.Location.Area!=IncomingDropArea.Frame)throw new IOException("Collapsed point became a content grid.");
        }
    }
}

internal sealed partial class PosterVisualWindow
{
    internal bool IncomingProbeReady=>CanTransferFiles&&_fileDrop?.IsReady==true;
    internal IncomingDropTicket IncomingProbeTicket(string[] paths,bool frame)
    {
        var surface=IncomingSurface();var v=surface.ContentViewport;
        var point=new PointerScreenPoint(surface.Frame.X+(int)((frame?4:v.X+4)*surface.ScaleX),
            surface.Frame.Y+(int)((frame?4:v.Y+4)*surface.ScaleY));
        var session=new IncomingDropSession();var preview=session.Enter(paths,surface,point,0,2);
        if(preview is null)throw new IOException("Incoming surface refused isolated probe.");
        ShowIncomingPreview(preview);var ticket=session.Drop(paths,surface,point,0,2)!;ShowIncomingPreview(null);return ticket;
    }
    internal IncomingContentPlacement? IncomingProbeLanding(IncomingDropTicket ticket)=>
        IncomingContentPlacement.From(EnsureContentSnapshot(_bounds),_contentViewport!,ticket.Preview.Location);
}
