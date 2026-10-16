using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public partial class App
{
    private IDisposable? _profileLifecycleLease;
    private bool IsUninstallMode => _startupIntent.Arguments.Any(a=>a.StartsWith("--prepare-uninstall",StringComparison.OrdinalIgnoreCase));

    // Started by Uninstall.exe before any package file is removed. No posters, tray,
    // watcher, automatic file replay, default storage or default poster is started here.
    private async void RunUninstallPreparation()
    {
        Environment.ExitCode=32;
        bool silent=_startupIntent.Arguments.Contains("--uninstall-silent",StringComparer.Ordinal);
        var paths=_services!.GetRequiredService<IUserDataPaths>();
        Window? window=null; var cancel=new CancellationTokenSource(); bool finished=false;
        TextBlock? status=null; Button? stop=null;
        var returned=new List<string>(); var retained=new List<string>();
        try
        {
            var flags=_startupIntent.Arguments.Where(a=>a.StartsWith("--prepare-uninstall",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(flags.Length!=1 || flags[0] is not ("--prepare-uninstall=return" or "--prepare-uninstall=keep"))throw new IOException("无效的卸载内容处理参数。");
            bool keep=flags[0].EndsWith("=keep",StringComparison.Ordinal);
            if(!silent)
            {
                window=new Window{Title="DesktopUp · 卸载前内容处理"};
                window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(640,360));
                status=new TextBlock{Text="正在检查收纳内容…",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
                stop=new Button{Content="取消卸载"};
                stop.Click+=(_,_)=>{if(finished){window.Close();ExitApplication();}else{cancel.Cancel();stop.IsEnabled=false;}};
                var panel=new StackPanel{Padding=new Thickness(24),Spacing=18};
                panel.Children.Add(new TextBlock{Text=keep?"保留收纳内容":"归还收纳内容",FontSize=24});
                panel.Children.Add(new ScrollViewer{Content=status,MaxHeight=200});panel.Children.Add(stop);window.Content=panel;
                window.AppWindow.Closing+=(_,args)=>{if(!finished){args.Cancel=true;cancel.Cancel();}};
                window.Closed+=(_,_)=>{if(finished)ExitApplication();};
                window.Activate();
            }
            string database=Path.Combine(paths.State,"desktopup.sqlite3");
            if(!File.Exists(database))
            {
                if(Directory.Exists(paths.State)&&Directory.EnumerateFileSystemEntries(paths.State).Any(p=>File.Exists(p)||Directory.EnumerateFileSystemEntries(p).Any()))
                    throw new IOException("配置库缺失但仍有历史记录；请先检查配置，未执行卸载。");
                Environment.ExitCode=0; return; // A never-started package has no registered storage.
            }
            if(new StateBackupRecovery(paths,()=>true).HasPendingRecovery)throw new IOException("配置恢复尚未完成，停止卸载；原配置和文件保留。");
            var store=_services!.GetRequiredService<SqliteStateStore>();
            var initialization=store.Initialize();
            if(!initialization.Succeeded)throw new IOException(initialization.Error);
            _storageInitialized=true;
            var service=new PosterContentReturn(store);
            retained.AddRange(await Task.Run(service.RetainedLocations));
            if(!keep)
            {
                var notices=await Task.Run(service.Reconcile);
                if(notices.Count>0)throw new IOException(string.Join("\n",notices));
            }
            if(!keep)
            {
                var inventory=await Task.Run(service.InspectUninstall);
                if(silent && inventory.Files.Count>0)throw new IOException("静默卸载不移动收纳文件，请使用交互卸载，或明确保留原位。");
                nint owner=window is null?0:WinRT.Interop.WindowNative.GetWindowHandle(window);
                var result=await Task.Run(()=>service.ReturnForUninstall(inventory,owner,cancel.Token,path=>
                    _dispatcher!.TryEnqueue(()=>{if(status is not null)status.Text="正在归还：\n"+path+"\n\n取消将保留程序和剩余内容；已归还项不搬回。";})));
                returned.AddRange(result.Returned);
                if(!result.Completed)throw new IOException(string.Join("\n",result.Errors));
            }
            if(cancel.IsCancellationRequested)throw new OperationCanceledException();
            SaveUninstallReport(paths,keep?"保留原位":"归还完成",returned,retained,[]);
            _log!.Write("info","uninstall.content_prepared",new{keep,returned=returned.Count,locations=retained.Count});
            Environment.ExitCode=0;
        }
        catch(Exception e)
        {
            Environment.ExitCode=cancel.IsCancellationRequested?33:32;
            _log?.Write("warning","uninstall.content_preserved",new{e.Message});
            try{SaveUninstallReport(paths,"未完成，程序及剩余文件保留",returned,retained,[e.Message]);}catch(Exception reportError){_log?.Write("warning","uninstall.report_failed",new{reportError.Message});}
            if(status is not null && stop is not null)
            {status.Text=e.Message+"\n\n未执行程序卸载。已经归还的文件保持原位，配置及剩余内容保留；可关闭后重试。";stop.Content="关闭";stop.IsEnabled=true;}
        }
        finally
        {
            finished=true;cancel.Dispose();
            if(Environment.ExitCode==0 || silent || window is null){window?.Close();ExitApplication();}
        }
    }

    private static void SaveUninstallReport(IUserDataPaths paths,string outcome,IEnumerable<string> returned,IEnumerable<string> retained,IEnumerable<string> errors)
    {
        string directory=Path.Combine(paths.Root,"uninstall-reports");
        for(string? p=directory;p is not null;p=Path.GetDirectoryName(p))
            if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("位置清单路径被替换。");
        Directory.CreateDirectory(directory);
        using var file=new FileStream(Path.Combine(directory,$"contents-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var writer=new StreamWriter(file,System.Text.Encoding.UTF8);
        writer.WriteLine("DesktopUp 卸载内容位置清单（仅保存在本机）\n"+outcome+"\n配置保留："+paths.Root);
        writer.WriteLine("\n已归还位置：");foreach(string path in returned)writer.WriteLine(path);
        writer.WriteLine("\n原收纳目录（空目录也保留；映射/外部引用未搬动）：");foreach(string path in retained)writer.WriteLine(path);
        foreach(string error in errors)writer.WriteLine("\n未完成："+error);
    }
}
