using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp;

public partial class App
{
    // Isolated same-package test only; never accepts a user file or arbitrary storage destination.
    private async void RunStorageSettingsProbe(string dataRoot)
    {
        string? mode=_startupIntent.Arguments.SingleOrDefault(x=>x.StartsWith("--storage-case="))?[15..];
        IDisposable? lease=null;
        try
        {
            string repo=AppContext.BaseDirectory;
            while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository fixture required.");
            string root=Path.GetDirectoryName(Path.GetFullPath(dataRoot))!;
            string allowed=Path.Combine(repo,"out","drop-d2");
            if(!LocalTransferFiles.Contains(allowed,root)||Path.GetFileName(dataRoot)!="profile"||
                Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT")!=dataRoot||
                !File.Exists(Path.Combine(root,".storage-settings-fixture"))||File.ReadAllText(Path.Combine(root,".storage-settings-fixture"))!="owned"||
                mode is not ("verify" or "cancel" or "close" or "early-close" or "exit" or "seed" or "restart" or "zero"))throw new IOException("Isolated storage settings fixture required.");
            lease=_ledger!.Acquire("fixture.storage-settings","same-package");
            await _transferDiscovery;
            var service=_storageLocation!;var store=_composition!.Store;
            var repository=new SqliteStateCommandRepository(store);
            var readyWatch=System.Diagnostics.Stopwatch.StartNew();
            while(_controlCenterEnvironmentPending)
            {if(readyWatch.Elapsed.TotalSeconds>20)throw new IOException("Desktop readiness timeout.");await Task.Delay(100);}
            _controlCenter!.Show();var center=_controlCenter.WindowForPortableProbe!;
            var display=center.ShowStorageForProbe();
            if(mode=="early-close")
            {await center.CancelPortableAndWaitAsync();await display;_log!.Write("info","contract.storage_settings_passed",new{mode,readCancelled=true});return;}
            await Task.Delay(250);
            if(center.StorageDialogForProbe?.Title?.ToString()!="收纳位置 · 全局")throw new IOException("Storage entry/dialog missing.");
            center.StorageDialogForProbe.Hide();await display;
            if(mode is "zero" or "restart")
            {var location=await Task.Run(service.Read);_log!.Write("info","contract.storage_settings_passed",new{mode,location.Exists});return;}
            var storage=await Task.Run(()=>service.EnsureReady());
            var state=repository.Read();
            if(state.ContentCatalog.Items.Count==0)
            {
                string source=Path.Combine(root,"test-source");Directory.CreateDirectory(source);
                string path=Path.Combine(source,"D2-04测试文件.txt");
                if(File.Exists(path))throw new IOException("Fixture source occupied; preserve it.");
                // A visible small text file; the 16 MiB payload makes cancellation observable without filling a disk.
                File.WriteAllText(path,"DesktopUp D2-04 自建验收文件。可以迁移，不包含个人内容。");
                string poster=state.Catalog.Posters.First().Poster.PosterId;
                var preview=DirectTransferPreparation.Incoming(state,poster,[path],storage);
                var journal=new SqliteTransferJournal(store);journal.Begin(preview.Plan,preview.Files,preview.Plan.Fingerprint,storage.Collection.Id);
                var moved=await Task.Run(()=>new AutomaticTransferCompletion(journal).Execute(preview.Plan.Request.OperationId));
                if(moved.Any(x=>x.Kind!=TransferFinishKind.Completed))throw new IOException("Fixture seed transfer failed.");
                if(mode!="seed")File.WriteAllBytes(Path.Combine(storage.Collection.StorageLocator!,"cancellation-fixture.bin"),new byte[16*1024*1024]);
            }
            _stateCoordinator!.Refresh();
            if(mode=="seed"){_log!.Write("info","contract.storage_settings_passed",new{mode,path=storage.Root.Locator});return;}
            string destination=Path.Combine(root,"destination");Directory.CreateDirectory(destination);
            var ticket=await Task.Run(()=>service.PreviewChange(destination));
            var operation=center.ChangeStorageForProbe(ticket);await Task.Delay(30);
            if(mode=="exit")
            {_log!.Write("info","contract.storage_settings_exit_armed");lease.Dispose();lease=null;ExitApplication();return;}
            if(mode=="close")
            {
                await center.CancelPortableAndWaitAsync();center.Close();await operation;
                _controlCenter.Show();if(_controlCenter.WindowForPortableProbe==center)throw new IOException("Center did not reopen.");
            }
            else
            {
                if(mode=="cancel")center.CancelStorageChange();
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    while(!operation.IsCompleted)
                    {
                        if(timer.Elapsed.TotalSeconds>35)throw new IOException("Storage operation timed out.");
                        if(center.StorageDialogForProbe is {} dialog&&dialog.Title?.ToString()!="正在更改收纳位置")dialog.Hide();
                        await Task.Delay(50);
                    }
                }
                await operation;
                if(mode=="cancel")
                {var reopened=center.ShowStorageForProbe();await Task.Delay(150);if(center.StorageDialogForProbe is null)throw new IOException("Cancel left center unusable.");center.StorageDialogForProbe.Hide();await reopened;}
            }
            var final=await Task.Run(service.Read);
            if(mode=="verify"&&final.Path!=destination)throw new IOException("New location not published.");
            var file=repository.Read().ContentCatalog.Items.Single().Target.Locator;
            if(!File.Exists(file))throw new IOException("Committed content missing.");
            _log!.Write("info","contract.storage_settings_passed",new{mode,location=final.Path,fileRetained=true,nativeMouse=false});
        }
        catch(Exception error){Environment.ExitCode=1;_log?.Write("error","contract.storage_settings_failed",new{mode,error.Message});}
        finally{lease?.Dispose();if(mode!="exit"||Volatile.Read(ref _exiting)==0)ExitApplication();}
    }
}
