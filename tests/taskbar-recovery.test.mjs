import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");
const desktopHost = readFileSync("src-tauri/src/desktop_host.rs", "utf8");
const taskbarRecovery = readFileSync("src-tauri/src/taskbar_recovery.rs", "utf8");
const posterRuntime = readFileSync("app/src/poster-runtime.js", "utf8");

test("TaskbarCreated uses the tray message window and one versioned lifecycle", () => {
  assert.match(taskbarRecovery, /TASKBAR_RECOVERY_CONTRACT_VERSION: u32 = 1/);
  assert.match(taskbarRecovery, /TRAY_WINDOW_CLASS: &str = "tray_icon_app"/);
  assert.match(taskbarRecovery, /RegisterWindowMessageW\(w!\("TaskbarCreated"\)\)/);
  assert.match(taskbarRecovery, /SetWindowSubclass/);
  assert.match(taskbarRecovery, /RemoveWindowSubclass/);
  assert.match(taskbarRecovery, /record_taskbar_created/);
  assert.match(taskbarRecovery, /coalesced_event_count/);
  assert.match(taskbarRecovery, /TASKBAR_DEBOUNCE_MS: u64 = 180/);
  assert.match(taskbarRecovery, /TASKBAR_RECOVERY_MAX_ATTEMPTS: u32 = 3/);
  assert.doesNotMatch(taskbarRecovery, /HWND_TOPMOST|HWND_NOTOPMOST/);
});

test("TaskbarCreated verifies or rebuilds exactly one tray and restores native snapshots", () => {
  assert.match(nativeRuntime, /const TRAY_ICON_ID: &str = "desktop-up-tray"/);
  assert.match(nativeRuntime, /fn build_tray_icon[\s\S]*?TrayIconBuilder::with_id\(TRAY_ICON_ID\)/);
  assert.match(nativeRuntime, /app\.on_menu_event/);
  assert.match(nativeRuntime, /app\.on_tray_icon_event/);
  assert.match(nativeRuntime, /tray\.set_tooltip\(Some\(TRAY_TOOLTIP\)\)/);
  assert.match(nativeRuntime, /drop\(app\.remove_tray_by_id\(TRAY_ICON_ID\)\)/);
  assert.match(nativeRuntime, /reattach_taskbar_listener_on_main_thread/);
  assert.match(nativeRuntime, /HostRecoveryRequest::taskbar_created/);
  assert.match(desktopHost, /Self::TaskbarCreated => "taskbarCreated"/);
  assert.match(posterRuntime, /taskbarRecovery: raw\.taskbarRecovery \?\? state\.taskbarRecovery/);
});

test("Exiting is irreversible and removes listeners, surfaces and tray in order", () => {
  assert.match(desktopHost, /HOST_RECOVERY_EXITING: AtomicBool/);
  assert.match(desktopHost, /pub fn begin_exit\(\)[\s\S]*?HOST_RECOVERY_PENDING/);
  assert.match(desktopHost, /fn queue_host_recovery[\s\S]*?HOST_RECOVERY_EXITING\.load/);
  assert.match(taskbarRecovery, /pub fn begin_exit[\s\S]*?RemoveWindowSubclass/);
  assert.match(taskbarRecovery, /ignored_after_exit_count/);

  const exitPath = nativeRuntime.match(/fn prepare_runtime_exit[\s\S]*?\n\}/)?.[0];
  assert.ok(exitPath, "unified exit preparation must exist");
  const sequence = [
    "begin_runtime_exit",
    "desktop_host::begin_exit",
    "taskbar_recovery::begin_exit",
    "store.flush",
    "window.destroy",
    "remove_tray_icon"
  ].map((step) => exitPath.indexOf(step));
  assert.ok(sequence.every((index) => index >= 0));
  assert.deepEqual([...sequence].sort((a, b) => a - b), sequence);
  assert.match(nativeRuntime, /RunEvent::ExitRequested[\s\S]*?prepare_runtime_exit/);
});
