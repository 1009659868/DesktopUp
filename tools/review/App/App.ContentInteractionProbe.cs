using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed record ContentNativeProbeTarget(long InputWindowHandle, string ItemId,
    PointerScreenPoint Start, PointerScreenPoint End, ContentResolvedPlacement Source, int? ExpectedPinnedIndex);
internal sealed record ContentNativeProbeState(ContentLayoutDocument Document, long Revision,
    long OpenIntents, bool HasGesture, bool NativeCapture, bool IsEditing);

internal sealed partial class PosterVisualWindow
{
    internal ContentNativeProbeState ReadNativeContentForProbe() => new(ContentDocument, _stateRevision,
        _contentOpenIntentCount, HasContentGesture, inputRouter.Capture?.Press.Session == inputSession, IsEditing);

    internal ContentNativeProbeTarget NativeContentTargetForProbe(string action)
    {
        if (_monitor is null || _contentLayout.Snapshot is null || _contentLayout.Viewport is null || HasContentGesture ||
            _contentReadOnly || _geometry.IsLocked || _contentLayout.Snapshot.LoopScroll || IsEditing != (action == "resize"))
            throw new InvalidOperationException("Native finite input requires an idle writable poster, with editing enabled only for resize.");
        var tiles=ContentLayoutFlow.ProjectTiles(_contentLayout.Snapshot,_contentLayout.Viewport).Where(t =>
            t.Clip.Width == t.Bounds.Width && t.Clip.Height == t.Bounds.Height).OrderBy(t=>t.ReadOrder).ToArray();
        if (tiles.Length < 2) throw new InvalidOperationException("Two complete visible cards are required.");
        var tile=tiles[action == "context" ? 1 : 0];
        if (action == "context" && SelectContentItem(tiles[0].ItemId) is { } selectionError)
            throw new InvalidOperationException(selectionError);
        if (action == "resize" && SelectContentItem(tile.ItemId) is { } error) throw new InvalidOperationException(error);
        var surface=inputRouter.ReadSurface(inputSession) ?? throw new InvalidOperationException("No native input surface.");
        var kind=action=="resize"?PosterInputTargetKind.ContentSpanHandle:PosterInputTargetKind.ContentItem;
        var region=surface.Layout.Regions.First(r=>r.Kind==kind && r.ContentItemId==tile.ItemId);
        var start=new PointerScreenPoint(surface.Frame.X+region.Bounds.X+region.Bounds.Width/2,
            surface.Frame.Y+region.Bounds.Y+region.Bounds.Height/2);
        var end=start;
        if(action is "auto-drag" or "cancel-drag")
        {
            var destination=tiles[1];
            end=new(start.X+(int)Math.Round((destination.Bounds.X-tile.Bounds.X)*_monitor.ScaleX),
                start.Y+(int)Math.Round((destination.Bounds.Y-tile.Bounds.Y)*_monitor.ScaleY));
        }
        else if(action=="manual-drag") end=start with {Y=start.Y+(int)Math.Round((_contentLayout.Snapshot.CellDip+_contentLayout.Snapshot.GapDip)*_monitor.ScaleY)};
        // Stay inside this poster's frame but outside its content viewport. A frame-relative
        // offscreen destination could make the driver reject before exercising final release.
        else if(action=="invalid-drag") end=start with {Y=_bounds.Y+(int)Math.Round(ContentTopDip*_monitor.ScaleY/2)};
        else if(action=="resize") end=start with {X=start.X-(int)Math.Round((_contentLayout.Snapshot.CellDip+_contentLayout.Snapshot.GapDip)*_monitor.ScaleX)};
        return new(_inputIsland!.WindowHandle.ToInt64(),tile.ItemId,start,end,
            _contentLayout.Snapshot.Items.Single(p=>p.ItemId==tile.ItemId),
            action=="auto-drag"?tiles[1].ReadOrder+1:null);
    }

    internal object CompleteNativeContentMenuForProbe(string expectedItem)
    {
        var probe = _menuInteraction?.ReadProbe();
        if (_selectedContentItem != expectedItem || _posterMenu is not { IsOpen: true } menu ||
            menu.Items.FirstOrDefault() is not MenuFlyoutItem { Text: "打开（演示）" } ||
            probe is not { Ordered: true, InputsTransparent: true })
            throw new InvalidOperationException("The native right-click did not open the current Item menu with safe popup ordering.");
        var result = new { SelectedItem = _selectedContentItem, MenuEntries = menu.Items.OfType<MenuFlyoutItem>().Select(i => i.Text).ToArray(),
            NativePopup = probe, ActualRightClick = true, NativeMenuActionClick = false };
        menu.Hide();
        return result;
    }
}

internal sealed partial class PosterSpatialRuntime
{
    internal ContentNativeProbeState ReadNativeContentForProbe(string id)=>_windows[id].ReadNativeContentForProbe();
    internal ContentNativeProbeTarget NativeContentTargetForProbe(string id,string action)=>_windows[id].NativeContentTargetForProbe(action);
    internal object CompleteNativeContentMenuForProbe(string id, string item) => _windows[id].CompleteNativeContentMenuForProbe(item);
}

public partial class App
{
    private void RunContentInteractionProbe(string dataRoot, string mode)
    {
        var timer=_dispatcher!.CreateTimer(); timer.Interval=TimeSpan.FromMilliseconds(100);
        var lease=_ledger!.Acquire("fixture.content-interaction","isolated-direct-input");
        var observations=new List<object>();
        string[] actions=mode=="native-mouse"
            ? ["click","double-click","auto-drag","invalid-drag","manual-drag","resize","context"]
            : ["click","double-click","auto-drag","cancel-drag","invalid-drag","manual-drag","resize","context"];
        string target="", expectedPath=Path.Combine(dataRoot,"content-interaction-expected.json");
        long deadline=_autoClock.ElapsedMilliseconds+90000, readyAfter=0;
        int index=0; bool stopped=false,ticking=false,initialized=false;
        ContentNativeProbeState? before=null;
        ContentNativeProbeTarget? request=null;
        string? requestId=null;
        Task<object>? protocolRun = null;
        string Fingerprint(DesktopUpProjection p)=>StateRequestFingerprint.Compute(new
        {p.Revision,p.CatalogVersion,p.ControlCenter.Settings,posters=p.Posters.Select(x=>new{x.Poster,x.EntityVersion,x.PosterRevision}).ToArray()});
        void Require(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
        void Stop(){if(stopped)return;stopped=true;timer.Stop();timer.Tick-=Tick;lease.Dispose();_stopAutoSmoke=null;}
        _stopAutoSmoke=Stop;
        void Finish(bool passed,string? error,bool inputBlocked=false)
        {
            Stop();
            _log!.Write(passed?"info":inputBlocked?"warning":"error","content.interaction_observed",new
            {passed,error,inputBlocked,mode,index,observations,nativeMouse=(mode is "native" or "native-mouse")&&!inputBlocked&&passed,
                nativeKeyboard=mode=="native"&&!inputBlocked&&passed,keyboardScope="Escape during owned capture only; Enter not synthesized",
                processRestartTested=mode=="restart"&&passed,uiCProductAccepted=false,milestoneAccepted=false});
            ExitApplication();
        }
        void SaveExpected(DesktopUpProjection current)
        {
            Require(!File.Exists(expectedPath),"Expected-state evidence already exists.");
            File.WriteAllText(expectedPath,JsonSerializer.Serialize(new{fingerprint=Fingerprint(current),current.Revision,
                posters=current.Posters.Select(p=>p.Poster).ToArray()},new JsonSerializerOptions{WriteIndented=true}));
        }
        void Tick(DispatcherQueueTimer sender,object args)
        {
            if(ticking||stopped)return;ticking=true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds<deadline,"Content interaction probe timed out.");
                if(!_autoReady||_autoClock.ElapsedMilliseconds<readyAfter)return;
                var current=_stateCoordinator!.Refresh();
                if (mode == "protocol" && !initialized && current.Posters.Count > 0 &&
                    !_spatialRuntime!.ContentVisualReadyForProbe(current.Posters[0].Poster.PosterId)) return;
                if(!initialized)
                {
                    Require(current.Posters.Count==(mode=="rejected-heat"?1:mode=="restart"?current.Posters.Count:2)&&current.Posters.Count>0,
                        "The isolated fixture has the wrong number of posters.");
                    target=current.Posters[0].Poster.PosterId;initialized=true;
                    if(mode=="restart")
                    {
                        using var expected=JsonDocument.Parse(File.ReadAllText(expectedPath));
                        Require(expected.RootElement.GetProperty("fingerprint").GetString()==Fingerprint(current),"Restart changed saved configuration or versions.");
                        foreach(var p in current.Posters)
                        {
                            var surface=_spatialRuntime!.ReadContentSurfaceForSmoke(p.Poster.PosterId);
                            Require(surface.CanvasMatches&&surface.InputMatches&&surface.Clipped,"Restarted native content geometry does not agree.");
                        }
                        observations.Add(new{name="process-restart-configuration-and-surface",current.Revision,fingerprint=Fingerprint(current)});
                        Finish(true,null);return;
                    }
                    if(mode=="protocol")
                    {
                        protocolRun = _spatialRuntime!.RunDirectContentProtocolProbe(target, dataRoot);
                        return;
                    }
                    if(mode=="rejected-heat")
                    {
                        _spatialRuntime!.PrepareRejectedContentPreviewProbe(target);
                        readyAfter=_autoClock.ElapsedMilliseconds+600;return;
                    }
                    Require(mode is "native" or "native-mouse","Unknown probe mode.");
                }
                if (mode == "protocol")
                {
                    if (protocolRun is null || !protocolRun.IsCompleted) return;
                    observations.Add(protocolRun.GetAwaiter().GetResult());
                    SaveExpected(_stateCoordinator.Refresh()); Finish(true, null); return;
                }
                if(mode=="rejected-heat")
                {
                    observations.Add(_spatialRuntime!.RunRejectedContentPreviewProbe(target));
                    SaveExpected(_stateCoordinator.Refresh());Finish(true,null);return;
                }
                if(requestId is not null)
                {
                    string response=Path.Combine(dataRoot,"native-response-"+requestId+".json");
                    if(!File.Exists(response))return;
                    using var result=JsonDocument.Parse(File.ReadAllText(response));
                    string status=result.RootElement.GetProperty("Status").GetString()??"";
                    if(status!="Completed")
                    {observations.Add(new{name="driver-"+actions[index],result=result.RootElement.Clone()});
                        Finish(false,"Native driver did not complete: "+status,
                        status is "NotExecuted" or "Occluded");return;}
                    if(_spatialRuntime!.IsSpatialBusy)return;
                    observations.Add(new{name="driver-"+actions[index],result=result.RootElement.Clone()});
                    var after=_spatialRuntime.ReadNativeContentForProbe(target);
                    string action=actions[index];
                    Require(!after.HasGesture&&!after.NativeCapture&&after.IsEditing==(action=="resize"),
                        "Native gesture/capture outlived completion or editing mode differed from the requested action.");
                    long revisions=action is "auto-drag" or "manual-drag" or "resize"?1:0;
                    Require(after.Revision==before!.Revision+revisions,"Unexpected number of committed revisions for "+action);
                    Require(after.OpenIntents==before.OpenIntents+(action=="double-click"?1:0),"Drag/single-click/cancel incorrectly opened an item, or double-click did not open exactly once.");
                    if(revisions==0)Require(ContentLayoutEditing.SameState(before.Document,after.Document),"Non-committing input changed author intent.");
                    else
                    {
                        Require(after.Document.DocumentVersion==before.Document.DocumentVersion+1,"Native edit did not increment document once.");
                        if(action=="auto-drag")
                        {
                            var pin=after.Document.Placements.Single(p=>p.ItemId==request!.ItemId);
                            var original=before.Document.Placements.Single(p=>p.ItemId==request!.ItemId);
                            Require(pin.PinnedIndex==request!.ExpectedPinnedIndex&&!pin.FixedPosition,"Automatic drag did not establish the expected final order pin.");
                            Require((pin with {Version=original.Version,PinnedIndex=original.PinnedIndex})==original&&
                                before.Document.Placements.Where(p=>p.ItemId!=pin.ItemId).All(p=>after.Document.Placements.Single(n=>n.ItemId==p.ItemId)==p),
                                "Automatic native drag altered another intent field or member.");
                        }
                        else if(action=="manual-drag")
                        {
                            var moved=after.Document.Placements.Single(p=>p.ItemId==request!.ItemId);
                            var source=request!.Source;
                            Require(after.Document.Mode==ContentLayoutMode.Manual&&moved.Column==source.Column-source.RegionColumn&&
                                moved.Row==source.Row-source.RegionRow+1&&moved.ColumnSpan==source.ColumnSpan&&moved.RowSpan==source.RowSpan,
                                "Native movement did not move the requested card by exactly one main-axis grid pitch.");
                        }
                        else if(action=="resize")
                        {
                            var resized=after.Document.Placements.Single(p=>p.ItemId==request!.ItemId);
                            var source=request!.Source;
                            Require(resized.Column==source.Column-source.RegionColumn&&resized.Row==source.Row-source.RegionRow&&
                                resized.ColumnSpan==source.ColumnSpan-1&&resized.RowSpan==source.RowSpan&&resized.ManualSize,
                                "Native span handle did not resize the requested card by exactly one column without moving it.");
                        }
                    }
                    observations.Add(new{name=action,before.Revision,afterRevision=after.Revision,after.OpenIntents,
                        document=after.Document,geometry=_spatialRuntime.ReadContentSurfaceForSmoke(target)});
                    if (action == "context") observations.Add(_spatialRuntime.CompleteNativeContentMenuForProbe(target, request!.ItemId));
                    if(action=="resize")
                    {
                        Require(_spatialRuntime.UndoContentEditor(target,false) is null,"Native resize undo failed.");
                        Require(ContentLayoutEditing.SameState(before.Document,_spatialRuntime.ReadNativeContentForProbe(target).Document,true),"Undo did not restore all intent fields.");
                        Require(_spatialRuntime.UndoContentEditor(target,true) is null,"Native resize redo failed.");
                        Require(ContentLayoutEditing.SameState(after.Document,_spatialRuntime.ReadNativeContentForProbe(target).Document,true),"Redo did not restore all intent fields.");
                        Require(_spatialRuntime.SetContentEditing(target,false) is null,"Cannot leave editing after native resize.");
                        var daily=_spatialRuntime.ReadContentAdornmentsForProbe(target);
                        observations.Add(new{name="native-resize-returned-to-daily",adornments=daily});
                        Require(!daily.IsEditing && daily.SelectedBorderThickness is { } thickness && double.IsFinite(thickness) &&
                            Math.Abs(thickness-.8)<=.000001 && daily.VisualResizeHandles==0 && daily.PublishedResizeHandles==0 &&
                            daily.Surface.CanvasMatches && daily.Surface.InputMatches,
                            "Native resize left editing adornments or stale handle hits in daily use: " + JsonSerializer.Serialize(daily));
                    }
                    requestId=null;request=null;index++;
                    readyAfter=_autoClock.ElapsedMilliseconds+NativeContentInteraction.DoubleClickSettings.MaximumIntervalMilliseconds+100;
                    return;
                }
                if(_spatialRuntime!.IsSpatialBusy)return;
                if(index==actions.Length){SaveExpected(current);Finish(true,null);return;}
                if(actions[index]=="manual-drag"&&_spatialRuntime.ReadNativeContentForProbe(target).Document.Mode!=ContentLayoutMode.Manual)
                {
                    Require(_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual}) is null,"Enter manual failed.");
                    readyAfter=_autoClock.ElapsedMilliseconds+300;return;
                }
                if(actions[index]=="resize"&&!_spatialRuntime.ReadNativeContentForProbe(target).IsEditing)
                {
                    Require(_spatialRuntime.SetContentEditing(target,true) is null,"Cannot enter editing for the native resize handle.");
                    readyAfter=_autoClock.ElapsedMilliseconds+100;return;
                }
                before=_spatialRuntime.ReadNativeContentForProbe(target);
                request=_spatialRuntime.NativeContentTargetForProbe(target,actions[index]);requestId=index.ToString("D2");
                string driverAction=actions[index] switch {"click"=>"click","double-click"=>"double-click","cancel-drag"=>"cancel-drag","context"=>"right-click",_=>"drag"};
                string path=Path.Combine(dataRoot,"native-request-"+requestId+".json");
                Require(!File.Exists(path),"Native request evidence already exists.");
                File.WriteAllText(path,JsonSerializer.Serialize(new{requestId,processId=Environment.ProcessId,
                    request.InputWindowHandle,action=driverAction,startX=request.Start.X,startY=request.Start.Y,endX=request.End.X,endY=request.End.Y}));
            }
            catch(Exception error){Finish(false,error.ToString());}
            finally{ticking=false;}
        }
        timer.Tick+=Tick;timer.Start();
    }
}
