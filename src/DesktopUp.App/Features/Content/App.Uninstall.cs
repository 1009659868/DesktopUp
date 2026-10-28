using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

public partial class App
{
    private IDisposable? _profileLifecycleLease;
    private bool IsUninstallMode => _startupIntent.Arguments.Any(a => a.StartsWith("--prepare-uninstall", StringComparison.OrdinalIgnoreCase));

    // Started by Uninstall.exe before any package file is removed. No posters, tray,
    // watcher, automatic file replay, default storage or default poster is started here.
    private async void RunUninstallPreparation()
    {
        Environment.ExitCode = 32;
        bool silent = _startupIntent.Arguments.Contains("--uninstall-silent", StringComparer.Ordinal);
        var preparation = CreateUninstallPreparation();
        Window? window = null; var cancel = new CancellationTokenSource(); bool finished = false;
        TextBlock? status = null; Button? stop = null;
        try
        {
            var flags = _startupIntent.Arguments.Where(a => a.StartsWith("--prepare-uninstall", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (flags.Length != 1 || flags[0] is not ("--prepare-uninstall=return" or "--prepare-uninstall=keep")) throw new IOException("无效的卸载内容处理参数。");
            bool keep = flags[0].EndsWith("=keep", StringComparison.Ordinal);
            if (!silent)
            {
                window = new Window { Title = "DesktopUp · 卸载前内容处理" };
                window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(640, 360));
                status = new TextBlock { Text = "正在检查收纳内容…", TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
                stop = new Button { Content = "取消卸载" };
                stop.Click += (_, _) => { if (finished) { window.Close(); ExitApplication(); } else { cancel.Cancel(); stop.IsEnabled = false; } };
                var panel = new StackPanel { Padding = new Thickness(24), Spacing = 18 };
                panel.Children.Add(new TextBlock { Text = keep ? "保留收纳内容" : "归还收纳内容", FontSize = 24 });
                panel.Children.Add(new ScrollViewer { Content = status, MaxHeight = 200 }); panel.Children.Add(stop); window.Content = panel;
                window.AppWindow.Closing += (_, args) => { if (!finished) { args.Cancel = true; cancel.Cancel(); } };
                window.Closed += (_, _) => { if (finished) ExitApplication(); };
                window.Activate();
            }
            if (!preparation.OpenExisting()) { Environment.ExitCode = 0; return; }
            _storageInitialized = true;
            nint owner = window is null ? 0 : WinRT.Interop.WindowNative.GetWindowHandle(window);
            await preparation.Execute(keep, silent, owner, cancel.Token, path =>
                _dispatcher!.TryEnqueue(() => { if (status is not null) status.Text = "正在归还：\n" + path + "\n\n取消将保留程序和剩余内容；已归还项不搬回。"; }));
            _log!.Write("info", "uninstall.content_prepared", new { keep, returned = preparation.ReturnedCount, locations = preparation.RetainedLocationCount });
            Environment.ExitCode = 0;
        }
        catch (Exception e)
        {
            Environment.ExitCode = cancel.IsCancellationRequested ? 33 : 32;
            _log?.Write("warning", "uninstall.content_preserved", new { e.Message });
            try { preparation.SaveFailureReport(e.Message); } catch (Exception reportError) { _log?.Write("warning", "uninstall.report_failed", new { reportError.Message }); }
            if (status is not null && stop is not null)
            { status.Text = e.Message + "\n\n未执行程序卸载。已经归还的文件保持原位，配置及剩余内容保留；可关闭后重试。"; stop.Content = "关闭"; stop.IsEnabled = true; }
        }
        finally
        {
            finished = true; cancel.Dispose();
            if (Environment.ExitCode == 0 || silent || window is null) { window?.Close(); ExitApplication(); }
        }
    }

}
