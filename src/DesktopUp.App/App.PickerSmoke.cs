using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

public partial class App
{
    // Real Windows dialogs and production portable scope/cancel/close/tray dispatch.
    // No native mouse automation, no file selection/IO or confirmation is simulated.
    private async void RunPickerSmoke(string dataRoot)
    {
        var mode = _startupIntent.Arguments.SingleOrDefault(a => a.StartsWith("--picker-case=", StringComparison.Ordinal))?["--picker-case=".Length..];
        var lease = _ledger!.Acquire("fixture.native-picker", "isolated-cancellation");
        int checks = 0;
        void Require(bool condition, string detail)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(detail);
        }
        try
        {
            Require(mode is "cancel-open" or "cancel-save" or "before-open" or "before-save" or
                "center-open" or "center-save" or "exit-open" or "exit-save" or "cancel-folder" or "center-folder" or "exit-folder" or "zero-folder" or
                "cancel-icon" or "center-icon" or "exit-icon", "unknown picker case");
            var ready = Stopwatch.StartNew();
            while (_controlCenterEnvironmentPending || _inputRouter.ReadyCount != (mode=="zero-folder"?0:1))
            {
                Require(ready.Elapsed < TimeSpan.FromSeconds(15), "desktop readiness timeout");
                await Task.Delay(100);
            }
            bool save = mode!.EndsWith("-save", StringComparison.Ordinal);
            for (int round = 0; round < (mode.StartsWith("cancel-") ? 3 : 1); round++)
            {
                _controlCenter!.Show();
                var window = _controlCenter.WindowForPortableProbe!;
                Require(window.TransferGlobalEntryForProbe,"global transfer recovery/menu unavailable");
                var scope = window.RunPortablePickerForProbe(save, initiallyCancelled: mode.StartsWith("before-"),
                    purpose:mode.EndsWith("-icon",StringComparison.Ordinal)?NativePickerPurpose.CardIcon:
                        mode.EndsWith("-folder",StringComparison.Ordinal)?NativePickerPurpose.ManagedStorageFolder:NativePickerPurpose.Profile);
                if (mode.StartsWith("before-"))
                {
                    await scope.WaitAsync(TimeSpan.FromSeconds(8));
                    Require(!window.IsPortableInteractionActive, "pre-cancel left scope busy");
                    Require(!_ledger.ActiveResources.Any(r => r.StartsWith("picker.native.", StringComparison.Ordinal)), "pre-cancel leaked resources");
                    continue;
                }
                var picker = window.ActivePortablePickerForProbe!;
                Require(picker is not null, "production picker was not attached");
                nint hwnd = await picker!.Shown.WaitAsync(TimeSpan.FromSeconds(8));
                Require(hwnd != 0 && NativeProfilePicker.IsDialogAlive(hwnd), "real dialog did not appear");
                if (mode.StartsWith("exit-"))
                {
                    _log!.Write("info", "contract.picker_exit_armed", new
                    { mode, checks, nativePicker = true, nativeMouse = false, fileIO = false });
                    lease.Dispose();
                    _tray!.InvokeForProbe(TrayAction.Exit);
                    _tray.InvokeForProbe(TrayAction.Exit); // Same production command; repeated exit is idempotent.
                    return; // The script verifies native completion, stop latency and resources from process logs.
                }
                var watch = Stopwatch.StartNew();
                if (mode.StartsWith("center-"))
                {
                    // Window.Close destroys directly; it does not exercise AppWindow.Closing.
                    // Send the system close request to this known center instance instead.
                    Require(PostPickerProbeClose(WinRT.Interop.WindowNative.GetWindowHandle(window),
                        0x0010 /* WM_CLOSE */, 0, 0), "center close request failed");
                }
                else { picker.Cancel(); picker.Cancel(); }
                string? result = await picker.Completion.WaitAsync(TimeSpan.FromSeconds(4));
                await scope.WaitAsync(TimeSpan.FromSeconds(4));
                Require(result is null, "cancel accepted a selection");
                Require(!NativeProfilePicker.IsDialogAlive(hwnd), "native dialog still exists after completion");
                Require(!window.IsPortableInteractionActive, "portable scope remained active");
                Require(!_ledger.ActiveResources.Any(r => r.StartsWith("picker.native.", StringComparison.Ordinal)), "picker resource leaked");
                if (mode.StartsWith("center-"))
                {
                    while (_controlCenter.WindowForPortableProbe is not null)
                    {
                        Require(watch.Elapsed < TimeSpan.FromSeconds(4), "center did not close");
                        await Task.Delay(30);
                    }
                    _controlCenter.Show();
                    Require(_controlCenter.WindowForPortableProbe is { IsPortableInteractionActive: false },
                        "center did not reopen cleanly");
                }
                _log!.Write("info", "contract.picker_round", new { mode, round, elapsedMs = watch.Elapsed.TotalMilliseconds, hwndClosed = true });
            }
            _log!.Write("info", "contract.picker_completed", new
            { passed = true, mode, checks, nativePicker = true, nativeMouse = false, fileIO = false });
        }
        catch (Exception error)
        {
            _log!.Write("error", "contract.picker_completed", new { passed = false, mode, checks, error = error.ToString() });
        }
        finally { lease.Dispose(); }
        ExitApplication();
    }

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostPickerProbeClose(nint hwnd, uint message, nuint wParam, nint lParam);
}
