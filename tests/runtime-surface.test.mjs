import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const tauriConfig = JSON.parse(readFileSync("src-tauri/tauri.conf.json", "utf8"));
const rustShell = readFileSync("src-tauri/src/lib.rs", "utf8");
const rustEntry = readFileSync("src-tauri/src/main.rs", "utf8");
const desktopHostAdapter = readFileSync("src-tauri/src/desktop_host.rs", "utf8");
const runtimeMonitor = readFileSync("src-tauri/src/runtime_monitor.rs", "utf8");
const placementStore = readFileSync("src-tauri/src/placement.rs", "utf8");
const posterSurface = readFileSync("app/src/poster-surface.js", "utf8");
const posterStyles = readFileSync("app/src/poster-surface.css", "utf8");

function windowConfig(label) {
  return tauriConfig.app.windows.find((window) => window.label === label);
}

test("cold start exposes only the poster surface", () => {
  const main = windowConfig("main");
  const poster = windowConfig("poster");

  assert.ok(main, "main control-center window must be configured");
  assert.ok(poster, "poster surface window must be configured");
  assert.equal(main.visible, false, "control center must stay hidden on cold start");
  assert.equal(poster.visible, true, "poster surface must remain the default visible surface");
  assert.equal(poster.title, "", "poster native title must stay empty to avoid DWM text");
  assert.match(rustShell, /window\.set_title\(""\)/);
  assert.match(desktopHostAdapter, /DWMWA_NCRENDERING_POLICY/);
  assert.match(desktopHostAdapter, /DWMNCRP_DISABLED/);
  assert.match(desktopHostAdapter, /DWMWA_BORDER_COLOR/);
  assert.match(posterStyles, /backdrop-filter:\s*none/);
});

test("release executable uses the Windows GUI subsystem", () => {
  assert.match(
    rustEntry,
    /cfg_attr\(not\(debug_assertions\),\s*windows_subsystem\s*=\s*"windows"\)/,
    "release builds must not create a console or Windows Terminal window"
  );
});

test("control center remains available through explicit native entry points", () => {
  assert.match(
    rustShell,
    /single_instance::init\(\|app,[\s\S]*?show_main_window\(app\)/,
    "a second launch must recall the existing control center"
  );
  assert.match(
    rustShell,
    /"show"\s*=>\s*show_main_window\(app\)/,
    "the tray home command must open the control center"
  );
  assert.match(
    rustShell,
    /WindowEvent::CloseRequested[\s\S]*?prevent_close\(\)[\s\S]*?window\.hide\(\)/,
    "closing the control center must hide it without ending the runtime"
  );
});

test("WorkerW attachment is isolated behind an observable ToolWindow fallback", () => {
  assert.match(desktopHostAdapter, /fn attach_to_workerw/);
  assert.match(desktopHostAdapter, /fn configure_tool_window_fallback/);
  assert.match(desktopHostAdapter, /WORKERW_REQUEST_TIMEOUT_MS/);
  assert.match(desktopHostAdapter, /DESKTOP_UP_DISABLE_WORKERW/);
  assert.match(desktopHostAdapter, /GW_HWNDPREV/);
  assert.match(rustShell, /desktop_host_diagnostics/);
  assert.match(rustShell, /桌面挂载：正在初始化/);
});

test("poster surface publishes a native interaction region", () => {
  assert.match(posterSurface, /set_poster_interaction_region/);
  assert.match(posterSurface, /POSTER_SURFACE_GUTTER/);
  assert.match(posterSurface, /window\.devicePixelRatio/);
  assert.match(posterSurface, /surface\.querySelector\(".poster"\)/);
  assert.match(rustShell, /tauri::PhysicalSize/);
  assert.match(desktopHostAdapter, /CreateRoundRectRgn/);
  assert.match(desktopHostAdapter, /SetWindowRgn/);
});

test("ToolWindow fallback recovers from shell foreground transitions without polling", () => {
  assert.match(desktopHostAdapter, /SetWinEventHook/);
  assert.match(desktopHostAdapter, /EVENT_SYSTEM_FOREGROUND/);
  assert.match(desktopHostAdapter, /EVENT_OBJECT_CREATE/);
  assert.match(desktopHostAdapter, /SetWindowSubclass/);
  assert.match(desktopHostAdapter, /WM_DISPLAYCHANGE/);
  assert.match(desktopHostAdapter, /WM_DPICHANGED/);
  assert.match(desktopHostAdapter, /SW_SHOWNOACTIVATE/);
  assert.match(desktopHostAdapter, /SWP_ASYNCWINDOWPOS/);
  assert.match(desktopHostAdapter, /SHELL_RECOVERY_SETTLE_MS/);
  assert.match(desktopHostAdapter, /NON_CLIENT_DECORATION_STYLES/);
  assert.match(desktopHostAdapter, /WS_CAPTION/);
  assert.match(rustShell, /install_shell_foreground_monitor/);
  assert.doesNotMatch(desktopHostAdapter, /setInterval/);
});

test("poster placement persists a monitor-relative anchor and restores inside work area", () => {
  assert.match(placementStore, /poster-placement-v1\.json|PLACEMENT_VERSION/);
  assert.match(placementStore, /PERSIST_DEBOUNCE_MS/);
  assert.match(placementStore, /monitor_device/);
  assert.match(placementStore, /anchor_x/);
  assert.match(desktopHostAdapter, /EnumDisplayMonitors/);
  assert.match(desktopHostAdapter, /MONITORINFOEXW/);
  assert.match(desktopHostAdapter, /restore_poster_placement/);
  assert.match(rustShell, /WindowEvent::ScaleFactorChanged/);
  assert.match(rustShell, /app_config_dir/);
  assert.match(posterSurface, /window\.addEventListener\("resize", scheduleNativeSurfaceSync\)/);
});

test("fullscreen lock and display-off pause through event-driven runtime policy", () => {
  assert.match(runtimeMonitor, /EVENT_SYSTEM_FOREGROUND/);
  assert.match(runtimeMonitor, /EVENT_OBJECT_LOCATIONCHANGE/);
  assert.match(runtimeMonitor, /WTSRegisterSessionNotification/);
  assert.match(runtimeMonitor, /WM_WTSSESSION_CHANGE/);
  assert.match(runtimeMonitor, /RegisterPowerSettingNotification/);
  assert.match(runtimeMonitor, /GUID_CONSOLE_DISPLAY_STATE/);
  assert.match(runtimeMonitor, /PBT_POWERSETTINGCHANGE/);
  assert.match(runtimeMonitor, /DesktopUp\.RuntimePauseFlags/);
  assert.match(runtimeMonitor, /DesktopUp\.ForegroundProbeFlags/);
  assert.match(runtimeMonitor, /DesktopUp\.RuntimeMonitorCapabilities/);
  assert.match(runtimeMonitor, /SetPropW/);
  assert.match(runtimeMonitor, /ForegroundCoverage::Fullscreen/);
  assert.match(runtimeMonitor, /ForegroundCoverage::Maximized/);
  assert.match(runtimeMonitor, /Shell_TrayWnd/);
  assert.match(runtimeMonitor, /preserve_previous/);
  assert.match(runtimeMonitor, /is_desktop_surface/);
  assert.match(runtimeMonitor, /STARTUP_RECHECK_MS/);
  assert.match(rustShell, /runtime-pause-state/);
  assert.match(rustShell, /runtime_monitor::reevaluate_foreground\(\)/);
  assert.match(rustShell, /effective_paused/);
  assert.match(posterSurface, /runtime-pause-state/);
  assert.match(posterSurface, /automaticallyPaused/);
  assert.doesNotMatch(runtimeMonitor, /setInterval|GetTickCount|loop\s*\{\s*thread::sleep/);
});

test("background runtime events synchronize tray UI without holding the state lock", () => {
  assert.match(
    rustShell,
    /fn schedule_runtime_outputs[\s\S]*?run_on_main_thread[\s\S]*?tray\.sync\(&snapshot\)/,
    "runtime UI updates must be marshalled onto the Tauri main thread"
  );
  assert.equal(
    (rustShell.match(/tray\.sync\(&snapshot\)/g) ?? []).length,
    1,
    "tray synchronization must only occur inside the main-thread scheduler"
  );
  assert.match(
    rustShell,
    /fn apply_runtime_visibility[\s\S]*?drop\(snapshot\);[\s\S]*?schedule_runtime_outputs/,
    "the runtime worker must release RuntimeState before scheduling UI work"
  );
});
