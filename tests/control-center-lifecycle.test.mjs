import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");
const lifecycleRuntime = readFileSync("src-tauri/src/control_center_lifecycle.rs", "utf8");
const controlCenterRuntime = readFileSync("app/src/main.js", "utf8");
const posterRuntime = readFileSync("app/src/poster-runtime.js", "utf8");

test("control center readiness is an explicit frontend handshake after listeners and first render", () => {
  assert.match(nativeRuntime, /mod control_center_lifecycle;/);
  assert.match(nativeRuntime, /fn mark_control_center_ready/);
  assert.match(nativeRuntime, /mark_control_center_ready,/);

  const pageLoadPath = nativeRuntime.match(
    /if webview\.label\(\) == CONTROL_CENTER_WINDOW_LABEL \{[\s\S]*?\n\s*return;\n\s*\}/
  )?.[0];
  assert.ok(pageLoadPath, "control-center page-load branch must exist");
  assert.match(pageLoadPath, /PageLoadEvent::Started/);
  assert.match(pageLoadPath, /page_load_started/);
  assert.doesNotMatch(pageLoadPath, /mark_main_window_ready|PageLoadEvent::Finished/);

  const connectStart = controlCenterRuntime.indexOf("async function connectNativeControls");
  const runtimeListener = controlCenterRuntime.indexOf('await listen("runtime-control"', connectStart);
  const bootstrapStart = controlCenterRuntime.indexOf("async function bootstrap");
  const connectCall = controlCenterRuntime.indexOf("await connectNativeControls()", bootstrapStart);
  const firstRender = controlCenterRuntime.indexOf("render();", connectCall);
  const readyCall = controlCenterRuntime.indexOf('await invoke("mark_control_center_ready")', firstRender);
  assert.ok(connectStart >= 0 && runtimeListener > connectStart);
  assert.ok(connectCall > bootstrapStart);
  assert.ok(firstRender > connectCall);
  assert.ok(readyCall > firstRender, "ready must follow event subscription, snapshot hydration and render");
});

test("pre-ready commands are ordered, reload-safe and observable in the native lifecycle snapshot", () => {
  assert.match(lifecycleRuntime, /pub const CONTROL_CENTER_LIFECYCLE_CONTRACT_VERSION: u32 = 1/);
  assert.match(lifecycleRuntime, /pending_commands: Vec<String>/);
  assert.match(lifecycleRuntime, /pub fn queue_or_take_ready/);
  assert.match(lifecycleRuntime, /pub fn mark_ready/);
  assert.match(lifecycleRuntime, /std::mem::take\(&mut state\.pending_commands\)/);
  assert.match(lifecycleRuntime, /pub fn page_load_started/);
  assert.match(lifecycleRuntime, /pub fn window_destroyed/);
  assert.match(nativeRuntime, /control_center: ControlCenterLifecycleSnapshot/);
  assert.match(posterRuntime, /controlCenter: raw\.controlCenter \?\? state\.controlCenter/);
  assert.match(posterRuntime, /taskbarRecovery: raw\.taskbarRecovery \?\? state\.taskbarRecovery/);
});

test("tray actions use native runtime or formal SQLite state instead of a hidden control-center executor", () => {
  const menuPath = nativeRuntime.match(
    /fn handle_tray_menu_event[\s\S]*?\n\}/
  )?.[0];
  assert.ok(menuPath, "native tray menu route must exist");
  assert.match(menuPath, /"workspace" => toggle_poster_window\(app\)/);
  assert.match(menuPath, /"edit" => toggle_edit_mode\(app\)/);
  assert.match(menuPath, /"lock-all" => toggle_all_poster_locks\(app\)/);
  assert.match(menuPath, /"pause" => toggle_manual_pause\(app\)/);
  assert.match(menuPath, /"theme-nocturne" => apply_quick_theme\(app, "nocturne"\)/);
  assert.doesNotMatch(menuPath, /emit_control|show_and_emit\(app, "theme:/);

  assert.match(nativeRuntime, /fn mutate_poster_state_with_cas/);
  assert.match(nativeRuntime, /replace_with_expected_revision\(Some\(revision\), snapshot\)/);
  assert.match(nativeRuntime, /fn all_posters_locked/);
  assert.match(nativeRuntime, /snapshot\.all_locked = initial_all_locked/);
  assert.match(nativeRuntime, /sync_poster_runtime_summary\(app, snapshot, "posterStateChanged"\)/);

  const runtimeControlPath = controlCenterRuntime.match(
    /await listen\("runtime-control"[\s\S]*?\n\s*\}\);/
  )?.[0];
  assert.ok(runtimeControlPath, "control center route consumer must exist");
  assert.match(runtimeControlPath, /open-settings/);
  assert.match(runtimeControlPath, /open-agent/);
  assert.doesNotMatch(runtimeControlPath, /toggle-edit|toggle-lock-all|theme:/);
});

test("single-click remains inert while double-click and close use the one-window coordinator", () => {
  const trayEventPath = nativeRuntime.match(
    /fn handle_tray_icon_event[\s\S]*?\n\}/
  )?.[0];
  assert.ok(trayEventPath, "tray icon event route must exist");
  assert.match(trayEventPath, /event\.id\(\)\.as_ref\(\) != TRAY_ICON_ID/);
  assert.match(trayEventPath, /TrayIconEvent::DoubleClick/);
  assert.match(trayEventPath, /MouseButton::Left/);
  assert.match(trayEventPath, /toggle_main_window\(app\)/);
  assert.doesNotMatch(trayEventPath, /TrayIconEvent::Click|toggle_poster_window/);
  assert.match(nativeRuntime, /app\.on_menu_event/);
  assert.match(nativeRuntime, /app\.on_tray_icon_event/);

  assert.match(nativeRuntime, /execution: Mutex<\(\)>/);
  assert.match(nativeRuntime, /get_webview_window\(CONTROL_CENTER_WINDOW_LABEL\)/);
  assert.match(nativeRuntime, /fn release_main_window[\s\S]*?window\.destroy\(\)/);
  assert.match(nativeRuntime, /failed to release control-center resources, hiding instead/);
  assert.match(nativeRuntime, /WindowEvent::CloseRequested[\s\S]*?api\.prevent_close\(\)[\s\S]*?release_main_window\(/);
  assert.match(nativeRuntime, /WindowEvent::Destroyed[\s\S]*?lifecycle\.window_destroyed\(\)/);
});
