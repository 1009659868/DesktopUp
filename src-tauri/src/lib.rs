pub mod autostart_contract;
#[cfg(feature = "autostart-test-root")]
mod autostart_test_gate;
mod control_center_lifecycle;
mod desktop_host;
mod host_contract;
mod host_topology;
pub mod install_baseline;
pub mod install_paths;
pub mod install_probe;
pub mod launch_intent;
mod performance_gate;
mod placement;
mod poster_contract;
pub mod poster_geometry;
pub mod poster_permissions;
mod poster_snapshot;
mod poster_state_recovery_gate;
pub mod poster_state_store;
pub mod poster_template_resolution;
mod runtime_lifecycle;
mod runtime_monitor;
mod taskbar_recovery;

use std::sync::{
    atomic::{AtomicBool, AtomicU64, Ordering},
    Mutex,
};
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use autostart_contract::{
    probe_autostart, AutostartChangeResult, AutostartCoordinator, AutostartDiagnostics,
    AUTOSTART_STATE_EVENT,
};
use control_center_lifecycle::{ControlCenterLifecycle, ControlCenterLifecycleSnapshot};
use desktop_host::{DesktopHostDiagnostics, HostRecoveryRequest, HostRecoveryTrigger};
use host_contract::{
    virtual_bounds, DisplayDescriptor, DisplayTopology, HostPlacementIntent, HostSurfaceRequest,
    HostSurfaceSnapshot, InteractionRegion, LogicalRect, LogicalSize, PhysicalRect,
    ResponsiveScalePolicy, HOST_CONTRACT_VERSION,
};
use host_topology::{
    desktop_host_window, effective_logical_work_area, inspect_host_topology,
    optional_desktop_host_window, responsive_scale_for_work_area, HostTopologySnapshot,
    CONTROL_CENTER_WINDOW_LABEL, DESKTOP_HOST_WINDOW_LABEL,
};
use install_paths::{InstallPathDiagnostics, RuntimePathResolution};
use install_probe::{probe_install_content, InstallContentProbe};
use launch_intent::{
    decide_initial_from_env, decide_secondary_forwarded_arguments, LaunchIntentCoordinator,
    LaunchIntentDiagnostics, LaunchIntentDisposition, INVALID_LAUNCH_EXIT_CODE,
};
use performance_gate::{
    evaluate_performance_samples, PerformanceGateReport, PerformanceSample,
    PERFORMANCE_GATE_ARGUMENT, PERFORMANCE_GATE_CONTRACT, PERFORMANCE_SAMPLE_COUNT,
    PERFORMANCE_STABILIZATION_MIN_MS,
};
use placement::PlacementStore;
use poster_snapshot::{PosterSnapshotLoadResult, PosterSnapshotStore};
use poster_state_store::{
    LegacyPosterMigrationRequest, LegacyPosterMigrationResult, PosterStateLoadResult,
    PosterStateMutationResult, PosterStateRepository, PosterStateStoreError,
};
use runtime_lifecycle::{
    RuntimeLifecycleInput, RuntimeLifecycleSnapshot, RuntimeMode, RuntimePhase,
};
use runtime_monitor::{RuntimeMonitorCapabilities, RuntimeVisibilitySnapshot};
use serde::Serialize;
use taskbar_recovery::{TaskbarRecoveryBatch, TaskbarRecoveryLifecycle, TaskbarRecoverySnapshot};
use tauri::{
    menu::{
        CheckMenuItem, CheckMenuItemBuilder, Menu, MenuBuilder, MenuItem, MenuItemBuilder,
        PredefinedMenuItem, SubmenuBuilder,
    },
    tray::{MouseButton, TrayIconBuilder, TrayIconEvent},
    webview::PageLoadEvent,
    Emitter, Manager, State, WindowEvent,
};

static POSTER_PLACEMENT_CAPTURE_ENABLED: AtomicBool = AtomicBool::new(false);
const TRAY_ICON_ID: &str = "desktop-up-tray";
const TRAY_TOOLTIP: &str = "Desktop-Up · 游戏海报桌面";

struct PerformanceClock(Instant);
#[derive(Default)]
struct HostRecoveryCoordinator {
    execution: Mutex<()>,
    completed_count: AtomicU64,
}

#[derive(Default)]
struct MainWindowCoordinator {
    execution: Mutex<()>,
    lifecycle: ControlCenterLifecycle,
}

struct RuntimeState(Mutex<RuntimeSnapshot>);

struct TraySurface {
    menu: Menu<tauri::Wry>,
    execution: Mutex<()>,
}

#[derive(Default)]
struct HostSurfaceCoordinator(Mutex<()>);

#[derive(Default)]
struct PosterSurfaceLifecycle(AtomicBool);

impl Default for RuntimeState {
    fn default() -> Self {
        Self(Mutex::new(RuntimeSnapshot::default()))
    }
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct RuntimeSnapshot {
    contract_version: u32,
    edit_mode: bool,
    manually_paused: bool,
    automatically_paused: bool,
    effective_paused: bool,
    all_locked: bool,
    poster_visible: bool,
    host_surface_ready: bool,
    pause_reasons: Vec<String>,
    poster_fps: f64,
    performance_mode: String,
    host_surface: HostSurfaceSnapshot,
    display_topology: DisplayTopology,
    host_topology: HostTopologySnapshot,
    desktop_host: DesktopHostDiagnostics,
    runtime_visibility: RuntimeVisibilitySnapshot,
    runtime_monitor: RuntimeMonitorCapabilities,
    runtime_lifecycle: RuntimeLifecycleSnapshot,
    control_center: ControlCenterLifecycleSnapshot,
    taskbar_recovery: TaskbarRecoverySnapshot,
    #[serde(skip)]
    exit_requested: bool,
}

impl Default for RuntimeSnapshot {
    fn default() -> Self {
        Self {
            contract_version: HOST_CONTRACT_VERSION,
            edit_mode: false,
            manually_paused: false,
            automatically_paused: false,
            effective_paused: false,
            all_locked: false,
            poster_visible: true,
            host_surface_ready: false,
            pause_reasons: Vec::new(),
            poster_fps: 0.0,
            performance_mode: "auto".to_string(),
            host_surface: HostSurfaceSnapshot::default(),
            display_topology: DisplayTopology::default(),
            host_topology: HostTopologySnapshot::default(),
            desktop_host: DesktopHostDiagnostics::default(),
            runtime_visibility: RuntimeVisibilitySnapshot::default(),
            runtime_monitor: RuntimeMonitorCapabilities::default(),
            runtime_lifecycle: RuntimeLifecycleSnapshot::default(),
            control_center: ControlCenterLifecycleSnapshot::default(),
            taskbar_recovery: TaskbarRecoverySnapshot::default(),
            exit_requested: false,
        }
    }
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct HostSurfaceSyncResult {
    contract_version: u32,
    host_surface: HostSurfaceSnapshot,
    display_topology: DisplayTopology,
    host_topology: HostTopologySnapshot,
    desktop_host: DesktopHostDiagnostics,
}

impl HostSurfaceSyncResult {
    fn from_runtime(snapshot: &RuntimeSnapshot) -> Self {
        Self {
            contract_version: HOST_CONTRACT_VERSION,
            host_surface: snapshot.host_surface.clone(),
            display_topology: snapshot.display_topology.clone(),
            host_topology: snapshot.host_topology.clone(),
            desktop_host: snapshot.desktop_host.clone(),
        }
    }
}

impl RuntimeSnapshot {
    fn refresh_runtime_lifecycle(&mut self, trigger: &str) -> bool {
        self.automatically_paused = self.runtime_visibility.automatically_paused;
        self.pause_reasons = self.runtime_visibility.pause_reasons.clone();
        self.effective_paused = self.manually_paused || self.automatically_paused;
        let input = RuntimeLifecycleInput {
            manually_paused: self.manually_paused,
            automatically_paused: self.automatically_paused,
            poster_visible: self.poster_visible,
            host_surface_ready: self.host_surface_ready,
            exit_requested: self.exit_requested,
            pause_reasons: self.pause_reasons.clone(),
            performance_mode: self.performance_mode.clone(),
            poster_fps: self.poster_fps,
        };
        self.runtime_lifecycle.reduce(input, trigger)
    }

    fn pause_state(&self) -> String {
        self.runtime_lifecycle.mode.as_str().to_string()
    }

    fn runtime_status_label(&self) -> String {
        match (self.runtime_lifecycle.phase, self.runtime_lifecycle.mode) {
            (RuntimePhase::Starting, _) => "运行状态：宿主正在初始化".to_string(),
            (RuntimePhase::Exiting, _) => "运行状态：正在退出".to_string(),
            (RuntimePhase::Running, RuntimeMode::Stopped) if !self.poster_visible => {
                "运行状态：海报已隐藏".to_string()
            }
            (RuntimePhase::Running, RuntimeMode::Stopped) => "运行状态：已停止".to_string(),
            (RuntimePhase::Running, RuntimeMode::Paused) => {
                self.runtime_visibility.tray_label(self.manually_paused)
            }
            (RuntimePhase::Running, RuntimeMode::ActiveAnimated) => {
                "运行状态：动态渲染".to_string()
            }
            (RuntimePhase::Running, RuntimeMode::ActiveStatic) => {
                "运行状态：静态按需绘制".to_string()
            }
        }
    }
}

struct TrayControls {
    host_status: MenuItem<tauri::Wry>,
    runtime_status: MenuItem<tauri::Wry>,
    workspace: CheckMenuItem<tauri::Wry>,
    edit: CheckMenuItem<tauri::Wry>,
    lock_all: CheckMenuItem<tauri::Wry>,
    pause: CheckMenuItem<tauri::Wry>,
    autostart: CheckMenuItem<tauri::Wry>,
}

impl TrayControls {
    fn sync(&self, snapshot: &RuntimeSnapshot) {
        let _ = self
            .host_status
            .set_text(snapshot.desktop_host.tray_label());
        let _ = self
            .runtime_status
            .set_text(snapshot.runtime_status_label());
        let _ = self.workspace.set_checked(snapshot.poster_visible);
        let _ = self.edit.set_checked(snapshot.edit_mode);
        let _ = self.lock_all.set_checked(snapshot.all_locked);
        let _ = self.pause.set_checked(snapshot.manually_paused);
    }

    fn sync_autostart(&self, snapshot: &AutostartDiagnostics) {
        let _ = self.autostart.set_checked(snapshot.effective_enabled);
        let _ = self
            .autostart
            .set_enabled(autostart_control_available(snapshot));
        let _ = self.autostart.set_text(autostart_tray_label(snapshot));
    }
}

fn build_tray_icon(app: &tauri::AppHandle, menu: &Menu<tauri::Wry>) -> tauri::Result<()> {
    TrayIconBuilder::with_id(TRAY_ICON_ID)
        .tooltip(TRAY_TOOLTIP)
        .menu(menu)
        .show_menu_on_left_click(false)
        .build(app)
        .map(|_| ())
}

fn publish_taskbar_recovery_snapshot(app: &tauri::AppHandle) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    if let Ok(snapshot) = state.0.lock().map(|snapshot| snapshot.clone()) {
        publish_runtime_snapshot(app, snapshot);
    };
}

fn reattach_taskbar_listener_on_main_thread(app: &tauri::AppHandle) -> Result<(), String> {
    let (sender, receiver) = std::sync::mpsc::sync_channel(1);
    app.run_on_main_thread(move || {
        let _ = sender.send(taskbar_recovery::reattach_listener());
    })
    .map_err(|error| error.to_string())?;
    receiver.recv().map_err(|error| error.to_string())?
}

fn rebuild_unique_tray_icon(app: &tauri::AppHandle) -> Result<(), String> {
    let surface = app.state::<TraySurface>();
    let _guard = surface
        .execution
        .lock()
        .map_err(|error| error.to_string())?;
    let registration_ready = app
        .tray_by_id(TRAY_ICON_ID)
        .ok_or_else(|| "the DesktopUp tray icon is absent".to_string())
        .and_then(|tray| {
            tray.set_tooltip(Some(TRAY_TOOLTIP))
                .map_err(|error| error.to_string())
        });
    if registration_ready.is_err() {
        drop(app.remove_tray_by_id(TRAY_ICON_ID));
        build_tray_icon(app, &surface.menu).map_err(|error| error.to_string())?;
        reattach_taskbar_listener_on_main_thread(app)?;
    }
    if let Some(runtime) = app.try_state::<RuntimeState>() {
        if let Ok(snapshot) = runtime.0.lock().map(|snapshot| snapshot.clone()) {
            if let Some(controls) = app.try_state::<TrayControls>() {
                controls.sync(&snapshot);
            }
        }
    }
    if let (Some(controls), Some(autostart)) = (
        app.try_state::<TrayControls>(),
        app.try_state::<AutostartCoordinator>(),
    ) {
        controls.sync_autostart(&autostart.snapshot());
    }
    Ok(())
}

fn remove_tray_icon(app: &tauri::AppHandle) {
    if let Some(surface) = app.try_state::<TraySurface>() {
        if let Ok(_guard) = surface.execution.lock() {
            drop(app.remove_tray_by_id(TRAY_ICON_ID));
            return;
        }
    }
    drop(app.remove_tray_by_id(TRAY_ICON_ID));
}

fn recover_after_taskbar_created(
    app: &tauri::AppHandle,
    batch: TaskbarRecoveryBatch,
) -> Result<(), String> {
    rebuild_unique_tray_icon(app)?;
    recover_desktop_host(
        app,
        HostRecoveryRequest::taskbar_created(batch.generation as usize, batch.event_count as usize),
    )?;
    Ok(())
}

fn reconnect_desktop_host_from_ui(app: &tauri::AppHandle) {
    let result = recover_desktop_host(
        app,
        HostRecoveryRequest::single(HostRecoveryTrigger::ManualReconnect),
    )
    .map(|_| ());
    if let Some(lifecycle) = app.try_state::<TaskbarRecoveryLifecycle>() {
        let _ = lifecycle.record_manual_reconnect(&result);
    }
    publish_taskbar_recovery_snapshot(app);
    if let Err(error) = result {
        eprintln!("failed to reconnect desktop host: {error}");
    }
}

fn handle_tray_menu_event(app: &tauri::AppHandle, id: &str) {
    match id {
        "show" => show_main_window(app),
        "settings" => show_and_emit(app, "open-settings"),
        "workspace" => toggle_poster_window(app),
        "edit" => toggle_edit_mode(app),
        "lock-all" => toggle_all_poster_locks(app),
        "pause" => toggle_manual_pause(app),
        "theme-nocturne" => apply_quick_theme(app, "nocturne"),
        "theme-ember" => apply_quick_theme(app, "ember"),
        "theme-glass" => apply_quick_theme(app, "glass"),
        "agent" => show_and_emit(app, "open-agent"),
        "autostart" => toggle_autostart(app),
        "reconnect" => reconnect_desktop_host_from_ui(app),
        "quit" => request_runtime_exit(app, "trayExit", 0),
        _ => {}
    }
}

fn handle_tray_icon_event(app: &tauri::AppHandle, event: &TrayIconEvent) {
    if event.id().as_ref() != TRAY_ICON_ID {
        return;
    }
    if matches!(
        event,
        TrayIconEvent::DoubleClick {
            button: MouseButton::Left,
            ..
        }
    ) {
        toggle_main_window(app);
    }
}
fn autostart_control_available(snapshot: &AutostartDiagnostics) -> bool {
    snapshot.registration_eligible
        && !matches!(
            snapshot.registration_state.as_str(),
            "unreadable" | "unsupported"
        )
}

fn autostart_tray_label(snapshot: &AutostartDiagnostics) -> &'static str {
    if !snapshot.registration_eligible {
        "开机自启（仅正式安装版）"
    } else {
        match snapshot.registration_state.as_str() {
            "unreadable" | "unsupported" => "开机自启（状态不可读）",
            "drifted" => "开机自启（配置需修复）",
            _ => "开机自启",
        }
    }
}

fn publish_autostart_snapshot(app: &tauri::AppHandle, snapshot: AutostartDiagnostics) {
    let ui_app = app.clone();
    if let Err(error) = app.run_on_main_thread(move || {
        if let Some(tray) = ui_app.try_state::<TrayControls>() {
            tray.sync_autostart(&snapshot);
        }
        let _ = ui_app.emit(AUTOSTART_STATE_EVENT, snapshot);
    }) {
        eprintln!("failed to schedule autostart UI synchronization: {error}");
    }
}

fn toggle_autostart(app: &tauri::AppHandle) {
    let Some(paths) = app.try_state::<RuntimePathResolution>() else {
        return;
    };
    let Some(coordinator) = app.try_state::<AutostartCoordinator>() else {
        return;
    };
    let result = coordinator.toggle(paths.inner());
    publish_autostart_snapshot(app, result.after.clone());
    if !result.succeeded {
        eprintln!(
            "Desktop-Up autostart toggle rejected: {} {}",
            result.outcome, result.error
        );
    }
}

fn emit_runtime_state_changed(app: &tauri::AppHandle, snapshot: &RuntimeSnapshot) {
    let _ = app.emit("runtime-state-changed", snapshot.clone());
    // Compatibility event for older A0 consumers. Both names carry the same snapshot.
    let _ = app.emit("runtime-pause-state", snapshot.clone());
}

fn enrich_control_center_snapshot(
    coordinator: &MainWindowCoordinator,
    snapshot: &mut RuntimeSnapshot,
) {
    if let Ok(control_center) = coordinator.lifecycle.snapshot() {
        snapshot.control_center = control_center;
    }
}

fn enrich_taskbar_recovery_snapshot(
    lifecycle: &TaskbarRecoveryLifecycle,
    snapshot: &mut RuntimeSnapshot,
) {
    if let Ok(taskbar_recovery) = lifecycle.snapshot() {
        snapshot.taskbar_recovery = taskbar_recovery;
    }
}

fn schedule_runtime_outputs(app: &tauri::AppHandle, snapshot: RuntimeSnapshot, emit_state: bool) {
    let ui_app = app.clone();
    if let Err(error) = app.run_on_main_thread(move || {
        let mut snapshot = snapshot;
        if let Some(coordinator) = ui_app.try_state::<MainWindowCoordinator>() {
            enrich_control_center_snapshot(coordinator.inner(), &mut snapshot);
        }
        if let Some(taskbar) = ui_app.try_state::<TaskbarRecoveryLifecycle>() {
            enrich_taskbar_recovery_snapshot(taskbar.inner(), &mut snapshot);
        }
        if let Some(tray) = ui_app.try_state::<TrayControls>() {
            tray.sync(&snapshot);
        }
        if emit_state {
            emit_runtime_state_changed(&ui_app, &snapshot);
        }
    }) {
        eprintln!("failed to schedule runtime UI synchronization: {error}");
    }
}

fn mutate_runtime_state(
    state: &RuntimeState,
    trigger: &str,
    mutate: impl FnOnce(&mut RuntimeSnapshot),
) -> Result<RuntimeSnapshot, String> {
    let mut snapshot = state.0.lock().map_err(|error| error.to_string())?;
    mutate(&mut snapshot);
    snapshot.refresh_runtime_lifecycle(trigger);
    Ok(snapshot.clone())
}

fn publish_runtime_snapshot(app: &tauri::AppHandle, snapshot: RuntimeSnapshot) {
    runtime_monitor::publish_diagnostics(&snapshot.runtime_visibility, snapshot.manually_paused);
    schedule_runtime_outputs(app, snapshot, true);
}

fn apply_runtime_visibility(app: &tauri::AppHandle, visibility: RuntimeVisibilitySnapshot) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    if let Ok(updated) = mutate_runtime_state(&state, "runtimeVisibility", move |snapshot| {
        snapshot.runtime_visibility = visibility;
    }) {
        publish_runtime_snapshot(app, updated);
    }
}

fn set_runtime_monitor_capabilities(
    app: &tauri::AppHandle,
    capabilities: RuntimeMonitorCapabilities,
) {
    runtime_monitor::publish_capabilities(&capabilities);
    if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(updated) =
            mutate_runtime_state(&state, "runtimeMonitorCapabilities", move |snapshot| {
                snapshot.runtime_monitor = capabilities;
            })
        {
            publish_runtime_snapshot(app, updated);
        }
    }
}

fn toggle_manual_pause(app: &tauri::AppHandle) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    if let Ok(updated) = mutate_runtime_state(&state, "trayManualPause", |snapshot| {
        snapshot.manually_paused = !snapshot.manually_paused;
    }) {
        publish_runtime_snapshot(app, updated);
    }
}

fn toggle_edit_mode(app: &tauri::AppHandle) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    if let Ok(updated) = mutate_runtime_state(state.inner(), "trayEditMode", |snapshot| {
        snapshot.edit_mode = !snapshot.edit_mode;
    }) {
        publish_runtime_snapshot(app, updated);
    }
}

#[tauri::command]
fn update_runtime_state(
    edit_mode: bool,
    manually_paused: bool,
    all_locked: bool,
    performance_mode: String,
    app: tauri::AppHandle,
    state: State<'_, RuntimeState>,
) -> Result<RuntimeSnapshot, String> {
    let updated = mutate_runtime_state(state.inner(), "controlCenterState", move |snapshot| {
        snapshot.edit_mode = edit_mode;
        snapshot.manually_paused = manually_paused;
        snapshot.all_locked = all_locked;
        if matches!(
            performance_mode.as_str(),
            "auto" | "activeStatic" | "activeAnimated" | "paused" | "stopped"
        ) {
            snapshot.performance_mode = performance_mode;
            if snapshot.performance_mode != "activeAnimated" {
                snapshot.poster_fps = 0.0;
            }
        }
    })?;
    publish_runtime_snapshot(&app, updated.clone());
    Ok(updated)
}

#[tauri::command]
fn inspect_poster_snapshot(store: State<'_, PosterSnapshotStore>) -> PosterSnapshotLoadResult {
    store.inspect()
}

#[tauri::command]
fn load_poster_state(
    repository: State<'_, PosterStateRepository>,
) -> Result<PosterStateLoadResult, PosterStateStoreError> {
    repository.load()
}

fn all_posters_locked(snapshot: &serde_json::Value) -> bool {
    snapshot
        .get("posters")
        .and_then(serde_json::Value::as_array)
        .filter(|posters| !posters.is_empty())
        .map(|posters| {
            posters.iter().all(|poster| {
                poster
                    .get("isLocked")
                    .and_then(serde_json::Value::as_bool)
                    .unwrap_or(false)
            })
        })
        .unwrap_or(false)
}

fn sync_poster_runtime_summary(
    app: &tauri::AppHandle,
    snapshot: &serde_json::Value,
    trigger: &str,
) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    let all_locked = all_posters_locked(snapshot);
    if let Ok(updated) = mutate_runtime_state(state.inner(), trigger, move |runtime| {
        runtime.all_locked = all_locked;
    }) {
        publish_runtime_snapshot(app, updated);
    }
}

fn emit_poster_state_update(app: &tauri::AppHandle, snapshot: &serde_json::Value) {
    sync_poster_runtime_summary(app, snapshot, "posterStateChanged");
    if let Err(error) = app.emit("poster-state-updated", snapshot.clone()) {
        eprintln!("failed to emit poster state update: {error}");
    }
}

fn mutate_poster_state_with_cas(
    repository: &PosterStateRepository,
    mut mutate: impl FnMut(&mut serde_json::Value) -> Result<bool, String>,
) -> Result<Option<serde_json::Value>, String> {
    for _ in 0..3 {
        let (revision, mut snapshot) = match repository.load().map_err(|error| error.to_string())? {
            PosterStateLoadResult::Loaded { revision, snapshot } => (revision, snapshot),
            PosterStateLoadResult::NoData => return Err("poster state is unavailable".to_string()),
            PosterStateLoadResult::Rejected { code, message } => {
                return Err(format!("{}: {message}", code.as_str()))
            }
        };
        if !mutate(&mut snapshot)? {
            return Ok(None);
        }
        match repository
            .replace_with_expected_revision(Some(revision), snapshot)
            .map_err(|error| error.to_string())?
        {
            PosterStateMutationResult::Stored { snapshot, .. } => return Ok(Some(snapshot)),
            PosterStateMutationResult::Conflict { .. } => continue,
            PosterStateMutationResult::Rejected { code, message } => {
                return Err(format!("{}: {message}", code.as_str()))
            }
        }
    }
    Err("poster state changed repeatedly while applying tray action".to_string())
}

fn toggle_all_poster_locks_snapshot(snapshot: &mut serde_json::Value) -> Result<bool, String> {
    let posters = snapshot
        .get_mut("posters")
        .and_then(serde_json::Value::as_array_mut)
        .ok_or_else(|| "poster state has no poster array".to_string())?;
    if posters.is_empty() {
        return Err("poster state has no posters".to_string());
    }
    let should_lock = !posters.iter().all(|poster| {
        poster
            .get("isLocked")
            .and_then(serde_json::Value::as_bool)
            .unwrap_or(false)
    });
    for poster in posters {
        let object = poster
            .as_object_mut()
            .ok_or_else(|| "poster entry is not an object".to_string())?;
        object.insert("isLocked".to_string(), serde_json::json!(should_lock));
    }
    Ok(true)
}

fn apply_quick_theme_snapshot(
    snapshot: &mut serde_json::Value,
    template_key: &str,
) -> Result<bool, String> {
    let selected_poster_id = snapshot
        .get("selectedPosterId")
        .and_then(serde_json::Value::as_str)
        .ok_or_else(|| "selected poster id is unavailable".to_string())?
        .to_string();
    let poster = snapshot
        .get_mut("posters")
        .and_then(serde_json::Value::as_array_mut)
        .and_then(|posters| {
            posters.iter_mut().find(|poster| {
                poster.get("id").and_then(serde_json::Value::as_str)
                    == Some(selected_poster_id.as_str())
            })
        })
        .ok_or_else(|| "selected poster is unavailable".to_string())?;
    if poster
        .get("templateKey")
        .and_then(serde_json::Value::as_str)
        == Some(template_key)
    {
        return Ok(false);
    }
    poster
        .as_object_mut()
        .ok_or_else(|| "selected poster is not an object".to_string())?
        .insert(
            "templateKey".to_string(),
            serde_json::Value::String(template_key.to_string()),
        );
    Ok(true)
}

fn toggle_all_poster_locks(app: &tauri::AppHandle) {
    let Some(repository) = app.try_state::<PosterStateRepository>() else {
        return;
    };
    let result = mutate_poster_state_with_cas(repository.inner(), toggle_all_poster_locks_snapshot);
    match result {
        Ok(Some(snapshot)) => emit_poster_state_update(app, &snapshot),
        Ok(None) => {}
        Err(error) => eprintln!("failed to toggle all poster locks: {error}"),
    }
}

fn apply_quick_theme(app: &tauri::AppHandle, theme: &str) {
    let template_key = match theme {
        "nocturne" => "transparent-default",
        "ember" => "builtin-ember",
        "glass" => "builtin-glass",
        _ => return,
    };
    let Some(repository) = app.try_state::<PosterStateRepository>() else {
        return;
    };
    let result = mutate_poster_state_with_cas(repository.inner(), |snapshot| {
        apply_quick_theme_snapshot(snapshot, template_key)
    });
    match result {
        Ok(Some(snapshot)) => emit_poster_state_update(app, &snapshot),
        Ok(None) => {}
        Err(error) => eprintln!("failed to apply quick theme: {error}"),
    }
}
#[tauri::command]
fn replace_poster_state(
    expected_revision: Option<u64>,
    snapshot: serde_json::Value,
    app: tauri::AppHandle,
    repository: State<'_, PosterStateRepository>,
) -> Result<PosterStateMutationResult, PosterStateStoreError> {
    let result = repository.replace_with_expected_revision(expected_revision, snapshot)?;
    if let PosterStateMutationResult::Stored { snapshot, .. } = &result {
        emit_poster_state_update(&app, snapshot);
    }
    Ok(result)
}

#[tauri::command]
fn migrate_poster_state(
    request: LegacyPosterMigrationRequest,
    app: tauri::AppHandle,
    repository: State<'_, PosterStateRepository>,
) -> Result<LegacyPosterMigrationResult, PosterStateStoreError> {
    let result = repository.migrate_legacy(request)?;
    if let LegacyPosterMigrationResult::Migrated { snapshot, .. } = &result {
        emit_poster_state_update(&app, snapshot);
    }
    Ok(result)
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
struct PosterStatePrimaryWorkArea {
    display_id: String,
    is_primary: bool,
    width: f64,
    height: f64,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
struct PosterStateBootstrapContext {
    primary_work_area: PosterStatePrimaryWorkArea,
}

fn poster_state_context_from_topology(
    topology: &DisplayTopology,
) -> Result<PosterStateBootstrapContext, String> {
    let display = topology
        .displays
        .iter()
        .find(|display| display.is_primary)
        .ok_or_else(|| "primary display work area is unavailable".to_string())?;
    let work_area = &display.effective_logical_work_area;
    if !work_area.width.is_finite()
        || !work_area.height.is_finite()
        || work_area.width <= 0.0
        || work_area.height <= 0.0
    {
        return Err("primary display work area is invalid".to_string());
    }
    Ok(PosterStateBootstrapContext {
        primary_work_area: PosterStatePrimaryWorkArea {
            display_id: display.display_id.clone(),
            is_primary: true,
            width: work_area.width,
            height: work_area.height,
        },
    })
}

#[tauri::command]
fn poster_state_bootstrap_context(
    app: tauri::AppHandle,
) -> Result<PosterStateBootstrapContext, String> {
    let window = desktop_host_window(&app)?;
    poster_state_context_from_topology(&resolve_display_topology(&window))
}

#[tauri::command]
fn runtime_state(
    state: State<'_, RuntimeState>,
    coordinator: State<'_, MainWindowCoordinator>,
    taskbar: State<'_, TaskbarRecoveryLifecycle>,
) -> Result<RuntimeSnapshot, String> {
    let mut snapshot = state
        .0
        .lock()
        .map(|snapshot| snapshot.clone())
        .map_err(|error| error.to_string())?;
    enrich_control_center_snapshot(coordinator.inner(), &mut snapshot);
    enrich_taskbar_recovery_snapshot(taskbar.inner(), &mut snapshot);
    Ok(snapshot)
}

#[tauri::command]
fn desktop_host_diagnostics(
    state: State<'_, RuntimeState>,
) -> Result<DesktopHostDiagnostics, String> {
    state
        .0
        .lock()
        .map(|snapshot| snapshot.desktop_host.clone())
        .map_err(|error| error.to_string())
}

#[tauri::command]
fn install_path_diagnostics(paths: State<'_, RuntimePathResolution>) -> InstallPathDiagnostics {
    paths.diagnostics().clone()
}

#[tauri::command]
fn install_content_probe(paths: State<'_, RuntimePathResolution>) -> InstallContentProbe {
    probe_install_content(paths.inner())
}

#[tauri::command]
fn autostart_diagnostics(
    app: tauri::AppHandle,
    paths: State<'_, RuntimePathResolution>,
    coordinator: State<'_, AutostartCoordinator>,
) -> AutostartDiagnostics {
    let snapshot = coordinator.refresh(paths.inner());
    publish_autostart_snapshot(&app, snapshot.clone());
    snapshot
}

#[tauri::command]
fn set_autostart_enabled(
    app: tauri::AppHandle,
    paths: State<'_, RuntimePathResolution>,
    coordinator: State<'_, AutostartCoordinator>,
    enabled: bool,
) -> AutostartChangeResult {
    let result = coordinator.set_enabled(paths.inner(), enabled);
    publish_autostart_snapshot(&app, result.after.clone());
    result
}
#[tauri::command]
fn launch_intent_diagnostics(
    coordinator: State<'_, LaunchIntentCoordinator>,
) -> LaunchIntentDiagnostics {
    coordinator.diagnostics()
}

fn monitor_matches(left: &tauri::Monitor, right: &tauri::Monitor) -> bool {
    left.name() == right.name()
        && left.position() == right.position()
        && left.size() == right.size()
}

fn physical_rect(
    position: &tauri::PhysicalPosition<i32>,
    size: &tauri::PhysicalSize<u32>,
) -> PhysicalRect {
    PhysicalRect {
        x: position.x,
        y: position.y,
        width: size.width,
        height: size.height,
    }
}

fn resolve_display_topology(window: &tauri::WebviewWindow) -> DisplayTopology {
    let primary = window.primary_monitor().ok().flatten();
    let scale_policy = ResponsiveScalePolicy::default();
    let mut monitors = window.available_monitors().unwrap_or_default();
    monitors.sort_by(|left, right| {
        (left.position().x, left.position().y, left.name()).cmp(&(
            right.position().x,
            right.position().y,
            right.name(),
        ))
    });
    let displays = monitors
        .iter()
        .enumerate()
        .map(|(index, monitor)| {
            let work_area = monitor.work_area();
            let physical_work_area = physical_rect(&work_area.position, &work_area.size);
            let effective_logical_work_area =
                effective_logical_work_area(&physical_work_area, monitor.scale_factor());
            let responsive_scale =
                responsive_scale_for_work_area(&effective_logical_work_area, &scale_policy);
            DisplayDescriptor {
                display_id: monitor
                    .name()
                    .cloned()
                    .unwrap_or_else(|| format!("display-{index}")),
                is_primary: primary
                    .as_ref()
                    .is_some_and(|primary| monitor_matches(primary, monitor)),
                scale_factor: monitor.scale_factor(),
                renderer_scale_factor: monitor.scale_factor(),
                bounds: physical_rect(monitor.position(), monitor.size()),
                work_area: physical_work_area,
                effective_logical_work_area,
                responsive_scale,
            }
        })
        .collect::<Vec<_>>();
    DisplayTopology {
        contract_version: HOST_CONTRACT_VERSION,
        coordinate_space: "physicalPixels".to_string(),
        virtual_bounds: virtual_bounds(&displays),
        scale_policy,
        displays,
    }
}

fn target_display_id(
    window: &tauri::WebviewWindow,
    intent: &HostPlacementIntent,
    placement_store: &PlacementStore,
) -> Option<String> {
    if let HostPlacementIntent::Anchor {
        display_id: Some(display_id),
        ..
    } = intent
    {
        return Some(display_id.clone());
    }
    window
        .current_monitor()
        .ok()
        .flatten()
        .and_then(|monitor| monitor.name().cloned())
        .or_else(|| {
            placement_store
                .snapshot()
                .and_then(|placement| placement.monitor_device)
        })
}

fn select_target_display_index(
    topology: &DisplayTopology,
    preferred_display_id: Option<&str>,
) -> Option<usize> {
    preferred_display_id
        .and_then(|display_id| {
            topology
                .displays
                .iter()
                .position(|display| display.display_id == display_id)
        })
        .or_else(|| {
            topology
                .displays
                .iter()
                .position(|display| display.is_primary)
        })
        .or_else(|| (!topology.displays.is_empty()).then_some(0))
}

fn next_host_revision(state: &RuntimeState) -> Result<u64, String> {
    state
        .0
        .lock()
        .map(|snapshot| snapshot.host_surface.revision.saturating_add(1).max(1))
        .map_err(|error| error.to_string())
}

fn current_host_surface(state: &RuntimeState) -> Result<HostSurfaceSnapshot, String> {
    state
        .0
        .lock()
        .map(|snapshot| snapshot.host_surface.clone())
        .map_err(|error| error.to_string())
}

fn apply_host_placement_intent(
    window: &tauri::WebviewWindow,
    intent: &HostPlacementIntent,
    placement_store: &PlacementStore,
    current_position: tauri::PhysicalPosition<i32>,
    is_initial_surface_sync: bool,
) -> Result<(), String> {
    match intent {
        HostPlacementIntent::Preserve => {
            if is_initial_surface_sync {
                if let Some(placement) = placement_store.snapshot() {
                    desktop_host::restore_poster_placement(window, &placement)?;
                }
            } else {
                // A collapse/expand transition changes only the bottom edge. Reapplying the
                // normalized saved anchor here would calculate Y against a different usable
                // monitor span and move the header even though the user did not drag it.
                window
                    .set_position(current_position)
                    .map_err(|error| error.to_string())?;
            }
        }
        HostPlacementIntent::Anchor {
            display_id,
            normalized_x,
            normalized_y,
        } => {
            let fallback = window.outer_position().map_err(|error| error.to_string())?;
            let placement = placement::PosterPlacement::new(
                display_id.clone(),
                *normalized_x,
                *normalized_y,
                fallback.x,
                fallback.y,
            );
            desktop_host::restore_poster_placement(window, &placement)?;
            placement_store.record(placement);
        }
    }
    Ok(())
}

fn update_host_surface_diagnostics(snapshot: &mut RuntimeSnapshot) {
    let regions = snapshot.host_surface.interaction_regions();
    let scale = snapshot.host_surface.effective_scale_factor;
    let mut summaries: Vec<String> = regions
        .iter()
        .take(8)
        .map(|region| {
            format!(
                "{}:{:.1},{:.1} {:.1}x{:.1} @{scale:.2}x",
                region.region_id,
                region.rect.x,
                region.rect.y,
                region.rect.width,
                region.rect.height,
            )
        })
        .collect();
    if regions.len() > summaries.len() {
        summaries.push(format!("+{} more", regions.len() - summaries.len()));
    }
    let (registry_version, registry_revision) = snapshot
        .host_surface
        .surface_registry
        .as_ref()
        .map(|registry| (registry.registry_version, registry.revision))
        .unwrap_or((0, 0));

    snapshot.desktop_host.interaction_region_count = regions.len();
    snapshot.desktop_host.interaction_region =
        (!summaries.is_empty()).then(|| summaries.join("; "));
    snapshot.desktop_host.surface_registry_version = registry_version;
    snapshot.desktop_host.surface_registry_revision = registry_revision;
    snapshot.desktop_host.registered_surface_count =
        snapshot.host_surface.registered_surface_count();
}
fn apply_host_surface_request(
    app: &tauri::AppHandle,
    request: HostSurfaceRequest,
) -> Result<HostSurfaceSyncResult, String> {
    let validated = request.validate()?;
    let coordinator = app.state::<HostSurfaceCoordinator>();
    let _guard = coordinator.0.lock().map_err(|error| error.to_string())?;
    let runtime_state = app.state::<RuntimeState>();
    let is_initial_surface_sync = {
        let snapshot = runtime_state.0.lock().map_err(|error| error.to_string())?;
        if validated.request.revision < snapshot.host_surface.revision {
            return Ok(HostSurfaceSyncResult::from_runtime(&snapshot));
        }
        snapshot.host_surface.revision == 0
    };

    let window = desktop_host_window(app)?;
    let current_position = window.outer_position().map_err(|error| error.to_string())?;
    let placement_store = app.state::<PlacementStore>();
    let mut display_topology = resolve_display_topology(&window);
    let preferred_display_id = target_display_id(
        &window,
        &validated.request.placement_intent,
        &placement_store,
    );
    let target_display_index =
        select_target_display_index(&display_topology, preferred_display_id.as_deref());
    if let Some(index) = target_display_index {
        let display = &mut display_topology.displays[index];
        display.renderer_scale_factor = validated.request.scale_factor;
        display.effective_logical_work_area =
            effective_logical_work_area(&display.work_area, display.renderer_scale_factor);
        display.responsive_scale = responsive_scale_for_work_area(
            &display.effective_logical_work_area,
            &display_topology.scale_policy,
        );
    }
    let target_display = target_display_index.map(|index| display_topology.displays[index].clone());
    let validated = if let Some(display) = target_display.as_ref() {
        validated.resolve_display_scale(
            Some(display.display_id.clone()),
            display.scale_factor,
            display.renderer_scale_factor,
            display.responsive_scale,
        )?
    } else {
        validated
    };
    let (physical_width, physical_height) = clamp_surface_physical_size(
        (
            validated.physical_size.width,
            validated.physical_size.height,
        ),
        target_display
            .as_ref()
            .map(|display| (display.work_area.width, display.work_area.height))
            .or_else(|| desktop_host::poster_work_area_size(&window)),
    );
    window
        .set_size(tauri::PhysicalSize::new(physical_width, physical_height))
        .map_err(|error| error.to_string())?;
    apply_host_placement_intent(
        &window,
        &validated.request.placement_intent,
        &placement_store,
        current_position,
        is_initial_surface_sync,
    )?;
    let visual_regions = validated.visual_regions();
    let interaction_regions = validated.interaction_regions();
    desktop_host::apply_surface_regions(
        &window,
        &visual_regions,
        &interaction_regions,
        validated.effective_scale_factor,
    )?;
    let _ = window.set_title("");

    let mut host_surface = HostSurfaceSnapshot::from(validated);
    host_surface.physical_size.width = physical_width;
    host_surface.physical_size.height = physical_height;
    let host_topology = inspect_host_topology(app);
    let mut snapshot = runtime_state.0.lock().map_err(|error| error.to_string())?;
    snapshot.host_surface = host_surface;
    snapshot.display_topology = display_topology;
    snapshot.host_topology = host_topology;
    update_host_surface_diagnostics(&mut snapshot);
    let updated = snapshot.clone();
    let result = HostSurfaceSyncResult::from_runtime(&snapshot);
    drop(snapshot);
    schedule_runtime_outputs(app, updated, false);
    Ok(result)
}

#[tauri::command]
fn sync_host_surface(
    request: HostSurfaceRequest,
    app: tauri::AppHandle,
) -> Result<HostSurfaceSyncResult, String> {
    apply_host_surface_request(&app, request)
}

fn resync_host_surface_for_display_change(
    app: &tauri::AppHandle,
) -> Result<Option<HostSurfaceSyncResult>, String> {
    let runtime_state = app.state::<RuntimeState>();
    let current = current_host_surface(&runtime_state)?;
    if current.revision == 0 {
        return Ok(None);
    }
    let revision = next_host_revision(&runtime_state)?;
    let mut surface_registry = current.surface_registry;
    if let Some(registry) = surface_registry.as_mut() {
        registry.revision = revision;
    }
    let request = HostSurfaceRequest {
        contract_version: HOST_CONTRACT_VERSION,
        revision,
        surface_id: current.surface_id,
        logical_size: current.logical_size,
        scale_factor: current.device_scale_factor,
        user_scale: current.user_scale,
        placement_intent: HostPlacementIntent::Preserve,
        interaction_region: current.interaction_region,
        surface_registry,
    };
    apply_host_surface_request(app, request).map(Some)
}

#[tauri::command]
fn start_poster_drag(app: tauri::AppHandle) -> Result<(), String> {
    let window = desktop_host_window(&app)?;
    desktop_host::start_poster_drag(&window)
}

fn clamp_surface_physical_size(requested: (u32, u32), work_area: Option<(u32, u32)>) -> (u32, u32) {
    match work_area {
        Some((work_width, work_height)) => (
            requested.0.min(work_width.max(1)),
            requested.1.min(work_height.max(1)),
        ),
        None => requested,
    }
}

fn fit_region_to_logical_size(region: &mut InteractionRegion, width: f64, height: f64) {
    region.rect.x = region.rect.x.clamp(0.0, (width - 1.0).max(0.0));
    region.rect.y = region.rect.y.clamp(0.0, (height - 1.0).max(0.0));
    region.rect.width = region.rect.width.min((width - region.rect.x).max(1.0));
    region.rect.height = region.rect.height.min((height - region.rect.y).max(1.0));
    region.corner_radius = region
        .corner_radius
        .min(region.rect.width.min(region.rect.height) / 2.0);
}

#[tauri::command]
fn resize_poster_surface(
    collapsed: bool,
    width: f64,
    height: f64,
    surface_gutter: f64,
    device_scale_factor: f64,
    app: tauri::AppHandle,
) -> Result<(), String> {
    let gutter = surface_gutter.clamp(0.0, 32.0);
    let scale = device_scale_factor.clamp(0.5, 4.0);
    let logical_width = width.clamp(240.0, 1920.0) + gutter * 2.0;
    let content_height = if collapsed {
        116.0
    } else {
        height.clamp(240.0, 2160.0)
    };
    let logical_height = content_height + gutter * 2.0;
    let runtime_state = app.state::<RuntimeState>();
    let current = current_host_surface(&runtime_state)?;
    let revision = next_host_revision(&runtime_state)?;
    let mut surface_registry = current.surface_registry;
    if let Some(registry) = surface_registry.as_mut() {
        registry.revision = revision;
        for surface in &mut registry.surfaces {
            fit_region_to_logical_size(&mut surface.visual_region, logical_width, logical_height);
            surface.interaction_regions.retain(|region| {
                region.rect.x + region.rect.width <= logical_width
                    && region.rect.y + region.rect.height <= logical_height
            });
        }
    }
    let interaction_region = current.interaction_region.filter(|region| {
        region.rect.x + region.rect.width <= logical_width
            && region.rect.y + region.rect.height <= logical_height
    });
    let request = HostSurfaceRequest {
        contract_version: HOST_CONTRACT_VERSION,
        revision,
        surface_id: current.surface_id,
        logical_size: LogicalSize {
            width: logical_width,
            height: logical_height,
        },
        scale_factor: scale,
        user_scale: current.user_scale,
        placement_intent: HostPlacementIntent::Preserve,
        interaction_region,
        surface_registry,
    };
    apply_host_surface_request(&app, request).map(|_| ())
}

#[tauri::command]
fn set_poster_interaction_region(
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    corner_radius: f64,
    device_scale_factor: f64,
    app: tauri::AppHandle,
) -> Result<DesktopHostDiagnostics, String> {
    let window = desktop_host_window(&app)?;
    let runtime_state = app.state::<RuntimeState>();
    let current = current_host_surface(&runtime_state)?;
    let scale = device_scale_factor.clamp(0.5, 4.0);
    let logical_size = if current.revision > 0 {
        current.logical_size
    } else {
        let physical = window.inner_size().map_err(|error| error.to_string())?;
        LogicalSize {
            width: (physical.width as f64 / scale).max(1.0),
            height: (physical.height as f64 / scale).max(1.0),
        }
    };
    let region_x = x.clamp(0.0, 64.0).min((logical_size.width - 1.0).max(0.0));
    let region_y = y.clamp(0.0, 64.0).min((logical_size.height - 1.0).max(0.0));
    let region_width = width
        .clamp(1.0, 1920.0)
        .min((logical_size.width - region_x).max(1.0));
    let region_height = height
        .clamp(1.0, 2160.0)
        .min((logical_size.height - region_y).max(1.0));
    let max_radius = region_width.min(region_height) / 2.0;
    let request = HostSurfaceRequest {
        contract_version: HOST_CONTRACT_VERSION,
        revision: next_host_revision(&runtime_state)?,
        surface_id: current.surface_id,
        logical_size,
        scale_factor: scale,
        user_scale: current.user_scale,
        placement_intent: HostPlacementIntent::Preserve,
        interaction_region: Some(InteractionRegion {
            region_id: "poster-legacy".to_string(),
            rect: LogicalRect {
                x: region_x,
                y: region_y,
                width: region_width,
                height: region_height,
            },
            corner_radius: corner_radius.clamp(0.0, 48.0).min(max_radius),
        }),
        surface_registry: None,
    };
    apply_host_surface_request(&app, request).map(|result| result.desktop_host)
}

fn configure_poster_host(
    app: &tauri::AppHandle,
    prefer_workerw: bool,
) -> Result<DesktopHostDiagnostics, String> {
    let window = desktop_host_window(app)?;
    POSTER_PLACEMENT_CAPTURE_ENABLED.store(false, Ordering::Release);
    let mut diagnostics = desktop_host::configure(&window, prefer_workerw);
    if let Some(placement) = app
        .try_state::<PlacementStore>()
        .and_then(|store| store.snapshot())
    {
        if let Err(error) = desktop_host::restore_poster_placement(&window, &placement) {
            eprintln!("failed to restore poster placement after host configuration: {error}");
        }
    }
    let _ = window.set_title("");
    POSTER_PLACEMENT_CAPTURE_ENABLED.store(true, Ordering::Release);

    let display_topology = resolve_display_topology(&window);
    let host_topology = inspect_host_topology(app);
    let updated = if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(mut snapshot) = state.0.lock() {
            diagnostics.recovery_count = snapshot.desktop_host.recovery_count;
            diagnostics.recovery_generation = snapshot.desktop_host.recovery_generation;
            diagnostics.recovery_event_count = snapshot.desktop_host.recovery_event_count;
            diagnostics.last_recovery_trigger = snapshot.desktop_host.last_recovery_trigger.clone();
            diagnostics.last_recovery_action = snapshot.desktop_host.last_recovery_action.clone();
            diagnostics.last_recovery_error = snapshot.desktop_host.last_recovery_error.clone();
            snapshot.desktop_host = diagnostics.clone();
            snapshot.display_topology = display_topology;
            snapshot.host_topology = host_topology;
            update_host_surface_diagnostics(&mut snapshot);
            diagnostics = snapshot.desktop_host.clone();
            Some(snapshot.clone())
        } else {
            None
        }
    } else {
        None
    };
    if let Some(updated) = updated {
        schedule_runtime_outputs(app, updated, false);
    }
    Ok(diagnostics)
}

fn recover_desktop_host(
    app: &tauri::AppHandle,
    request: HostRecoveryRequest,
) -> Result<DesktopHostDiagnostics, String> {
    let coordinator = app.state::<HostRecoveryCoordinator>();
    let _guard = coordinator
        .execution
        .lock()
        .map_err(|error| error.to_string())?;
    if app
        .state::<RuntimeState>()
        .0
        .lock()
        .map(|snapshot| snapshot.runtime_lifecycle.phase == RuntimePhase::Exiting)
        .unwrap_or(true)
    {
        return Err("runtime is exiting; desktop recovery is disabled".to_string());
    }

    POSTER_PLACEMENT_CAPTURE_ENABLED.store(false, Ordering::Release);
    let mut recovery_action = "fullRecovery";
    let result = (|| -> Result<(), String> {
        if request.is_shell_only() {
            recovery_action = "shellRepair";
            let window = desktop_host_window(app)?;
            if desktop_host::repair_shell_host(&window)? {
                return Ok(());
            }
            recovery_action = "fullRecovery";
        }
        configure_poster_host(app, true)?;
        POSTER_PLACEMENT_CAPTURE_ENABLED.store(false, Ordering::Release);
        let _ = resync_host_surface_for_display_change(app)?;
        Ok(())
    })();
    POSTER_PLACEMENT_CAPTURE_ENABLED.store(true, Ordering::Release);

    let recovery_count = coordinator
        .completed_count
        .fetch_add(1, Ordering::AcqRel)
        .saturating_add(1);
    let recovery_error = result.as_ref().err().cloned();
    let trigger_summary = request.trigger_summary();
    let runtime_state = app.state::<RuntimeState>();
    let (updated, diagnostics) = {
        let mut snapshot = runtime_state.0.lock().map_err(|error| error.to_string())?;
        snapshot.desktop_host.recovery_count = recovery_count;
        snapshot.desktop_host.recovery_generation = request.generation;
        snapshot.desktop_host.recovery_event_count = request.event_count;
        snapshot.desktop_host.last_recovery_trigger = Some(trigger_summary);
        snapshot.desktop_host.last_recovery_action = Some(recovery_action.to_string());
        snapshot.desktop_host.last_recovery_error = recovery_error;
        let diagnostics = snapshot.desktop_host.clone();
        (snapshot.clone(), diagnostics)
    };
    schedule_runtime_outputs(app, updated, false);
    result.map(|_| diagnostics)
}

#[tauri::command]
fn reconnect_desktop_host(app: tauri::AppHandle) -> Result<DesktopHostDiagnostics, String> {
    let result = recover_desktop_host(
        &app,
        HostRecoveryRequest::single(HostRecoveryTrigger::ManualReconnect),
    );
    if let Some(lifecycle) = app.try_state::<TaskbarRecoveryLifecycle>() {
        let diagnostic = result.as_ref().map(|_| ()).map_err(Clone::clone);
        let _ = lifecycle.record_manual_reconnect(&diagnostic);
    }
    publish_taskbar_recovery_snapshot(&app);
    result
}

#[derive(Default)]
struct ProcessMemorySnapshot {
    working_set_bytes: u64,
    private_working_set_bytes: u64,
    private_commit_bytes: u64,
}

#[derive(Default)]
struct ProcessTreeMemorySnapshot {
    working_set_bytes: u64,
    private_working_set_bytes: u64,
    private_commit_bytes: u64,
    process_count: u32,
    measurement_complete: bool,
}

#[cfg(target_os = "windows")]
fn process_memory_for_handle(
    process: windows::Win32::Foundation::HANDLE,
) -> Option<ProcessMemorySnapshot> {
    use windows::Win32::System::ProcessStatus::{
        K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS, PROCESS_MEMORY_COUNTERS_EX2,
    };

    let mut counters = PROCESS_MEMORY_COUNTERS_EX2 {
        cb: std::mem::size_of::<PROCESS_MEMORY_COUNTERS_EX2>() as u32,
        ..Default::default()
    };
    let ok = unsafe {
        K32GetProcessMemoryInfo(
            process,
            &mut counters as *mut PROCESS_MEMORY_COUNTERS_EX2 as *mut PROCESS_MEMORY_COUNTERS,
            std::mem::size_of::<PROCESS_MEMORY_COUNTERS_EX2>() as u32,
        )
    };
    ok.as_bool().then_some(ProcessMemorySnapshot {
        working_set_bytes: counters.WorkingSetSize as u64,
        private_working_set_bytes: counters.PrivateWorkingSetSize as u64,
        private_commit_bytes: counters.PrivateUsage as u64,
    })
}

#[cfg(target_os = "windows")]
fn current_process_tree_memory() -> ProcessTreeMemorySnapshot {
    use std::collections::HashSet;
    use windows::Win32::Foundation::CloseHandle;
    use windows::Win32::System::Diagnostics::ToolHelp::{
        CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W,
        TH32CS_SNAPPROCESS,
    };
    use windows::Win32::System::Threading::{
        GetCurrentProcess, GetCurrentProcessId, OpenProcess, PROCESS_QUERY_INFORMATION,
        PROCESS_VM_READ,
    };

    let root_process_id = unsafe { GetCurrentProcessId() };
    let snapshot = match unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) } {
        Ok(snapshot) => snapshot,
        Err(_) => return ProcessTreeMemorySnapshot::default(),
    };
    let mut entries = Vec::new();
    let mut entry = PROCESSENTRY32W {
        dwSize: std::mem::size_of::<PROCESSENTRY32W>() as u32,
        ..Default::default()
    };
    if unsafe { Process32FirstW(snapshot, &mut entry) }.is_err() {
        let _ = unsafe { CloseHandle(snapshot) };
        return ProcessTreeMemorySnapshot::default();
    }
    loop {
        entries.push((entry.th32ProcessID, entry.th32ParentProcessID));
        if unsafe { Process32NextW(snapshot, &mut entry) }.is_err() {
            break;
        }
    }
    let _ = unsafe { CloseHandle(snapshot) };

    if !entries
        .iter()
        .any(|(process_id, _)| *process_id == root_process_id)
    {
        return ProcessTreeMemorySnapshot::default();
    }

    let mut process_ids = HashSet::from([root_process_id]);
    loop {
        let previous_count = process_ids.len();
        for (process_id, parent_process_id) in &entries {
            if process_ids.contains(parent_process_id) {
                process_ids.insert(*process_id);
            }
        }
        if process_ids.len() == previous_count {
            break;
        }
    }

    let mut result = ProcessTreeMemorySnapshot {
        process_count: process_ids.len() as u32,
        measurement_complete: true,
        ..Default::default()
    };
    for process_id in process_ids {
        let (handle, close_after_read) = if process_id == root_process_id {
            (unsafe { GetCurrentProcess() }, false)
        } else {
            match unsafe {
                OpenProcess(
                    PROCESS_QUERY_INFORMATION | PROCESS_VM_READ,
                    false,
                    process_id,
                )
            } {
                Ok(handle) => (handle, true),
                Err(_) => {
                    result.measurement_complete = false;
                    continue;
                }
            }
        };
        let memory = process_memory_for_handle(handle);
        if close_after_read {
            let _ = unsafe { CloseHandle(handle) };
        }
        if let Some(memory) = memory {
            result.working_set_bytes = result
                .working_set_bytes
                .saturating_add(memory.working_set_bytes);
            result.private_working_set_bytes = result
                .private_working_set_bytes
                .saturating_add(memory.private_working_set_bytes);
            result.private_commit_bytes = result
                .private_commit_bytes
                .saturating_add(memory.private_commit_bytes);
        } else {
            result.measurement_complete = false;
        }
    }
    result
}

#[cfg(not(target_os = "windows"))]
fn current_process_tree_memory() -> ProcessTreeMemorySnapshot {
    ProcessTreeMemorySnapshot::default()
}

#[cfg(target_os = "windows")]
fn current_process_memory() -> ProcessMemorySnapshot {
    use windows::Win32::System::ProcessStatus::{
        K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS, PROCESS_MEMORY_COUNTERS_EX2,
    };
    use windows::Win32::System::Threading::GetCurrentProcess;

    let mut counters = PROCESS_MEMORY_COUNTERS_EX2 {
        cb: std::mem::size_of::<PROCESS_MEMORY_COUNTERS_EX2>() as u32,
        ..Default::default()
    };
    let ok = unsafe {
        K32GetProcessMemoryInfo(
            GetCurrentProcess(),
            &mut counters as *mut PROCESS_MEMORY_COUNTERS_EX2 as *mut PROCESS_MEMORY_COUNTERS,
            std::mem::size_of::<PROCESS_MEMORY_COUNTERS_EX2>() as u32,
        )
    };
    if ok.as_bool() {
        ProcessMemorySnapshot {
            working_set_bytes: counters.WorkingSetSize as u64,
            private_working_set_bytes: counters.PrivateWorkingSetSize as u64,
            private_commit_bytes: counters.PrivateUsage as u64,
        }
    } else {
        ProcessMemorySnapshot::default()
    }
}

#[cfg(not(target_os = "windows"))]
fn current_process_memory() -> ProcessMemorySnapshot {
    ProcessMemorySnapshot::default()
}

#[cfg(target_os = "windows")]
fn current_process_count() -> u32 {
    use windows::Win32::System::ProcessStatus::EnumProcesses;

    let mut pids = [0u32; 4096];
    let mut needed = 0u32;
    let ok = unsafe {
        EnumProcesses(
            pids.as_mut_ptr(),
            std::mem::size_of_val(&pids) as u32,
            &mut needed,
        )
    };
    if ok.is_err() {
        return 0;
    }
    let count = (needed as usize / std::mem::size_of::<u32>()).min(pids.len());
    count as u32
}

#[cfg(not(target_os = "windows"))]
fn current_process_count() -> u32 {
    0
}

#[cfg(target_os = "windows")]
fn current_process_cpu_ms() -> u64 {
    use windows::Win32::Foundation::FILETIME;
    use windows::Win32::System::Threading::{GetCurrentProcess, GetProcessTimes};

    let mut creation = FILETIME::default();
    let mut exit = FILETIME::default();
    let mut kernel = FILETIME::default();
    let mut user = FILETIME::default();
    let ok = unsafe {
        GetProcessTimes(
            GetCurrentProcess(),
            &mut creation,
            &mut exit,
            &mut kernel,
            &mut user,
        )
    };
    if ok.is_err() {
        return 0;
    }
    let kernel_ms = (((kernel.dwHighDateTime as u64) << 32) | kernel.dwLowDateTime as u64) / 10_000;
    let user_ms = (((user.dwHighDateTime as u64) << 32) | user.dwLowDateTime as u64) / 10_000;
    kernel_ms + user_ms
}

#[cfg(not(target_os = "windows"))]
fn current_process_cpu_ms() -> u64 {
    0
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct PerformanceSnapshot {
    memory_bytes: u64,
    private_working_set_bytes: u64,
    private_commit_bytes: u64,
    process_count: u32,
    process_tree_working_set_bytes: u64,
    process_tree_private_working_set_bytes: u64,
    process_tree_private_commit_bytes: u64,
    process_tree_process_count: u32,
    process_tree_measurement_complete: bool,
    poster_fps: f64,
    cpu_ms: u64,
    pause_state: String,
    elapsed_ms: u64,
    build_profile: String,
    host_contract_version: u32,
    performance_gate_contract: String,
}

fn collect_performance_snapshot(
    app: &tauri::AppHandle,
    state: &RuntimeState,
) -> Result<PerformanceSnapshot, String> {
    let (poster_fps, pause_state) = {
        let snapshot = state.0.lock().map_err(|error| error.to_string())?;
        (snapshot.poster_fps, snapshot.pause_state())
    };
    let elapsed_ms = app
        .try_state::<PerformanceClock>()
        .map(|clock| clock.0.elapsed().as_millis() as u64)
        .unwrap_or(0);
    let memory = current_process_memory();
    let process_tree_memory = current_process_tree_memory();
    Ok(PerformanceSnapshot {
        memory_bytes: memory.working_set_bytes,
        private_working_set_bytes: memory.private_working_set_bytes,
        private_commit_bytes: memory.private_commit_bytes,
        process_count: current_process_count(),
        process_tree_working_set_bytes: process_tree_memory.working_set_bytes,
        process_tree_private_working_set_bytes: process_tree_memory.private_working_set_bytes,
        process_tree_private_commit_bytes: process_tree_memory.private_commit_bytes,
        process_tree_process_count: process_tree_memory.process_count,
        process_tree_measurement_complete: process_tree_memory.measurement_complete,
        poster_fps,
        cpu_ms: current_process_cpu_ms(),
        pause_state,
        elapsed_ms,
        build_profile: if cfg!(debug_assertions) {
            "debug".to_string()
        } else {
            "release".to_string()
        },
        host_contract_version: HOST_CONTRACT_VERSION,
        performance_gate_contract: PERFORMANCE_GATE_CONTRACT.to_string(),
    })
}

#[tauri::command]
fn get_performance_snapshot(
    app: tauri::AppHandle,
    state: State<'_, RuntimeState>,
) -> Result<PerformanceSnapshot, String> {
    collect_performance_snapshot(&app, state.inner())
}

#[tauri::command]
fn report_poster_fps(
    fps: f64,
    app: tauri::AppHandle,
    state: State<'_, RuntimeState>,
) -> Result<(), String> {
    let updated = mutate_runtime_state(state.inner(), "posterFps", |snapshot| {
        snapshot.poster_fps = fps.clamp(0.0, 240.0);
    })?;
    publish_runtime_snapshot(&app, updated);
    Ok(())
}

fn merge_performance_samples(
    existing: Option<&serde_json::Value>,
    incoming: Vec<serde_json::Value>,
) -> Vec<serde_json::Value> {
    let mut merged = Vec::new();
    if let Some(samples) = existing
        .and_then(|value| value.get("samples"))
        .and_then(|samples| samples.as_array())
    {
        merged.extend(samples.iter().cloned());
    }
    for sample in &incoming {
        let mode = sample
            .get("mode")
            .and_then(|value| value.as_str())
            .unwrap_or("");
        merged
            .retain(|existing| existing.get("mode").and_then(|value| value.as_str()) != Some(mode));
    }
    merged.extend(incoming);
    merged
}

fn save_performance_baseline_file(
    samples: Vec<PerformanceSample>,
    app: &tauri::AppHandle,
) -> Result<PerformanceGateReport, String> {
    let directory = app
        .path()
        .app_config_dir()
        .map_err(|error| error.to_string())?;
    std::fs::create_dir_all(&directory).map_err(|error| error.to_string())?;
    let path = directory.join("performance-baseline-v4.json");
    let existing = std::fs::read(&path)
        .ok()
        .and_then(|bytes| serde_json::from_slice::<serde_json::Value>(&bytes).ok());
    let incoming = samples
        .into_iter()
        .map(serde_json::to_value)
        .collect::<Result<Vec<_>, _>>()
        .map_err(|error| error.to_string())?;
    let samples = merge_performance_samples(existing.as_ref(), incoming);
    let typed_samples = samples
        .iter()
        .cloned()
        .map(serde_json::from_value::<PerformanceSample>)
        .collect::<Result<Vec<_>, _>>()
        .map_err(|error| error.to_string())?;
    let gate = evaluate_performance_samples(&typed_samples, HOST_CONTRACT_VERSION);
    let saved_at = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|duration| duration.as_secs())
        .unwrap_or(0);
    let mut payload = serde_json::json!({
        "version": 4,
        "contractVersion": "performance-baseline-v4",
        "savedAtUnixSeconds": saved_at,
        "samples": samples,
        "gate": gate.clone(),
    });
    if let Some(existing) = existing.as_ref().and_then(|value| value.as_object()) {
        for key in ["machine"] {
            if let Some(value) = existing.get(key) {
                payload[key] = value.clone();
            }
        }
    }
    payload["sampling"] = serde_json::json!({
        "method": "Release main process and Desktop-Up/WebView2 descendant tree; wait at least 120000ms after launch, then collect 6 snapshots per mode across a 10000ms window",
        "stabilizationMinMs": PERFORMANCE_STABILIZATION_MIN_MS,
        "memoryMetrics": [
            "workingSetBytes",
            "privateWorkingSetBytes",
            "privateCommitBytes",
            "processTreeWorkingSetBytes",
            "processTreePrivateWorkingSetBytes",
            "processTreePrivateCommitBytes",
            "processTreeProcessCount",
            "processTreeMeasurementComplete"
        ],
        "intervalMs": 2000,
        "samplesPerMode": 6,
        "windowMs": 10000,
        "modes": ["activeStatic", "activeAnimated", "paused", "stopped"],
    });
    std::fs::write(
        path,
        serde_json::to_vec_pretty(&payload).map_err(|error| error.to_string())?,
    )
    .map_err(|error| error.to_string())?;
    Ok(gate)
}

#[tauri::command]
fn save_performance_baseline(
    samples: Vec<PerformanceSample>,
    app: tauri::AppHandle,
) -> Result<PerformanceGateReport, String> {
    save_performance_baseline_file(samples, &app)
}

fn performance_probe_requested() -> bool {
    std::env::args().any(|argument| argument == PERFORMANCE_GATE_ARGUMENT)
}

fn performance_sample_from_snapshot(
    mode: &str,
    snapshot: PerformanceSnapshot,
) -> PerformanceSample {
    let sampled_at = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| format!("unix-ms:{}", duration.as_millis()))
        .unwrap_or_else(|_| "unix-ms:0".to_string());
    PerformanceSample {
        mode: mode.to_string(),
        memory_bytes: snapshot.memory_bytes,
        private_working_set_bytes: snapshot.private_working_set_bytes,
        private_commit_bytes: snapshot.private_commit_bytes,
        process_count: snapshot.process_count,
        process_tree_working_set_bytes: snapshot.process_tree_working_set_bytes,
        process_tree_private_working_set_bytes: snapshot.process_tree_private_working_set_bytes,
        process_tree_private_commit_bytes: snapshot.process_tree_private_commit_bytes,
        process_tree_process_count: snapshot.process_tree_process_count,
        process_tree_measurement_complete: snapshot.process_tree_measurement_complete,
        poster_fps: snapshot.poster_fps,
        cpu_ms: snapshot.cpu_ms,
        pause_state: snapshot.pause_state,
        elapsed_ms: snapshot.elapsed_ms,
        sampled_at,
        build_profile: snapshot.build_profile,
        host_contract_version: snapshot.host_contract_version,
    }
}

fn start_release_performance_probe(app: tauri::AppHandle) {
    std::thread::spawn(move || {
        let elapsed_ms = app
            .try_state::<PerformanceClock>()
            .map(|clock| clock.0.elapsed().as_millis() as u64)
            .unwrap_or(0);
        if elapsed_ms < PERFORMANCE_STABILIZATION_MIN_MS {
            std::thread::sleep(Duration::from_millis(
                PERFORMANCE_STABILIZATION_MIN_MS - elapsed_ms,
            ));
        }
        let modes = ["activeStatic", "activeAnimated", "paused", "stopped"];
        let mut samples = Vec::with_capacity(modes.len() * PERFORMANCE_SAMPLE_COUNT);

        for mode in modes {
            let updated = {
                let state = app.state::<RuntimeState>();
                mutate_runtime_state(state.inner(), "performanceGateMode", |snapshot| {
                    snapshot.manually_paused = false;
                    snapshot.performance_mode = mode.to_string();
                })
            };
            let updated = match updated {
                Ok(updated) => updated,
                Err(error) => {
                    eprintln!("performance gate failed to enter {mode}: {error}");
                    app.exit(2);
                    return;
                }
            };
            schedule_runtime_outputs(&app, updated, false);
            std::thread::sleep(if mode == "activeAnimated" {
                Duration::from_millis(2_500)
            } else {
                Duration::from_millis(500)
            });

            for index in 0..PERFORMANCE_SAMPLE_COUNT {
                let snapshot = {
                    let state = app.state::<RuntimeState>();
                    collect_performance_snapshot(&app, state.inner())
                };
                match snapshot {
                    Ok(snapshot) => samples.push(performance_sample_from_snapshot(mode, snapshot)),
                    Err(error) => {
                        eprintln!("performance gate failed to sample {mode}: {error}");
                        app.exit(2);
                        return;
                    }
                }
                if index + 1 < PERFORMANCE_SAMPLE_COUNT {
                    std::thread::sleep(Duration::from_secs(2));
                }
            }
        }

        let report = match save_performance_baseline_file(samples, &app) {
            Ok(report) => report,
            Err(error) => {
                eprintln!("performance gate failed to save baseline: {error}");
                app.exit(2);
                return;
            }
        };
        let reset = {
            let state = app.state::<RuntimeState>();
            mutate_runtime_state(state.inner(), "performanceGateComplete", |snapshot| {
                snapshot.performance_mode = "auto".to_string();
            })
        };
        if let Ok(reset) = reset {
            schedule_runtime_outputs(&app, reset, false);
        }
        request_runtime_exit(
            &app,
            "performanceGateExit",
            if report.passed { 0 } else { 2 },
        );
    });
}

fn apply_native_poster_visibility(app: &tauri::AppHandle, snapshot: &RuntimeSnapshot) {
    let native_visible =
        snapshot.poster_visible && snapshot.host_surface_ready && !snapshot.exit_requested;
    desktop_host::set_poster_visibility_intent(native_visible);
    if let Some(window) = optional_desktop_host_window(app) {
        if native_visible {
            if let Err(error) = desktop_host::show_poster_at_desktop_layer(&window) {
                eprintln!("failed to show poster at the desktop layer: {error}");
            }
        } else {
            let _ = window.hide();
        }
    }
}

fn set_poster_visibility(app: &tauri::AppHandle, visible: bool) {
    if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(updated) = mutate_runtime_state(&state, "posterVisibility", |snapshot| {
            snapshot.poster_visible = visible;
        }) {
            // State is committed before any native window call.
            apply_native_poster_visibility(app, &updated);
            publish_runtime_snapshot(app, updated);
        }
    }
}

fn reset_poster_surface_initialization(app: &tauri::AppHandle) {
    if let Some(lifecycle) = app.try_state::<PosterSurfaceLifecycle>() {
        lifecycle.0.store(false, Ordering::Release);
    }
    if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(updated) = mutate_runtime_state(&state, "hostSurfaceStarted", |snapshot| {
            snapshot.host_surface_ready = false;
        }) {
            apply_native_poster_visibility(app, &updated);
            publish_runtime_snapshot(app, updated);
            return;
        }
    }
    desktop_host::set_poster_visibility_intent(false);
    if let Some(window) = optional_desktop_host_window(app) {
        let _ = window.hide();
    }
}

#[tauri::command]
fn mark_poster_surface_ready(revision: u64, app: tauri::AppHandle) -> Result<bool, String> {
    if revision == 0 {
        return Err("poster surface readiness requires a synchronized revision".to_string());
    }

    let host_surface = {
        let state = app.state::<RuntimeState>();
        let snapshot = state.0.lock().map_err(|error| error.to_string())?;
        if snapshot.host_surface.revision < revision {
            return Err(format!(
                "poster surface revision {} is not synchronized yet",
                revision
            ));
        }
        snapshot.host_surface.clone()
    };

    let lifecycle = app.state::<PosterSurfaceLifecycle>();
    if lifecycle.0.load(Ordering::Acquire) {
        return Ok(true);
    }

    // Reassert the final popup/DWM styles after the canonical size and window region are in
    // place, while the native window is still hidden. This prevents the cold-start frame from
    // ever exposing a stale Windows non-client surface.
    configure_poster_host(&app, true)?;
    let window = desktop_host_window(&app)?;
    window
        .set_size(tauri::PhysicalSize::new(
            host_surface.physical_size.width,
            host_surface.physical_size.height,
        ))
        .map_err(|error| error.to_string())?;
    // SWP_FRAMECHANGED in host configuration must run before the final region write.
    // Reapplying the merged region last invalidates the stale Windows 11/DWM non-client strip
    // that otherwise survives until the first user-initiated collapse/resize.
    let visual_regions = host_surface.visual_regions();
    let interaction_regions = host_surface.interaction_regions();
    desktop_host::apply_surface_regions(
        &window,
        &visual_regions,
        &interaction_regions,
        host_surface.effective_scale_factor,
    )?;
    lifecycle.0.store(true, Ordering::Release);
    let updated = mutate_runtime_state(
        app.state::<RuntimeState>().inner(),
        "hostSurfaceReady",
        |snapshot| {
            snapshot.host_surface_ready = true;
        },
    )?;
    apply_native_poster_visibility(&app, &updated);
    publish_runtime_snapshot(&app, updated);
    runtime_monitor::reevaluate_foreground();
    Ok(true)
}

fn begin_runtime_exit(app: &tauri::AppHandle, trigger: &str) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    if let Ok(updated) = mutate_runtime_state(&state, trigger, |snapshot| {
        snapshot.exit_requested = true;
    }) {
        apply_native_poster_visibility(app, &updated);
        publish_runtime_snapshot(app, updated);
    }
}

fn prepare_runtime_exit(app: &tauri::AppHandle, trigger: &str) {
    begin_runtime_exit(app, trigger);
    desktop_host::begin_exit();
    if let Some(taskbar) = app.try_state::<TaskbarRecoveryLifecycle>() {
        taskbar_recovery::begin_exit(taskbar.inner());
    }
    if let Some(store) = app.try_state::<PlacementStore>() {
        if let Err(error) = store.flush() {
            eprintln!("failed to flush poster placement before exit: {error}");
        }
    }
    if let Some(window) = app.get_webview_window(CONTROL_CENTER_WINDOW_LABEL) {
        let _ = window.destroy();
    }
    if let Some(window) = optional_desktop_host_window(app) {
        let _ = window.destroy();
    }
    remove_tray_icon(app);
}

fn request_runtime_exit(app: &tauri::AppHandle, trigger: &str, exit_code: i32) {
    prepare_runtime_exit(app, trigger);
    app.exit(exit_code);
}

enum MainWindowAction {
    Show,
    Toggle,
    ShowAndEmit(String),
}

fn build_main_window(app: &tauri::AppHandle) -> tauri::Result<tauri::WebviewWindow<tauri::Wry>> {
    tauri::WebviewWindowBuilder::new(
        app,
        CONTROL_CENTER_WINDOW_LABEL,
        tauri::WebviewUrl::App("index.html".into()),
    )
    .title("Desktop-Up · A0 Demo")
    .inner_size(1440.0, 900.0)
    .min_inner_size(1100.0, 720.0)
    .center()
    .visible(false)
    .resizable(true)
    .decorations(true)
    .transparent(false)
    .shadow(true)
    .build()
}

fn reveal_main_window(window: &tauri::WebviewWindow<tauri::Wry>) {
    let _ = window.show();
    let _ = window.unminimize();
    let _ = window.set_focus();
}

fn refresh_runtime_host_topology(app: &tauri::AppHandle, trigger: &str) {
    let host_topology = inspect_host_topology(app);
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    if let Ok(updated) = mutate_runtime_state(&state, trigger, move |snapshot| {
        snapshot.host_topology = host_topology;
    }) {
        publish_runtime_snapshot(app, updated);
    }
}

fn publish_control_center_runtime_snapshot(app: &tauri::AppHandle) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    if let Ok(snapshot) = state.0.lock().map(|snapshot| snapshot.clone()) {
        publish_runtime_snapshot(app, snapshot);
    };
}

fn queue_or_emit_main_control(
    app: &tauri::AppHandle,
    coordinator: &MainWindowCoordinator,
    payload: String,
) {
    match coordinator.lifecycle.queue_or_take_ready(payload) {
        Ok(Some(payload)) => {
            let _ = app.emit("runtime-control", payload);
        }
        Ok(None) => {}
        Err(error) => eprintln!("failed to queue control-center action: {error}"),
    }
    publish_control_center_runtime_snapshot(app);
}

fn mark_main_window_ready(
    app: &tauri::AppHandle,
) -> Result<ControlCenterLifecycleSnapshot, String> {
    let coordinator = app.state::<MainWindowCoordinator>();
    let pending = coordinator.lifecycle.mark_ready()?;
    refresh_runtime_host_topology(app, "controlCenterReady");
    for payload in pending {
        let _ = app.emit("runtime-control", payload);
    }
    coordinator.lifecycle.snapshot()
}

#[tauri::command]
fn mark_control_center_ready(
    app: tauri::AppHandle,
) -> Result<ControlCenterLifecycleSnapshot, String> {
    mark_main_window_ready(&app)
}

fn release_main_window(
    app: &tauri::AppHandle,
    coordinator: &MainWindowCoordinator,
    window: &tauri::Window<tauri::Wry>,
    trigger: &str,
) {
    match window.destroy() {
        Ok(()) => {
            if let Err(error) = coordinator.lifecycle.window_destroyed() {
                eprintln!("failed to record control-center release: {error}");
            }
            refresh_runtime_host_topology(app, trigger);
        }
        Err(error) => {
            eprintln!("failed to release control-center resources, hiding instead: {error}");
            let _ = window.hide();
            if let Err(error) = coordinator.lifecycle.window_hidden() {
                eprintln!("failed to record control-center hide fallback: {error}");
            }
            publish_control_center_runtime_snapshot(app);
        }
    }
}

fn run_main_window_action(app: tauri::AppHandle, action: MainWindowAction) {
    let coordinator = app.state::<MainWindowCoordinator>();
    let _execution = match coordinator.execution.lock() {
        Ok(execution) => execution,
        Err(error) => {
            eprintln!("failed to lock control-center creation: {error}");
            return;
        }
    };
    let existed = app
        .get_webview_window(CONTROL_CENTER_WINDOW_LABEL)
        .is_some();
    let window = match app.get_webview_window(CONTROL_CENTER_WINDOW_LABEL) {
        Some(window) => window,
        None => match build_main_window(&app) {
            Ok(window) => {
                if let Err(error) = coordinator.lifecycle.window_created() {
                    eprintln!("failed to record control-center creation: {error}");
                }
                refresh_runtime_host_topology(&app, "controlCenterCreated");
                window
            }
            Err(error) => {
                eprintln!("failed to create control-center window: {error}");
                return;
            }
        },
    };

    if matches!(action, MainWindowAction::Toggle) && existed && window.is_visible().unwrap_or(false)
    {
        let release_window = window.as_ref().window();
        release_main_window(
            &app,
            coordinator.inner(),
            &release_window,
            "controlCenterToggleReleased",
        );
        return;
    }

    reveal_main_window(&window);
    if let Err(error) = coordinator.lifecycle.window_shown() {
        eprintln!("failed to record control-center show: {error}");
    }
    if let MainWindowAction::ShowAndEmit(payload) = action {
        queue_or_emit_main_control(&app, coordinator.inner(), payload);
    } else {
        publish_control_center_runtime_snapshot(&app);
    }
}

fn schedule_main_window_action(app: &tauri::AppHandle, action: MainWindowAction) {
    let app = app.clone();
    std::thread::spawn(move || run_main_window_action(app, action));
}

fn show_main_window(app: &tauri::AppHandle) {
    schedule_main_window_action(app, MainWindowAction::Show);
}

fn toggle_main_window(app: &tauri::AppHandle) {
    schedule_main_window_action(app, MainWindowAction::Toggle);
}

fn toggle_poster_window(app: &tauri::AppHandle) {
    let visible = app
        .try_state::<RuntimeState>()
        .and_then(|state| state.0.lock().ok().map(|snapshot| snapshot.poster_visible))
        .unwrap_or(false);
    set_poster_visibility(app, !visible);
}

fn show_and_emit(app: &tauri::AppHandle, payload: &str) {
    schedule_main_window_action(app, MainWindowAction::ShowAndEmit(payload.to_string()));
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    if let Some(exit_code) = poster_state_recovery_gate::run_if_requested() {
        std::process::exit(exit_code);
    }

    let initial_launch_intent = {
        #[cfg(feature = "autostart-test-root")]
        {
            if autostart_test_gate::is_initial_forward_request() {
                launch_intent::decide_initial_application_arguments(&[])
            } else {
                decide_initial_from_env()
            }
        }
        #[cfg(not(feature = "autostart-test-root"))]
        decide_initial_from_env()
    };
    if initial_launch_intent.disposition == LaunchIntentDisposition::Reject {
        if let Ok(diagnostic) = serde_json::to_string(&initial_launch_intent) {
            eprintln!("Desktop-Up rejected launch intent: {diagnostic}");
        }
        std::process::exit(INVALID_LAUNCH_EXIT_CODE);
    }

    let application = tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, args, _cwd| {
            #[cfg(feature = "autostart-test-root")]
            if autostart_test_gate::try_handle_forwarded(app, &args) {
                return;
            }
            let decision = decide_secondary_forwarded_arguments(&args);
            if let Some(coordinator) = app.try_state::<LaunchIntentCoordinator>() {
                coordinator.record_secondary(decision.clone());
            }
            match decision.disposition {
                LaunchIntentDisposition::ShowControlCenter => show_main_window(app),
                LaunchIntentDisposition::KeepExistingUi => {}
                LaunchIntentDisposition::Reject => {
                    if let Ok(diagnostic) = serde_json::to_string(&decision) {
                        eprintln!("Desktop-Up rejected secondary launch intent: {diagnostic}");
                    }
                }
                LaunchIntentDisposition::StartRuntime
                | LaunchIntentDisposition::StartDiagnosticRuntime => {
                    eprintln!("Desktop-Up ignored an invalid secondary launch disposition");
                }
            }
        }))
        .manage(LaunchIntentCoordinator::new(initial_launch_intent))
        .manage(RuntimeState::default())
        .manage(HostSurfaceCoordinator::default())
        .manage(HostRecoveryCoordinator::default())
        .manage(PosterSurfaceLifecycle::default())
        .manage(MainWindowCoordinator::default())
        .manage(TaskbarRecoveryLifecycle::default())
        .manage(PerformanceClock(Instant::now()))
        .setup(|app| {
            let runtime_paths = install_paths::resolve_for_app(app.handle())?;
            let autostart_snapshot = probe_autostart(&runtime_paths);
            app.manage(AutostartCoordinator::new(autostart_snapshot.clone()));
            let placement_path = runtime_paths.placement_path();
            let placement_store = PlacementStore::open(placement_path);
            let startup_placement = placement_store.snapshot();
            app.manage(placement_store);
            let snapshot_path = runtime_paths.snapshot_bridge_path();
            app.manage(PosterSnapshotStore::open(snapshot_path));
            let poster_state_path = runtime_paths.poster_state_path();
            let poster_state_repository = PosterStateRepository::open(poster_state_path)?;
            let initial_all_locked = match poster_state_repository.load() {
                Ok(PosterStateLoadResult::Loaded { snapshot, .. }) => all_posters_locked(&snapshot),
                _ => false,
            };
            app.manage(poster_state_repository);
            app.manage(runtime_paths);

            if let Some(state) = app.try_state::<RuntimeState>() {
                let host_topology = inspect_host_topology(app.handle());
                if let Ok(mut snapshot) = state.0.lock() {
                    snapshot.host_topology = host_topology;
                    snapshot.all_locked = initial_all_locked;
                }
            }

            let runtime_menu_snapshot = app
                .state::<RuntimeState>()
                .0
                .lock()
                .map(|snapshot| snapshot.clone())
                .unwrap_or_default();

            let show_item = MenuItemBuilder::with_id("show", "打开 Desktop-Up 主页").build(app)?;
            let settings_item = MenuItemBuilder::with_id("settings", "设置…").build(app)?;
            let separator_one = PredefinedMenuItem::separator(app)?;

            let host_status_item = MenuItemBuilder::with_id("host-status", "桌面挂载：正在初始化")
                .enabled(false)
                .build(app)?;
            let runtime_status_item =
                MenuItemBuilder::with_id("runtime-status", "运行状态：正在初始化")
                    .enabled(false)
                    .build(app)?;

            let workspace_item = CheckMenuItemBuilder::with_id("workspace", "显示海报宿主")
                .checked(runtime_menu_snapshot.poster_visible)
                .build(app)?;
            let edit_item = CheckMenuItemBuilder::with_id("edit", "编辑海报布局")
                .checked(runtime_menu_snapshot.edit_mode)
                .build(app)?;
            let lock_item = CheckMenuItemBuilder::with_id("lock-all", "锁定全部海报")
                .checked(runtime_menu_snapshot.all_locked)
                .build(app)?;
            let pause_item = CheckMenuItemBuilder::with_id("pause", "暂停动态效果")
                .checked(runtime_menu_snapshot.manually_paused)
                .build(app)?;

            let theme_menu = SubmenuBuilder::new(app, "快捷主题")
                .text("theme-nocturne", "深夜行动 / NOCTURNE")
                .text("theme-ember", "余烬档案 / EMBER")
                .text("theme-glass", "玻璃陈列 / GLASS")
                .build()?;

            let separator_two = PredefinedMenuItem::separator(app)?;
            let agent_item =
                MenuItemBuilder::with_id("agent", "Agent 助手（需要 API Key）").build(app)?;
            let autostart_item = CheckMenuItemBuilder::with_id(
                "autostart",
                autostart_tray_label(&autostart_snapshot),
            )
            .checked(autostart_snapshot.effective_enabled)
            .enabled(autostart_control_available(&autostart_snapshot))
            .build(app)?;
            let reconnect_item =
                MenuItemBuilder::with_id("reconnect", "重新连接桌面宿主").build(app)?;
            let separator_three = PredefinedMenuItem::separator(app)?;
            let quit_item = MenuItemBuilder::with_id("quit", "退出 Desktop-Up").build(app)?;

            let menu = MenuBuilder::new(app)
                .items(&[
                    &show_item,
                    &settings_item,
                    &separator_one,
                    &host_status_item,
                    &runtime_status_item,
                    &workspace_item,
                    &edit_item,
                    &lock_item,
                    &pause_item,
                    &theme_menu,
                    &separator_two,
                    &agent_item,
                    &autostart_item,
                    &reconnect_item,
                    &separator_three,
                    &quit_item,
                ])
                .build()?;

            app.manage(TrayControls {
                host_status: host_status_item,
                runtime_status: runtime_status_item,
                workspace: workspace_item,
                edit: edit_item,
                lock_all: lock_item,
                pause: pause_item,
                autostart: autostart_item,
            });

            app.on_menu_event(|app, event| {
                handle_tray_menu_event(app, event.id.as_ref());
            });
            app.on_tray_icon_event(|app, event| {
                handle_tray_icon_event(app, &event);
            });
            build_tray_icon(app.handle(), &menu)?;
            app.manage(TraySurface {
                menu,
                execution: Mutex::new(()),
            });

            let taskbar_lifecycle = app.state::<TaskbarRecoveryLifecycle>().inner().clone();
            let taskbar_app = app.handle().clone();
            let taskbar_status_app = app.handle().clone();
            if let Err(error) = taskbar_recovery::install(
                taskbar_lifecycle.clone(),
                move |batch| recover_after_taskbar_created(&taskbar_app, batch),
                move || publish_taskbar_recovery_snapshot(&taskbar_status_app),
            ) {
                let _ = taskbar_lifecycle.mark_listener_failed(error.clone());
                publish_taskbar_recovery_snapshot(app.handle());
                eprintln!("failed to install TaskbarCreated recovery listener: {error}");
            }

            if let Some(poster) = optional_desktop_host_window(app.handle()) {
                if let Err(error) =
                    poster.set_background_color(Some(tauri::window::Color(0, 0, 0, 0)))
                {
                    eprintln!("failed to force the poster surface background transparent: {error}");
                }
                if let Some(placement) = startup_placement.as_ref() {
                    if let Err(error) = desktop_host::restore_poster_placement(&poster, placement) {
                        eprintln!("failed to restore saved poster placement: {error}");
                    }
                }
                let recovery_app = app.handle().clone();
                if let Err(error) =
                    desktop_host::install_shell_foreground_monitor(&poster, move |request| {
                        if let Err(error) = recover_desktop_host(&recovery_app, request) {
                            eprintln!("failed to recover desktop host: {error}");
                        }
                    })
                {
                    eprintln!("failed to install shell foreground monitor: {error}");
                }
                let monitor_app = app.handle().clone();
                match runtime_monitor::install(&poster, move |visibility| {
                    apply_runtime_visibility(&monitor_app, visibility);
                }) {
                    Ok(capabilities) => {
                        set_runtime_monitor_capabilities(app.handle(), capabilities)
                    }
                    Err(error) => {
                        eprintln!("failed to install runtime visibility monitor: {error}")
                    }
                }
            }

            if performance_probe_requested() {
                start_release_performance_probe(app.handle().clone());
            }

            Ok(())
        })
        .on_page_load(|webview, payload| {
            let app = webview.app_handle();
            if webview.label() == CONTROL_CENTER_WINDOW_LABEL {
                if payload.event() == PageLoadEvent::Started {
                    let coordinator = app.state::<MainWindowCoordinator>();
                    if let Err(error) = coordinator.lifecycle.page_load_started() {
                        eprintln!("failed to record control-center page load: {error}");
                    }
                    publish_control_center_runtime_snapshot(app);
                }
                return;
            }

            if webview.label() != DESKTOP_HOST_WINDOW_LABEL {
                return;
            }
            if payload.event() == PageLoadEvent::Started {
                reset_poster_surface_initialization(app);
                return;
            }
            if payload.event() != PageLoadEvent::Finished {
                return;
            }

            if let Err(error) = configure_poster_host(app, true) {
                eprintln!("failed to configure poster host window: {error}");
            }
            runtime_monitor::reevaluate_foreground();
        })
        .on_window_event(|window, event| {
            if window.label() == DESKTOP_HOST_WINDOW_LABEL {
                match event {
                    WindowEvent::Moved(_)
                        if POSTER_PLACEMENT_CAPTURE_ENABLED.load(Ordering::Acquire)
                            && !desktop_host::placement_recovery_in_progress() =>
                    {
                        if let Ok(placement) = desktop_host::capture_poster_window_placement(window)
                        {
                            if let Some(store) = window.try_state::<PlacementStore>() {
                                store.record(placement);
                            }
                        }
                    }
                    WindowEvent::Moved(_) => {}
                    WindowEvent::ScaleFactorChanged { .. } => {
                        desktop_host::request_host_recovery(
                            HostRecoveryTrigger::TauriScaleFactorChanged,
                        );
                    }
                    WindowEvent::Destroyed => {
                        if let Some(store) = window.try_state::<PlacementStore>() {
                            if let Err(error) = store.flush() {
                                eprintln!("failed to flush poster placement: {error}");
                            }
                        }
                    }
                    _ => {}
                }
                return;
            }

            if window.label() != CONTROL_CENTER_WINDOW_LABEL {
                return;
            }

            match event {
                WindowEvent::CloseRequested { api, .. } => {
                    api.prevent_close();
                    let coordinator = window.state::<MainWindowCoordinator>();
                    release_main_window(
                        window.app_handle(),
                        coordinator.inner(),
                        window,
                        "controlCenterCloseReleased",
                    );
                }
                WindowEvent::Destroyed => {
                    let coordinator = window.state::<MainWindowCoordinator>();
                    if let Err(error) = coordinator.lifecycle.window_destroyed() {
                        eprintln!("failed to record control-center destruction: {error}");
                    }
                    refresh_runtime_host_topology(window.app_handle(), "controlCenterDestroyed");
                }
                _ => {}
            }
        })
        .invoke_handler(tauri::generate_handler![
            runtime_state,
            mark_control_center_ready,
            inspect_poster_snapshot,
            load_poster_state,
            replace_poster_state,
            migrate_poster_state,
            poster_state_bootstrap_context,
            desktop_host_diagnostics,
            install_path_diagnostics,
            install_content_probe,
            autostart_diagnostics,
            set_autostart_enabled,
            update_runtime_state,
            launch_intent_diagnostics,
            start_poster_drag,
            sync_host_surface,
            mark_poster_surface_ready,
            resize_poster_surface,
            set_poster_interaction_region,
            reconnect_desktop_host,
            get_performance_snapshot,
            report_poster_fps,
            save_performance_baseline
        ])
        .build(tauri::generate_context!())
        .expect("failed to build Desktop-Up");

    application.run(|app, event| {
        if let tauri::RunEvent::ExitRequested { .. } = event {
            prepare_runtime_exit(app, "exitRequested");
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn poster_state_bootstrap_uses_host_confirmed_primary_logical_work_area() {
        let topology = DisplayTopology {
            displays: vec![
                DisplayDescriptor {
                    display_id: "secondary".to_string(),
                    is_primary: false,
                    scale_factor: 1.25,
                    renderer_scale_factor: 1.25,
                    bounds: PhysicalRect {
                        x: -1920,
                        y: 0,
                        width: 1920,
                        height: 1080,
                    },
                    work_area: PhysicalRect::default(),
                    effective_logical_work_area: LogicalSize {
                        width: 1536.0,
                        height: 824.0,
                    },
                    responsive_scale: 1.0,
                },
                DisplayDescriptor {
                    display_id: "primary".to_string(),
                    is_primary: true,
                    scale_factor: 1.5,
                    renderer_scale_factor: 1.5,
                    bounds: PhysicalRect {
                        x: 0,
                        y: 0,
                        width: 2560,
                        height: 1440,
                    },
                    work_area: PhysicalRect::default(),
                    effective_logical_work_area: LogicalSize {
                        width: 1706.666_666_666_7,
                        height: 920.0,
                    },
                    responsive_scale: 1.0,
                },
            ],
            ..DisplayTopology::default()
        };
        let context = poster_state_context_from_topology(&topology).unwrap();
        assert_eq!(context.primary_work_area.display_id, "primary");
        assert!(context.primary_work_area.is_primary);
        assert_eq!(context.primary_work_area.width, 1706.666_666_666_7);
        assert_eq!(context.primary_work_area.height, 920.0);
    }

    #[test]
    fn tray_lock_and_theme_mutations_target_formal_poster_fields() {
        let mut snapshot = serde_json::json!({
            "selectedPosterId": "poster-b",
            "posters": [
                { "id": "poster-a", "isLocked": false, "templateKey": "transparent-default" },
                { "id": "poster-b", "isLocked": false, "templateKey": "transparent-default" }
            ]
        });

        assert!(toggle_all_poster_locks_snapshot(&mut snapshot).unwrap());
        assert!(all_posters_locked(&snapshot));
        assert!(toggle_all_poster_locks_snapshot(&mut snapshot).unwrap());
        assert!(!all_posters_locked(&snapshot));

        assert!(apply_quick_theme_snapshot(&mut snapshot, "builtin-ember").unwrap());
        assert_eq!(snapshot["posters"][0]["templateKey"], "transparent-default");
        assert_eq!(snapshot["posters"][1]["templateKey"], "builtin-ember");
        assert!(!apply_quick_theme_snapshot(&mut snapshot, "builtin-ember").unwrap());
    }

    #[test]
    fn runtime_snapshot_serializes_with_frontend_camel_case_contract() {
        let value = serde_json::to_value(RuntimeSnapshot::default()).unwrap();
        let object = value
            .as_object()
            .expect("snapshot must serialize to an object");
        for key in [
            "contractVersion",
            "editMode",
            "manuallyPaused",
            "automaticallyPaused",
            "effectivePaused",
            "allLocked",
            "posterVisible",
            "hostSurfaceReady",
            "pauseReasons",
            "posterFps",
            "performanceMode",
            "hostSurface",
            "displayTopology",
            "hostTopology",
            "desktopHost",
            "runtimeVisibility",
            "runtimeMonitor",
            "runtimeLifecycle",
            "controlCenter",
        ] {
            assert!(
                object.contains_key(key),
                "missing frontend contract field {key}"
            );
        }
        assert_eq!(value["contractVersion"], HOST_CONTRACT_VERSION);
        assert_eq!(value["hostSurface"]["geometryOwner"], "nativeHost");
        assert_eq!(
            value["displayTopology"]["coordinateSpace"],
            "physicalPixels"
        );
        assert_eq!(
            value["runtimeLifecycle"]["contractVersion"],
            runtime_lifecycle::RUNTIME_LIFECYCLE_CONTRACT_VERSION
        );
        assert_eq!(
            value["controlCenter"]["contractVersion"],
            control_center_lifecycle::CONTROL_CENTER_LIFECYCLE_CONTRACT_VERSION
        );
        assert!(value.get("exitRequested").is_none());
        for key in [
            "recoveryCount",
            "recoveryGeneration",
            "recoveryEventCount",
            "lastRecoveryTrigger",
            "lastRecoveryAction",
            "lastRecoveryError",
        ] {
            assert!(
                value["desktopHost"].get(key).is_some(),
                "missing desktop recovery diagnostic field {key}"
            );
        }
    }

    #[test]
    fn pause_reasons_are_promoted_to_the_top_level_snapshot() {
        let mut snapshot = RuntimeSnapshot::default();
        snapshot.runtime_visibility = RuntimeVisibilitySnapshot {
            automatically_paused: true,
            pause_reasons: vec!["displayOff".to_string()],
            ..Default::default()
        };
        snapshot.refresh_runtime_lifecycle("testVisibility");
        assert_eq!(snapshot.pause_reasons, ["displayOff"]);
        assert!(snapshot.effective_paused);
    }

    #[test]
    fn pause_state_follows_auto_runtime_contract() {
        let mut snapshot = RuntimeSnapshot::default();
        assert_eq!(snapshot.pause_state(), "stopped");

        snapshot.host_surface_ready = true;
        snapshot.refresh_runtime_lifecycle("hostSurfaceReady");
        assert_eq!(snapshot.pause_state(), "activeStatic");

        snapshot.poster_fps = 30.0;
        snapshot.refresh_runtime_lifecycle("posterFps");
        assert_eq!(snapshot.pause_state(), "activeAnimated");

        snapshot.manually_paused = true;
        snapshot.refresh_runtime_lifecycle("manualPause");
        assert_eq!(snapshot.pause_state(), "paused");

        snapshot.manually_paused = false;
        snapshot.poster_visible = false;
        snapshot.refresh_runtime_lifecycle("posterVisibility");
        assert_eq!(snapshot.pause_state(), "stopped");
    }

    #[test]
    fn performance_mode_cannot_override_lifecycle_safety() {
        let mut snapshot = RuntimeSnapshot::default();
        snapshot.host_surface_ready = true;
        snapshot.performance_mode = "paused".to_string();
        snapshot.refresh_runtime_lifecycle("performanceMode");
        assert_eq!(snapshot.pause_state(), "paused");

        snapshot.performance_mode = "activeAnimated".to_string();
        snapshot.manually_paused = true;
        snapshot.refresh_runtime_lifecycle("manualPause");
        assert_eq!(snapshot.pause_state(), "paused");
    }

    #[test]
    fn performance_baseline_merge_replaces_only_incoming_modes() {
        let existing = serde_json::json!({
            "samples": [
                {"mode": "stopped"},
                {"mode": "activeStatic"}
            ]
        });
        let incoming = vec![
            serde_json::json!({"mode": "activeStatic"}),
            serde_json::json!({"mode": "activeAnimated"}),
        ];
        let merged = merge_performance_samples(Some(&existing), incoming);
        let modes: Vec<_> = merged
            .iter()
            .filter_map(|sample| sample["mode"].as_str())
            .collect();
        assert_eq!(modes, ["stopped", "activeStatic", "activeAnimated"]);
    }

    #[test]
    fn performance_snapshot_serializes_with_frontend_camel_case_contract() {
        let snapshot = PerformanceSnapshot {
            memory_bytes: 1234,
            private_working_set_bytes: 456,
            private_commit_bytes: 789,
            process_count: 7,
            process_tree_working_set_bytes: 2345,
            process_tree_private_working_set_bytes: 1234,
            process_tree_private_commit_bytes: 3456,
            process_tree_process_count: 6,
            process_tree_measurement_complete: true,
            poster_fps: 30.0,
            cpu_ms: 88,
            pause_state: "activeAnimated".to_string(),
            elapsed_ms: 99,
            build_profile: "release".to_string(),
            host_contract_version: HOST_CONTRACT_VERSION,
            performance_gate_contract: PERFORMANCE_GATE_CONTRACT.to_string(),
        };
        let value = serde_json::to_value(snapshot).unwrap();
        let object = value
            .as_object()
            .expect("snapshot must serialize to an object");
        for key in [
            "memoryBytes",
            "privateWorkingSetBytes",
            "privateCommitBytes",
            "processCount",
            "processTreeWorkingSetBytes",
            "processTreePrivateWorkingSetBytes",
            "processTreePrivateCommitBytes",
            "processTreeProcessCount",
            "processTreeMeasurementComplete",
            "posterFps",
            "cpuMs",
            "pauseState",
            "elapsedMs",
            "buildProfile",
            "hostContractVersion",
            "performanceGateContract",
        ] {
            assert!(object.contains_key(key), "missing performance field {key}");
        }
    }

    #[test]
    fn visual_region_is_fitted_when_the_host_collapses() {
        let mut region = InteractionRegion {
            region_id: "poster-primary-visual".to_string(),
            rect: LogicalRect {
                x: 10.0,
                y: 10.0,
                width: 490.0,
                height: 392.0,
            },
            corner_radius: 18.0,
        };

        fit_region_to_logical_size(&mut region, 510.0, 136.0);
        assert_eq!(region.rect.width, 490.0);
        assert_eq!(region.rect.height, 126.0);
        assert_eq!(region.corner_radius, 18.0);
    }

    #[test]
    fn poster_surface_size_never_exceeds_the_monitor_work_area() {
        assert_eq!(
            clamp_surface_physical_size((1800, 1400), Some((1920, 1080))),
            (1800, 1080)
        );
        assert_eq!(clamp_surface_physical_size((900, 700), None), (900, 700));
    }
}
