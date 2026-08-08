mod desktop_host;
mod placement;
mod poster_snapshot;
mod runtime_monitor;

use std::sync::{
    atomic::{AtomicBool, Ordering},
    Mutex,
};
use std::time::Instant;

use desktop_host::DesktopHostDiagnostics;
use placement::PlacementStore;
use poster_snapshot::PosterSnapshotStore;
use runtime_monitor::{RuntimeMonitorCapabilities, RuntimeVisibilitySnapshot};
use serde::Serialize;
use tauri::{
    menu::{
        CheckMenuItem, CheckMenuItemBuilder, MenuBuilder, MenuItem, MenuItemBuilder,
        PredefinedMenuItem, SubmenuBuilder,
    },
    tray::{MouseButton, TrayIconBuilder, TrayIconEvent},
    webview::PageLoadEvent,
    Emitter, Manager, State, WindowEvent,
};

static POSTER_PLACEMENT_CAPTURE_ENABLED: AtomicBool = AtomicBool::new(false);

struct PerformanceClock(Instant);

struct RuntimeState(Mutex<RuntimeSnapshot>);

impl Default for RuntimeState {
    fn default() -> Self {
        Self(Mutex::new(RuntimeSnapshot::default()))
    }
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct RuntimeSnapshot {
    edit_mode: bool,
    manually_paused: bool,
    automatically_paused: bool,
    effective_paused: bool,
    all_locked: bool,
    poster_visible: bool,
    pause_reasons: Vec<String>,
    poster_fps: f64,
    performance_mode: String,
    desktop_host: DesktopHostDiagnostics,
    runtime_visibility: RuntimeVisibilitySnapshot,
    runtime_monitor: RuntimeMonitorCapabilities,
}

impl Default for RuntimeSnapshot {
    fn default() -> Self {
        Self {
            edit_mode: false,
            manually_paused: false,
            automatically_paused: false,
            effective_paused: false,
            all_locked: false,
            poster_visible: true,
            pause_reasons: Vec::new(),
            poster_fps: 0.0,
            performance_mode: "auto".to_string(),
            desktop_host: DesktopHostDiagnostics::default(),
            runtime_visibility: RuntimeVisibilitySnapshot::default(),
            runtime_monitor: RuntimeMonitorCapabilities::default(),
        }
    }
}

impl RuntimeSnapshot {
    fn refresh_pause_state(&mut self) {
        self.automatically_paused = self.runtime_visibility.automatically_paused;
        self.pause_reasons = self.runtime_visibility.pause_reasons.clone();
        self.effective_paused = self.manually_paused || self.automatically_paused;
    }

    fn pause_state(&self) -> String {
        match self.performance_mode.as_str() {
            "activeStatic" | "activeAnimated" | "paused" | "stopped" => {
                self.performance_mode.clone()
            }
            _ => {
                if !self.poster_visible {
                    "stopped".to_string()
                } else if self.effective_paused {
                    "paused".to_string()
                } else if self.poster_fps > 0.0 {
                    "activeAnimated".to_string()
                } else {
                    "activeStatic".to_string()
                }
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
}

impl TrayControls {
    fn sync(&self, snapshot: &RuntimeSnapshot) {
        let _ = self
            .host_status
            .set_text(snapshot.desktop_host.tray_label());
        let _ = self.runtime_status.set_text(
            snapshot
                .runtime_visibility
                .tray_label(snapshot.manually_paused),
        );
        let _ = self.workspace.set_checked(snapshot.poster_visible);
        let _ = self.edit.set_checked(snapshot.edit_mode);
        let _ = self.lock_all.set_checked(snapshot.all_locked);
        let _ = self.pause.set_checked(snapshot.manually_paused);
    }
}

fn emit_runtime_pause_state(app: &tauri::AppHandle, snapshot: &RuntimeSnapshot) {
    let _ = app.emit("runtime-pause-state", snapshot.clone());
}

fn schedule_runtime_outputs(
    app: &tauri::AppHandle,
    snapshot: RuntimeSnapshot,
    emit_pause_state: bool,
) {
    let ui_app = app.clone();
    if let Err(error) = app.run_on_main_thread(move || {
        if let Some(tray) = ui_app.try_state::<TrayControls>() {
            tray.sync(&snapshot);
        }
        if emit_pause_state {
            emit_runtime_pause_state(&ui_app, &snapshot);
        }
    }) {
        eprintln!("failed to schedule runtime UI synchronization: {error}");
    }
}

fn apply_runtime_visibility(app: &tauri::AppHandle, visibility: RuntimeVisibilitySnapshot) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    let Ok(mut snapshot) = state.0.lock() else {
        return;
    };
    snapshot.runtime_visibility = visibility;
    snapshot.refresh_pause_state();
    runtime_monitor::publish_diagnostics(&snapshot.runtime_visibility, snapshot.manually_paused);
    let updated = snapshot.clone();
    drop(snapshot);
    schedule_runtime_outputs(app, updated, true);
}

fn set_runtime_monitor_capabilities(
    app: &tauri::AppHandle,
    capabilities: RuntimeMonitorCapabilities,
) {
    runtime_monitor::publish_capabilities(&capabilities);
    if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(mut snapshot) = state.0.lock() {
            snapshot.runtime_monitor = capabilities;
        }
    }
}

fn toggle_manual_pause(app: &tauri::AppHandle) {
    let Some(state) = app.try_state::<RuntimeState>() else {
        return;
    };
    let Ok(mut snapshot) = state.0.lock() else {
        return;
    };
    snapshot.manually_paused = !snapshot.manually_paused;
    snapshot.refresh_pause_state();
    runtime_monitor::publish_diagnostics(&snapshot.runtime_visibility, snapshot.manually_paused);
    let updated = snapshot.clone();
    drop(snapshot);
    schedule_runtime_outputs(app, updated, true);
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
    let mut snapshot = state.0.lock().map_err(|error| error.to_string())?;
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
    snapshot.refresh_pause_state();
    runtime_monitor::publish_diagnostics(&snapshot.runtime_visibility, snapshot.manually_paused);
    let updated = snapshot.clone();
    drop(snapshot);
    schedule_runtime_outputs(&app, updated.clone(), true);
    Ok(updated)
}

#[tauri::command]
fn load_poster_snapshot(store: State<'_, PosterSnapshotStore>) -> Option<serde_json::Value> {
    store.snapshot()
}

#[tauri::command]
fn save_poster_snapshot(
    snapshot: serde_json::Value,
    app: tauri::AppHandle,
    store: State<'_, PosterSnapshotStore>,
) -> Result<(), String> {
    store.replace(snapshot.clone())?;
    app.emit("poster-snapshot-updated", snapshot)
        .map_err(|error| error.to_string())
}

#[tauri::command]
fn runtime_state(state: State<'_, RuntimeState>) -> Result<RuntimeSnapshot, String> {
    state
        .0
        .lock()
        .map(|snapshot| snapshot.clone())
        .map_err(|error| error.to_string())
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
fn start_poster_drag(app: tauri::AppHandle) -> Result<(), String> {
    app.get_webview_window("poster")
        .ok_or_else(|| "poster window is unavailable".to_string())?
        .start_dragging()
        .map_err(|error| error.to_string())
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

#[tauri::command]
fn resize_poster_surface(
    collapsed: bool,
    width: f64,
    height: f64,
    surface_gutter: f64,
    device_scale_factor: f64,
    app: tauri::AppHandle,
    placement_store: State<'_, PlacementStore>,
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
    let window = app
        .get_webview_window("poster")
        .ok_or_else(|| "poster window is unavailable".to_string())?;
    let (physical_width, physical_height) = clamp_surface_physical_size(
        (
            (logical_width * scale).round() as u32,
            (logical_height * scale).round() as u32,
        ),
        desktop_host::poster_work_area_size(&window),
    );
    window
        .set_size(tauri::PhysicalSize::new(physical_width, physical_height))
        .map_err(|error| error.to_string())?;
    if let Some(placement) = placement_store.snapshot() {
        desktop_host::restore_poster_placement(&window, &placement)?;
    }
    Ok(())
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
    let window = app
        .get_webview_window("poster")
        .ok_or_else(|| "poster window is unavailable".to_string())?;
    desktop_host::apply_interaction_region(
        &window,
        x.clamp(0.0, 64.0),
        y.clamp(0.0, 64.0),
        width.clamp(1.0, 1920.0),
        height.clamp(1.0, 2160.0),
        corner_radius.clamp(0.0, 48.0),
        device_scale_factor.clamp(0.5, 4.0),
    )?;

    let runtime_state = app.state::<RuntimeState>();
    let mut snapshot = runtime_state.0.lock().map_err(|error| error.to_string())?;
    snapshot.desktop_host.interaction_region_count = 1;
    snapshot.desktop_host.interaction_region = Some(format!(
        "{:.1},{:.1} {:.1}x{:.1} @{:.2}x",
        x, y, width, height, device_scale_factor
    ));
    let updated = snapshot.clone();
    let diagnostics = snapshot.desktop_host.clone();
    drop(snapshot);
    schedule_runtime_outputs(&app, updated, false);
    let _ = window.set_title("");
    Ok(diagnostics)
}

fn configure_poster_host(
    app: &tauri::AppHandle,
    prefer_workerw: bool,
) -> Result<DesktopHostDiagnostics, String> {
    let window = app
        .get_webview_window("poster")
        .ok_or_else(|| "poster window is unavailable".to_string())?;
    POSTER_PLACEMENT_CAPTURE_ENABLED.store(false, Ordering::Release);
    let diagnostics = desktop_host::configure(&window, prefer_workerw);
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

    let updated = if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(mut snapshot) = state.0.lock() {
            snapshot.desktop_host = diagnostics.clone();
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

#[tauri::command]
fn reconnect_desktop_host(app: tauri::AppHandle) -> Result<DesktopHostDiagnostics, String> {
    configure_poster_host(&app, true)
}

#[cfg(target_os = "windows")]
fn current_process_memory_bytes() -> u64 {
    use windows::Win32::System::ProcessStatus::{K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS};
    use windows::Win32::System::Threading::GetCurrentProcess;

    let mut counters = PROCESS_MEMORY_COUNTERS {
        cb: std::mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32,
        ..Default::default()
    };
    let ok = unsafe {
        K32GetProcessMemoryInfo(
            GetCurrentProcess(),
            &mut counters,
            std::mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32,
        )
    };
    if ok.as_bool() {
        counters.WorkingSetSize as u64
    } else {
        0
    }
}

#[cfg(not(target_os = "windows"))]
fn current_process_memory_bytes() -> u64 {
    0
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
    process_count: u32,
    poster_fps: f64,
    cpu_ms: u64,
    pause_state: String,
    elapsed_ms: u64,
}

#[tauri::command]
fn get_performance_snapshot(
    app: tauri::AppHandle,
    state: State<'_, RuntimeState>,
) -> Result<PerformanceSnapshot, String> {
    let snapshot = state.0.lock().map_err(|error| error.to_string())?;
    let elapsed_ms = app
        .try_state::<PerformanceClock>()
        .map(|clock| clock.0.elapsed().as_millis() as u64)
        .unwrap_or(0);
    Ok(PerformanceSnapshot {
        memory_bytes: current_process_memory_bytes(),
        process_count: current_process_count(),
        poster_fps: snapshot.poster_fps,
        cpu_ms: current_process_cpu_ms(),
        pause_state: snapshot.pause_state(),
        elapsed_ms,
    })
}

#[tauri::command]
fn report_poster_fps(fps: f64, state: State<'_, RuntimeState>) -> Result<(), String> {
    let mut snapshot = state.0.lock().map_err(|error| error.to_string())?;
    snapshot.poster_fps = fps.clamp(0.0, 240.0);
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

#[tauri::command]
fn save_performance_baseline(
    samples: Vec<serde_json::Value>,
    app: tauri::AppHandle,
) -> Result<(), String> {
    let directory = app
        .path()
        .app_config_dir()
        .map_err(|error| error.to_string())?;
    std::fs::create_dir_all(&directory).map_err(|error| error.to_string())?;
    let path = directory.join("performance-baseline-v1.json");
    let existing = std::fs::read(&path)
        .ok()
        .and_then(|bytes| serde_json::from_slice::<serde_json::Value>(&bytes).ok());
    let samples = merge_performance_samples(existing.as_ref(), samples);
    let saved_at = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|duration| duration.as_secs())
        .unwrap_or(0);
    let mut payload = serde_json::json!({
        "version": 1,
        "savedAtUnixSeconds": saved_at,
        "samples": samples,
    });
    if let Some(existing) = existing.as_ref().and_then(|value| value.as_object()) {
        for key in ["machine", "sampling"] {
            if let Some(value) = existing.get(key) {
                payload[key] = value.clone();
            }
        }
    }
    if payload.get("sampling").is_none() {
        payload["sampling"] = serde_json::json!({
            "method": "Release control center; each mode collects 5 snapshots every 2000ms",
            "intervalMs": 2000,
            "samplesPerMode": 5,
        });
    }
    std::fs::write(
        path,
        serde_json::to_vec_pretty(&payload).map_err(|error| error.to_string())?,
    )
    .map_err(|error| error.to_string())
}

fn set_poster_visibility(app: &tauri::AppHandle, visible: bool) {
    desktop_host::set_poster_visibility_intent(visible);
    if let Some(window) = app.get_webview_window("poster") {
        if visible {
            let _ = window.show();
        } else {
            let _ = window.hide();
        }
    }

    let updated = if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(mut snapshot) = state.0.lock() {
            snapshot.poster_visible = visible;
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
}

fn show_main_window(app: &tauri::AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

fn toggle_main_window(app: &tauri::AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        if window.is_visible().unwrap_or(false) {
            let _ = window.hide();
        } else {
            let _ = window.show();
            let _ = window.unminimize();
            let _ = window.set_focus();
        }
    }
}

fn toggle_poster_window(app: &tauri::AppHandle) {
    let visible = app
        .get_webview_window("poster")
        .and_then(|window| window.is_visible().ok())
        .unwrap_or(false);
    set_poster_visibility(app, !visible);
}

fn show_and_emit(app: &tauri::AppHandle, payload: &str) {
    show_main_window(app);
    let _ = app.emit("runtime-control", payload);
}

fn emit_control(app: &tauri::AppHandle, payload: &str) {
    let _ = app.emit("runtime-control", payload);
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            show_main_window(app);
        }))
        .manage(RuntimeState::default())
        .manage(PerformanceClock(Instant::now()))
        .setup(|app| {
            let placement_path = app
                .path()
                .app_config_dir()?
                .join("poster-placement-v1.json");
            let placement_store = PlacementStore::open(placement_path);
            let startup_placement = placement_store.snapshot();
            app.manage(placement_store);
            let snapshot_path = app
                .path()
                .app_config_dir()?
                .join("poster-snapshot-bridge-v1.json");
            app.manage(PosterSnapshotStore::open(snapshot_path));

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
                .checked(true)
                .build(app)?;
            let edit_item = CheckMenuItemBuilder::with_id("edit", "编辑海报布局")
                .checked(false)
                .build(app)?;
            let lock_item = CheckMenuItemBuilder::with_id("lock-all", "锁定全部海报")
                .checked(false)
                .build(app)?;
            let pause_item = CheckMenuItemBuilder::with_id("pause", "暂停动态效果")
                .checked(false)
                .build(app)?;

            let theme_menu = SubmenuBuilder::new(app, "快捷主题")
                .text("theme-nocturne", "深夜行动 / NOCTURNE")
                .text("theme-ember", "余烬档案 / EMBER")
                .text("theme-glass", "玻璃陈列 / GLASS")
                .build()?;

            let separator_two = PredefinedMenuItem::separator(app)?;
            let agent_item =
                MenuItemBuilder::with_id("agent", "Agent 助手（需要 API Key）").build(app)?;
            let autostart_item = MenuItemBuilder::with_id("autostart", "开机自启（A1 接入）")
                .enabled(false)
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
            });

            TrayIconBuilder::with_id("desktop-up-tray")
                .tooltip("Desktop-Up · 游戏海报桌面")
                .menu(&menu)
                .show_menu_on_left_click(false)
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "show" => show_main_window(app),
                    "settings" => show_and_emit(app, "open-settings"),
                    "workspace" => toggle_poster_window(app),
                    "edit" => emit_control(app, "toggle-edit"),
                    "lock-all" => emit_control(app, "toggle-lock-all"),
                    "pause" => toggle_manual_pause(app),
                    "theme-nocturne" => emit_control(app, "theme:nocturne"),
                    "theme-ember" => emit_control(app, "theme:ember"),
                    "theme-glass" => emit_control(app, "theme:glass"),
                    "agent" => show_and_emit(app, "open-agent"),
                    "reconnect" => {
                        if let Err(error) = configure_poster_host(app, true) {
                            eprintln!("failed to reconnect desktop host: {error}");
                        }
                    }
                    "quit" => {
                        if let Some(store) = app.try_state::<PlacementStore>() {
                            if let Err(error) = store.flush() {
                                eprintln!("failed to flush poster placement before exit: {error}");
                            }
                        }
                        app.exit(0);
                    }
                    _ => {}
                })
                .on_tray_icon_event(|tray, event| match event {
                    TrayIconEvent::DoubleClick {
                        button: MouseButton::Left,
                        ..
                    } => toggle_main_window(tray.app_handle()),
                    _ => {}
                })
                .build(app)?;

            if let Some(poster) = app.get_webview_window("poster") {
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
                if let Err(error) = desktop_host::install_shell_foreground_monitor(&poster) {
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

            Ok(())
        })
        .on_page_load(|webview, payload| {
            if webview.label() != "poster" || payload.event() != PageLoadEvent::Finished {
                return;
            }

            let app = webview.app_handle();
            let should_show = app
                .try_state::<RuntimeState>()
                .and_then(|state| state.0.lock().ok().map(|snapshot| snapshot.poster_visible))
                .unwrap_or(true);
            if !should_show {
                return;
            }

            set_poster_visibility(app, true);
            if let Err(error) = configure_poster_host(app, true) {
                eprintln!("failed to configure poster host window: {error}");
            }
            runtime_monitor::reevaluate_foreground();
        })
        .on_window_event(|window, event| {
            if window.label() == "poster" {
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
                        if let Some(placement) = window
                            .try_state::<PlacementStore>()
                            .and_then(|store| store.snapshot())
                        {
                            if let Err(error) =
                                desktop_host::restore_poster_window_placement(window, &placement)
                            {
                                eprintln!("failed to recover poster after DPI change: {error}");
                            }
                        }
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

            if window.label() != "main" {
                return;
            }

            if let WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .invoke_handler(tauri::generate_handler![
            runtime_state,
            load_poster_snapshot,
            save_poster_snapshot,
            desktop_host_diagnostics,
            update_runtime_state,
            start_poster_drag,
            resize_poster_surface,
            set_poster_interaction_region,
            reconnect_desktop_host,
            get_performance_snapshot,
            report_poster_fps,
            save_performance_baseline
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Desktop-Up");
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn runtime_snapshot_serializes_with_frontend_camel_case_contract() {
        let value = serde_json::to_value(RuntimeSnapshot::default()).unwrap();
        let object = value
            .as_object()
            .expect("snapshot must serialize to an object");
        for key in [
            "editMode",
            "manuallyPaused",
            "automaticallyPaused",
            "effectivePaused",
            "allLocked",
            "posterVisible",
            "pauseReasons",
            "posterFps",
            "performanceMode",
            "desktopHost",
            "runtimeVisibility",
            "runtimeMonitor",
        ] {
            assert!(
                object.contains_key(key),
                "missing frontend contract field {key}"
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
        snapshot.refresh_pause_state();
        assert_eq!(snapshot.pause_reasons, ["displayOff"]);
        assert!(snapshot.effective_paused);
    }

    #[test]
    fn pause_state_follows_auto_runtime_contract() {
        let mut snapshot = RuntimeSnapshot::default();
        assert_eq!(snapshot.pause_state(), "activeStatic");

        snapshot.poster_fps = 30.0;
        assert_eq!(snapshot.pause_state(), "activeAnimated");

        snapshot.manually_paused = true;
        snapshot.refresh_pause_state();
        assert_eq!(snapshot.pause_state(), "paused");

        snapshot.manually_paused = false;
        snapshot.refresh_pause_state();
        snapshot.poster_visible = false;
        assert_eq!(snapshot.pause_state(), "stopped");
    }

    #[test]
    fn performance_mode_override_controls_pause_state() {
        let mut snapshot = RuntimeSnapshot::default();
        snapshot.performance_mode = "paused".to_string();
        assert_eq!(snapshot.pause_state(), "paused");

        snapshot.performance_mode = "activeAnimated".to_string();
        snapshot.effective_paused = true;
        assert_eq!(snapshot.pause_state(), "activeAnimated");
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
            process_count: 7,
            poster_fps: 30.0,
            cpu_ms: 88,
            pause_state: "activeAnimated".to_string(),
            elapsed_ms: 99,
        };
        let value = serde_json::to_value(snapshot).unwrap();
        let object = value
            .as_object()
            .expect("snapshot must serialize to an object");
        for key in [
            "memoryBytes",
            "processCount",
            "posterFps",
            "cpuMs",
            "pauseState",
            "elapsedMs",
        ] {
            assert!(object.contains_key(key), "missing performance field {key}");
        }
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
