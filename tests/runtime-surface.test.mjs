import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";
import { buildPosterMarkup } from "../app/src/poster-core.js";
import {
  LEGACY_POSTER_STORAGE_KEY,
  inspectLocalLegacyPosterSource
} from "../app/src/poster-legacy-bridge.js";
import { POSTER_STATE_LOAD_COMMAND } from "../app/src/poster-state-store.js";

const tauriConfig = JSON.parse(readFileSync("src-tauri/tauri.conf.json", "utf8"));
const controlCenterRuntime = readFileSync("app/src/main.js", "utf8");
const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");
const performanceGateRuntime = readFileSync("src-tauri/src/performance_gate.rs", "utf8");
const desktopHostRuntime = readFileSync("src-tauri/src/desktop_host.rs", "utf8");
const posterSurfaceRuntime = readFileSync("app/src/poster-surface.js", "utf8");
const releaseExe = "src-tauri/target/release/DesktopUp.exe";

function windowConfig(label) {
  return tauriConfig.app.windows.find((window) => window.label === label);
}

function memoryStorage() {
  const values = new Map();
  return {
    getItem: (key) => values.has(key) ? values.get(key) : null,
    setItem: (key, value) => values.set(key, String(value)),
    removeItem: (key) => values.delete(key)
  };
}

test("cold start creates only the poster WebView and defers the control center", () => {
  const main = windowConfig("main");
  const poster = windowConfig("poster");

  assert.equal(main, undefined, "control center must not allocate a cold-start WebView");
  assert.ok(poster, "poster surface window must be configured");
  assert.match(nativeRuntime, /WebviewWindowBuilder::new\(/);
  assert.match(nativeRuntime, /CONTROL_CENTER_WINDOW_LABEL/);
  assert.match(
    nativeRuntime,
    /std::thread::spawn(move || run_main_window_action(app, action))/
  );
  assert.match(nativeRuntime, /struct MainWindowCoordinator/);
  assert.match(nativeRuntime, /lifecycle: ControlCenterLifecycle/);
  assert.match(nativeRuntime, /fn mark_main_window_ready/);
  assert.match(nativeRuntime, /fn mark_control_center_ready/);
  assert.equal(
    poster.visible,
    false,
    "poster native window must stay hidden until its first canonical surface handshake"
  );
  assert.equal(poster.title, "", "poster native title must stay empty");
  assert.equal(poster.url, "poster.html", "poster window must load the native poster page");
  assert.equal(poster.transparent, true, "poster surface must be transparent");
  assert.deepEqual(
    poster.backgroundColor,
    [0, 0, 0, 0],
    "poster window and WebView must start with a fully transparent background"
  );
  assert.equal(poster.decorations, false, "poster surface must not use native decorations");
  assert.equal(poster.focus, false, "poster surface must not steal focus");
  assert.equal(poster.focusable, false, "poster surface must not become an active application");
  assert.equal(
    poster.alwaysOnBottom,
    false,
    "the native desktop anchor must own Z-order instead of Tauri window levels"
  );
  assert.equal(poster.closable, false, "poster surface must not be closable by the user");
  assert.equal(poster.skipTaskbar, true, "poster surface must never acquire a taskbar button");
});

test("cold start reveals the poster only after native geometry and CSS presentation are ready", () => {
  assert.match(nativeRuntime, /struct PosterSurfaceLifecycle\(AtomicBool\)/);
  assert.match(nativeRuntime, /fn mark_poster_surface_ready/);
  assert.match(nativeRuntime, /configure_poster_host\(&app, true\)\?/);
  const readinessFinalize = nativeRuntime.match(
    /fn mark_poster_surface_ready[\s\S]*?runtime_monitor::reevaluate_foreground\(\);/
  )?.[0];
  assert.ok(readinessFinalize, "cold-start native finalization path must exist");
  assert.ok(
    readinessFinalize.indexOf("configure_poster_host(&app, true)?")
      < readinessFinalize.indexOf("desktop_host::apply_surface_regions"),
    "the final window region must be applied after SWP_FRAMECHANGED host configuration"
  );
  assert.match(posterSurfaceRuntime, /applyHostPresentation\(result\?\.hostSurface\)/);
  assert.match(posterSurfaceRuntime, /revealInitializedNativeSurface\(result\?\.hostSurface\?\.revision\)/);
});

test("native style changes cannot restore a Windows caption after cold start", () => {
  const desktopHost = readFileSync("src-tauri/src/desktop_host.rs", "utf8");
  assert.match(desktopHost, /WM_STYLECHANGING/);
  assert.match(desktopHost, /strip_non_client_decoration_styles\(style\.styleNew\)/);
});

test("poster drag stays on the desktop layer without entering the Windows system move loop", () => {
  const commandPath = nativeRuntime.match(
    /fn start_poster_drag[\s\S]*?\n}/
  )?.[0];
  assert.ok(commandPath, "the Tauri poster drag command must exist");
  assert.match(commandPath, /desktop_host::start_poster_drag\(&window\)/);
  assert.doesNotMatch(commandPath, /\.start_dragging\(/);

  const nativeDragPath = desktopHostRuntime.match(
    /pub fn start_poster_drag[\s\S]*?fn repair_tool_window_during_shell_transition/
  )?.[0];
  assert.ok(nativeDragPath, "the native desktop-layer drag transaction must exist");
  assert.match(nativeDragPath, /POSTER_DRAG_STATE/);
  assert.match(nativeDragPath, /move_active_poster_drag/);
  assert.match(nativeDragPath, /z_order_anchor/);
  assert.match(nativeDragPath, /SWP_NOSIZE \| SWP_NOACTIVATE \| SWP_ASYNCWINDOWPOS/);
  assert.match(nativeDragPath, /PostMessageW\(Some\(hwnd\), WM_POSTER_DRAG_FINISHED/);

  const mouseHookPath = desktopHostRuntime.match(
    /unsafe extern "system" fn input_mouse_hook[\s\S]*?fn run_input_mouse_hook/
  )?.[0];
  assert.ok(mouseHookPath, "the low-level mouse hook must own native drag movement");
  assert.match(mouseHookPath, /message == WM_MOUSEMOVE[\s\S]*?move_active_poster_drag/);
  assert.match(mouseHookPath, /message == WM_LBUTTONUP[\s\S]*?finish_active_poster_drag/);

  const dragFinishPath = desktopHostRuntime.match(
    /if message == WM_POSTER_DRAG_FINISHED[\s\S]*?return LRESULT\(0\);/
  )?.[0];
  assert.ok(dragFinishPath, "drag completion must restore the poster frame on its window thread");
  assert.match(dragFinishPath, /enforce_poster_window_frame\(hwnd\)/);
  assert.match(desktopHostRuntime, /reapply_existing_window_region\(hwnd\)/);
});

test("collapse and expand preserve the current poster top-left after initial sync", () => {
  const placementStart = nativeRuntime.indexOf("fn apply_host_placement_intent");
  const placementEnd = nativeRuntime.indexOf("fn update_host_surface_diagnostics");
  const placementPath = nativeRuntime.slice(placementStart, placementEnd);
  assert.match(placementPath, /HostPlacementIntent::Preserve/);
  assert.match(placementPath, /if is_initial_surface_sync/);
  assert.match(placementPath, /restore_poster_placement/);
  assert.match(placementPath, /set_position\(current_position\)/);

  const syncStart = nativeRuntime.indexOf("fn apply_host_surface_request");
  const syncEnd = nativeRuntime.indexOf("#[tauri::command]", syncStart);
  const syncPath = nativeRuntime.slice(syncStart, syncEnd);
  const captureIndex = syncPath.indexOf("let current_position = window.outer_position()");
  const resizeIndex = syncPath.indexOf(".set_size(");
  const placementIndex = syncPath.indexOf("apply_host_placement_intent(");
  assert.ok(captureIndex >= 0, "surface resize must capture the current top-left");
  assert.ok(captureIndex < resizeIndex, "top-left must be captured before changing height");
  assert.ok(resizeIndex < placementIndex, "the captured top-left must be restored after resize");
});

test("runtime configuration contains exactly one global transparent desktop host", () => {
  const hostWindows = tauriConfig.app.windows.filter((window) =>
    window.label === "poster" || window.url === "poster.html"
  );
  assert.equal(hostWindows.length, 1, "all posters must share one production host WebView");
  assert.equal(hostWindows[0].label, "poster", "the legacy label is the canonical host label");
  assert.equal(hostWindows[0].transparent, true);
  assert.equal(hostWindows[0].skipTaskbar, true);
});

test("native host size and both region maps share the resolved display scale", () => {
  const regionApply = nativeRuntime.match(
    /desktop_host::apply_surface_regions\([\s\S]*?validated\.effective_scale_factor[\s\S]*?\)\?;/
  )?.[0];
  assert.ok(regionApply, "native visual and input region application path must exist");
  assert.match(regionApply, /&visual_regions/);
  assert.match(regionApply, /&interaction_regions/);
  assert.doesNotMatch(regionApply, /validated\.request\.scale_factor/);
});
test("native host preserves the visual silhouette while blank poster areas pass through", () => {
  const desktopHost = readFileSync("src-tauri/src/desktop_host.rs", "utf8");
  assert.match(desktopHost, /for region in visual_regions/);
  assert.match(desktopHost, /SetWindowRgn\(hwnd, Some\(combined\), true\)/);
  assert.match(desktopHost, /update_input_hit_regions\(input_regions, device_scale_factor\)/);
  assert.match(desktopHost, /SetWindowsHookExW\(WH_MOUSE_LL/);
  assert.match(desktopHost, /WS_EX_TRANSPARENT/);
  assert.match(posterSurfaceRuntime, /collectSinglePosterInteractionRegions/);
  assert.match(posterSurfaceRuntime, /querySelectorAll\([^\n]*data-action=[^\n]*launch/);
  assert.match(posterSurfaceRuntime, /addEventListener\("contextmenu"/);
  assert.match(posterSurfaceRuntime, /event\.preventDefault\(\)/);
});

test("desktop recovery funnels native and manual triggers through one coordinator", () => {
  const desktopHost = readFileSync("src-tauri/src/desktop_host.rs", "utf8");
  assert.match(desktopHost, /recovery_count: u64/);
  assert.match(desktopHost, /last_recovery_trigger: Option<String>/);
  assert.match(desktopHost, /HOST_RECOVERY_PENDING/);
  assert.match(desktopHost, /merge_host_recovery_request\(&mut pending, generation, trigger\)/);
  assert.doesNotMatch(desktopHost, /restore_after_shell_or_display_event/);

  const workerPath = desktopHost.match(
    /fn run_shell_recovery_worker[\s\S]*?unsafe extern "system" fn shell_foreground_event/
  )?.[0];
  assert.ok(workerPath, "the coalescing native recovery worker must exist");
  assert.match(workerPath, /HOST_RECOVERY_GENERATION\.load\(Ordering::Acquire\) == generation/);
  assert.match(workerPath, /handler\(request\.clone\(\)\)/);

  const shellEventPath = desktopHost.match(
    /unsafe extern "system" fn shell_foreground_event[\s\S]*?fn run_shell_foreground_hook/
  )?.[0];
  assert.ok(shellEventPath, "the native Shell event callback must exist");
  assert.match(shellEventPath, /repair_tool_window_during_shell_transition\(\)/);
  assert.ok(
    shellEventPath.indexOf("repair_tool_window_during_shell_transition()")
      < shellEventPath.indexOf("queue_host_recovery(trigger)"),
    "the first desktop-relative repair must run before delayed recovery is queued"
  );
  assert.match(shellEventPath, /is_desktop_shell_window\(hwnd\)/);

  const shellTransitionRepairPath = desktopHost.match(
    /fn repair_tool_window_during_shell_transition[\s\S]*?pub fn repair_shell_host/
  )?.[0];
  assert.ok(shellTransitionRepairPath, "the bounded Shell desktop-layer repair must exist");
  assert.match(shellTransitionRepairPath, /POSTER_VISIBLE_INTENT/);
  assert.match(shellTransitionRepairPath, /TOOL_WINDOW_FALLBACK_ACTIVE/);
  assert.match(shellTransitionRepairPath, /anchor_poster_surface_windows_to_desktop\(\)/);
  assert.doesNotMatch(shellTransitionRepairPath, /SWP_FRAMECHANGED/);
  assert.match(
    desktopHost,
    /SHELL_DESKTOP_LAYER_REPAIR_DELAYS_MS: \[u64; 14\] =[\s\S]*?\[4, 8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48, 64, 96\]/
  );
  assert.match(workerPath, /if pending_host_recovery_includes_shell\(generation\)/);
  assert.match(workerPath, /for repair_at_ms in SHELL_DESKTOP_LAYER_REPAIR_DELAYS_MS/);
  assert.match(workerPath, /repair_tool_window_during_shell_transition\(\)/);
  assert.ok(
    workerPath.lastIndexOf("repair_tool_window_during_shell_transition()")
      < workerPath.indexOf("handler(request.clone())"),
    "the bounded desktop-relative repair burst must finish before coordinated recovery"
  );
  assert.doesNotMatch(desktopHost, /\bHWND_(?:TOPMOST|NOTOPMOST)\b/);
  assert.doesNotMatch(desktopHost, /shell_z_order_guard/i);

  const coordinatorPath = nativeRuntime.match(
    /fn recover_desktop_host[\s\S]*?\n}\r?\n\r?\n#\[tauri::command\]/
  )?.[0];
  assert.ok(coordinatorPath, "the single application recovery coordinator must exist");
  assert.match(coordinatorPath, /configure_poster_host\(app, true\)\?/);
  assert.match(coordinatorPath, /resync_host_surface_for_display_change\(app\)\?/);
  assert.match(coordinatorPath, /last_recovery_trigger = Some\(trigger_summary\)/);
  assert.match(coordinatorPath, /request\.is_shell_only\(\)/);
  assert.match(coordinatorPath, /desktop_host::repair_shell_host/);
  assert.match(coordinatorPath, /last_recovery_action = Some\(recovery_action\.to_string\(\)\)/);
  assert.ok(
    coordinatorPath.indexOf("desktop_host::repair_shell_host")
      < coordinatorPath.indexOf("configure_poster_host(app, true)?"),
    "shell repair must return before full host reconfiguration"
  );

  const shellRepairPath = desktopHost.match(
    /pub fn repair_shell_host[\s\S]*?fn take_pending_host_recovery_request/
  )?.[0];
  assert.ok(shellRepairPath, "the lightweight shell repair path must exist");
  assert.match(shellRepairPath, /anchor_poster_surface_windows_to_desktop/);
  assert.doesNotMatch(shellRepairPath, /SWP_FRAMECHANGED|restore_poster_placement|SetWindowRgn/);
  const fallbackAnchorPath = desktopHost.match(
    /fn fallback_z_order_anchor[\s\S]*?fn anchor_tool_window_to_desktop/
  )?.[0];
  assert.ok(fallbackAnchorPath, "the ToolWindow fallback anchor path must exist");
  assert.match(fallbackAnchorPath, /shell_windows\(\)/);
  assert.match(fallbackAnchorPath, /window\.is_progman/);
  assert.doesNotMatch(desktopHost, /FindWindowW/);

  const scalePath = nativeRuntime.match(
    /WindowEvent::ScaleFactorChanged[\s\S]*?WindowEvent::Destroyed/
  )?.[0];
  assert.ok(scalePath, "the Tauri scale-factor recovery route must exist");
  assert.match(scalePath, /request_host_recovery/);
  assert.doesNotMatch(scalePath, /restore_poster_window_placement/);

  const reconnectPath = nativeRuntime.match(
    /fn reconnect_desktop_host[\s\S]*?\n}/
  )?.[0];
  assert.ok(reconnectPath, "the manual reconnect route must exist");
  assert.match(reconnectPath, /recover_desktop_host/);
  assert.match(reconnectPath, /HostRecoveryTrigger::ManualReconnect/);
});

test("runtime lifecycle uses one lock-safe event and state-first visibility path", () => {
  const lifecycleRuntime = readFileSync("src-tauri/src/runtime_lifecycle.rs", "utf8");
  assert.match(nativeRuntime, /mod runtime_lifecycle;/);
  assert.match(nativeRuntime, /runtime_lifecycle: RuntimeLifecycleSnapshot/);
  assert.match(nativeRuntime, /app\.emit\("runtime-state-changed", snapshot\.clone\(\)\)/);
  assert.match(lifecycleRuntime, /event_count = self\.event_count\.saturating_add\(1\)/);
  assert.match(lifecycleRuntime, /self\.generation = self\.generation\.saturating_add\(1\)/);

  const mutationPath = nativeRuntime.match(
    /fn mutate_runtime_state[\s\S]*?\n}\r?\n\r?\nfn publish_runtime_snapshot/
  )?.[0];
  assert.ok(mutationPath, "runtime mutation must have one lock-only helper");
  assert.doesNotMatch(mutationPath, /publish_diagnostics|schedule_runtime_outputs|run_on_main_thread/);

  const hostSurfacePath = nativeRuntime.match(
    /fn apply_host_surface_request[\s\S]*?fn sync_host_surface/
  )?.[0];
  assert.ok(
    hostSurfacePath.indexOf("inspect_host_topology(app)")
      < hostSurfacePath.lastIndexOf("runtime_state.0.lock()"),
    "host topology inspection must precede the RuntimeState write lock"
  );
  const configurePath = nativeRuntime.match(
    /fn configure_poster_host[\s\S]*?fn recover_desktop_host/
  )?.[0];
  assert.ok(
    configurePath.indexOf("resolve_display_topology(&window)")
      < configurePath.indexOf("state.0.lock()")
  );
  assert.ok(
    configurePath.indexOf("inspect_host_topology(app)")
      < configurePath.indexOf("state.0.lock()")
  );
  assert.match(
    nativeRuntime,
    /let host_topology = inspect_host_topology\(app\.handle\(\)\);[\s\S]*?snapshot\.host_topology = host_topology/
  );

  const visibilityPath = nativeRuntime.match(
    /fn set_poster_visibility[\s\S]*?\n}\r?\n\r?\nfn reset_poster_surface_initialization/
  )?.[0];
  assert.ok(visibilityPath, "poster visibility transition must exist");
  assert.ok(
    visibilityPath.indexOf("mutate_runtime_state")
      < visibilityPath.indexOf("apply_native_poster_visibility"),
    "poster visibility state must commit before native show or hide"
  );
  const nativeVisibilityPath = nativeRuntime.match(
    /fn apply_native_poster_visibility[\s\S]*?\n}\r?\n\r?\nfn set_poster_visibility/
  )?.[0];
  assert.ok(nativeVisibilityPath, "native poster visibility path must exist");
  assert.match(nativeVisibilityPath, /desktop_host::show_poster_at_desktop_layer\(&window\)/);
  assert.doesNotMatch(nativeVisibilityPath, /window\.show\(\)/);
  const nativeShowPath = desktopHostRuntime.match(
    /pub fn show_poster_at_desktop_layer[\s\S]*?pub fn repair_shell_host/
  )?.[0];
  assert.ok(nativeShowPath, "the atomic native poster show path must exist");
  assert.match(nativeShowPath, /ShowWindow\(hwnd, SW_SHOWNOACTIVATE\)/);
  assert.match(nativeShowPath, /anchor_poster_surface_windows_to_desktop\(\)/);
  assert.match(nativeShowPath, /settle_poster_desktop_layer_after_show\(\)/);
  assert.ok(nativeShowPath.indexOf("ShowWindow") < nativeShowPath.indexOf("anchor_poster_surface_windows_to_desktop"));
  const layerSettlePath = desktopHostRuntime.match(
    /fn settle_poster_desktop_layer_after_show[\s\S]*?pub fn show_poster_at_desktop_layer/
  )?.[0];
  assert.ok(layerSettlePath, "the bounded post-show desktop-layer settle path must exist");
  assert.match(desktopHostRuntime, /POST_SHOW_DESKTOP_LAYER_SETTLE_DELAYS_MS: \[u64; 3\] = \[16, 48, 96\]/);
  assert.match(layerSettlePath, /for delay_ms in POST_SHOW_DESKTOP_LAYER_SETTLE_DELAYS_MS/);
  assert.match(layerSettlePath, /anchor_poster_surface_windows_to_desktop\(\)/);
  assert.doesNotMatch(layerSettlePath, /POSTER_VISIBLE_INTENT\.load|TOOL_WINDOW_FALLBACK_ACTIVE\.load/);
  const posterWindowsPath = desktopHostRuntime.match(
    /unsafe extern "system" fn collect_poster_surface_window[\s\S]*?fn fallback_z_order_anchor/
  )?.[0];
  assert.ok(posterWindowsPath, "the native poster surface window set must have one owner");
  assert.match(posterWindowsPath, /GetCurrentProcessId\(\)/);
  assert.match(posterWindowsPath, /IsWindowVisible\(hwnd\)/);
  assert.match(posterWindowsPath, /WS_EX_TOOLWINDOW/);
  assert.match(posterWindowsPath, /WS_EX_NOACTIVATE/);
  assert.match(posterWindowsPath, /tray_icon_app/);
  assert.match(posterWindowsPath, /Tao Thread Event Target/);
  assert.match(nativeShowPath, /TOOL_WINDOW_FALLBACK_ACTIVE\.store\(true, Ordering::Release\)/);
  assert.doesNotMatch(layerSettlePath, /while\s*\(/);

  const quitPath = nativeRuntime.match(
    /fn prepare_runtime_exit[\s\S]*?\n\}/
  )?.[0];
  assert.ok(quitPath, "unified runtime exit preparation must exist");
  const orderedExitSteps = [
    "begin_runtime_exit",
    "desktop_host::begin_exit",
    "taskbar_recovery::begin_exit",
    "store.flush",
    "window.destroy",
    "remove_tray_icon"
  ].map((step) => quitPath.indexOf(step));
  assert.ok(orderedExitSteps.every((index) => index >= 0));
  assert.deepEqual([...orderedExitSteps].sort((a, b) => a - b), orderedExitSteps);
  assert.match(nativeRuntime, /"quit" => request_runtime_exit\(app, "trayExit", 0\)/);
  assert.match(nativeRuntime, /tauri::RunEvent::ExitRequested/);
  assert.match(posterSurfaceRuntime, /listen\("runtime-state-changed"/);
  assert.doesNotMatch(posterSurfaceRuntime, /listen\("runtime-pause-state"/);
});

test("static paused and stopped surfaces do not keep a render polling loop", () => {
  assert.doesNotMatch(posterSurfaceRuntime, /setInterval\(/);
  assert.doesNotMatch(controlCenterRuntime, /setInterval\(/);
  assert.match(posterSurfaceRuntime, /function shouldSampleFps\(\)[\s\S]*?runtimeMode/);
  assert.match(
    posterSurfaceRuntime,
    /function sampleFrame[\s\S]*?if \(!shouldSampleFps\(\)\)[\s\S]*?stopFpsSampling\(\)/
  );
  assert.match(posterSurfaceRuntime, /cancelAnimationFrame\(fpsRafId\)/);
  assert.match(posterSurfaceRuntime, /ACTIVE_ANIMATION_FRAME_INTERVAL_MS = 1000 \/ 30/);
  assert.match(posterSurfaceRuntime, /clearTimeout\(fpsTimerId\)/);
  assert.match(controlCenterRuntime, /PERFORMANCE_SAMPLE_COUNT = 6/);
  assert.match(controlCenterRuntime, /PERFORMANCE_MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES = 50 \* 1024 \* 1024/);
  assert.match(controlCenterRuntime, /PERFORMANCE_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES = 256 \* 1024 \* 1024/);
});

test("release performance gate only starts from an explicit diagnostic argument", () => {
  assert.match(performanceGateRuntime, /PERFORMANCE_GATE_ARGUMENT: &str = "--cdrp-performance-gate"/);
  assert.match(nativeRuntime, /argument == PERFORMANCE_GATE_ARGUMENT/);
  assert.match(nativeRuntime, /fn performance_probe_requested\(\)/);
  assert.match(nativeRuntime, /fn start_release_performance_probe/);
  assert.match(nativeRuntime, /performance-baseline-v4\.json/);
  assert.match(nativeRuntime, /PERFORMANCE_STABILIZATION_MIN_MS/);
  assert.match(nativeRuntime, /private_working_set_bytes/);
  assert.match(nativeRuntime, /process_tree_private_working_set_bytes/);
  assert.match(nativeRuntime, /process_tree_measurement_complete/);
  assert.match(
    nativeRuntime,
    /request_runtime_exit\([\s\S]*"performanceGateExit"[\s\S]*if report\.passed \{ 0 \} else \{ 2 \}/
  );
});
test("release executable uses the Windows GUI subsystem", { skip: !existsSync(releaseExe) }, () => {
  const pe = readFileSync(releaseExe);
  const peOffset = pe.readUInt32LE(0x3c);
  assert.equal(pe.toString("ascii", peOffset, peOffset + 4), "PE\0\0", "invalid PE signature");
  const optionalOffset = peOffset + 4 + 20;
  assert.equal(
    pe.readUInt16LE(optionalOffset + 68),
    2,
    "release executable must use the Windows GUI subsystem"
  );
});

test("SQLite is authoritative while the legacy browser source remains read-only", () => {
  const storage = memoryStorage();
  assert.equal(POSTER_STATE_LOAD_COMMAND, "load_poster_state");
  assert.match(nativeRuntime, /PosterStateRepository::open/);
  assert.match(nativeRuntime, /fn load_poster_state/);
  assert.match(nativeRuntime, /fn replace_poster_state/);
  assert.match(nativeRuntime, /fn migrate_poster_state/);
  assert.doesNotMatch(nativeRuntime, /save_poster_snapshot/);

  assert.deepEqual(
    inspectLocalLegacyPosterSource(storage),
    { status: "noData" },
    "an absent legacy browser source must remain distinguishable from rejection"
  );

  storage.setItem(LEGACY_POSTER_STORAGE_KEY, "{");
  const corruptSource = inspectLocalLegacyPosterSource(storage);
  assert.equal(corruptSource.status, "rejected");
  assert.equal(corruptSource.code, "corruptJson");

  const rawLegacy = JSON.stringify({
    selectedPosterId: "night-ops",
    posters: [{ id: "night-ops", title: "NIGHT OPS" }]
  });
  storage.setItem(LEGACY_POSTER_STORAGE_KEY, rawLegacy);
  const legacySource = inspectLocalLegacyPosterSource(storage);
  assert.equal(legacySource.status, "loaded");
  assert.equal(legacySource.kind, "legacyA0");
  assert.equal(legacySource.snapshot.posters[0].title, "NIGHT OPS");
  assert.equal(storage.getItem(LEGACY_POSTER_STORAGE_KEY), rawLegacy, "inspection must not rewrite the source");
});

test("poster core renders the native surface without demo controls", () => {
  const markup = buildPosterMarkup({
    title: "NIGHT OPS",
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    collapsed: false,
    locked: true,
    items: [{ title: "AFTERLIGHT", mark: "AL", hue: "cyan" }]
  }, { surface: "native" });

  assert.match(markup, /data-native-drag/);
  assert.match(markup, /data-action="lock"/);
  assert.match(markup, /data-action="collapse"/);
  assert.doesNotMatch(markup, /data-resize-handle/);
  assert.doesNotMatch(markup, /data-poster-id=/);
});

test("poster core renders the demo surface with geometry and edit handles", () => {
  const markup = buildPosterMarkup({
    id: "night-ops",
    x: 72,
    y: 154,
    width: 490,
    height: 392,
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    collapsed: false,
    locked: false,
    items: []
  }, {
    surface: "demo",
    posterId: "night-ops",
    posterIndex: 0,
    isSelected: true
  });

  assert.match(markup, /data-poster-id="night-ops"/);
  assert.match(markup, /data-drag-handle/);
  assert.match(markup, /data-resize-handle/);
  assert.match(markup, /--poster-width:490px/);
  assert.match(markup, /is-selected/);
});
