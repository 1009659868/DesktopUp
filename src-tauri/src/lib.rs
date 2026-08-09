mod desktop_host;
mod host_contract;
mod host_topology;
mod placement;
mod poster_snapshot;
mod runtime_monitor;

use std::sync::{
    atomic::{AtomicBool, Ordering},
    Mutex,
};
use std::time::Instant;

use desktop_host::DesktopHostDiagnostics;
use host_contract::{
    virtual_bounds, DisplayDescriptor, DisplayTopology, HostPlacementIntent, HostSurfaceRequest,
    HostSurfaceSnapshot, InteractionRegion, LogicalRect, LogicalSize, PhysicalRect,
    ResponsiveScalePolicy, HOST_CONTRACT_VERSION,
};
use host_topology::{
    desktop_host_window, effective_logical_work_area, inspect_host_topology,
    optional_desktop_host_window, responsive_scale_for_work_area, HostTopologySnapshot,
    DESKTOP_HOST_WINDOW_LABEL,
};
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
) -> Result<(), String> {
    match intent {
        HostPlacementIntent::Preserve => {
            if let Some(placement) = placement_store.snapshot() {
                desktop_host::restore_poster_placement(window, &placement)?;
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

fn apply_host_surface_request(
    app: &tauri::AppHandle,
    request: HostSurfaceRequest,
) -> Result<HostSurfaceSyncResult, String> {
    let validated = request.validate()?;
    let coordinator = app.state::<HostSurfaceCoordinator>();
    let _guard = coordinator.0.lock().map_err(|error| error.to_string())?;
    let runtime_state = app.state::<RuntimeState>();
    {
        let snapshot = runtime_state.0.lock().map_err(|error| error.to_string())?;
        if validated.request.revision < snapshot.host_surface.revision {
            return Ok(HostSurfaceSyncResult::from_runtime(&snapshot));
        }
    }

    let window = desktop_host_window(app)?;
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
    )?;
    if let Some(region) = validated.request.interaction_region.as_ref() {
        desktop_host::apply_interaction_region(
            &window,
            region.rect.x,
            region.rect.y,
            region.rect.width,
            region.rect.height,
            region.corner_radius,
            validated.effective_scale_factor,
        )?;
    }
    let _ = window.set_title("");

    let mut host_surface = HostSurfaceSnapshot::from(validated);
    host_surface.physical_size.width = physical_width;
    host_surface.physical_size.height = physical_height;
    let mut snapshot = runtime_state.0.lock().map_err(|error| error.to_string())?;
    snapshot.host_surface = host_surface;
    snapshot.display_topology = display_topology;
    snapshot.host_topology = inspect_host_topology(app);
    snapshot.desktop_host.interaction_region_count =
        usize::from(snapshot.host_surface.interaction_region.is_some());
    snapshot.desktop_host.interaction_region = snapshot
        .host_surface
        .interaction_region
        .as_ref()
        .map(|region| {
            format!(
                "{}:{:.1},{:.1} {:.1}x{:.1} @{:.2}x",
                region.region_id,
                region.rect.x,
                region.rect.y,
                region.rect.width,
                region.rect.height,
                snapshot.host_surface.effective_scale_factor
            )
        });
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
    let request = HostSurfaceRequest {
        contract_version: HOST_CONTRACT_VERSION,
        revision: next_host_revision(&runtime_state)?,
        surface_id: current.surface_id,
        logical_size: current.logical_size,
        scale_factor: current.device_scale_factor,
        user_scale: current.user_scale,
        placement_intent: HostPlacementIntent::Preserve,
        interaction_region: current.interaction_region,
    };
    apply_host_surface_request(app, request).map(Some)
}

#[tauri::command]
fn start_poster_drag(app: tauri::AppHandle) -> Result<(), String> {
    desktop_host_window(&app)?
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
    let interaction_region = current.interaction_region.filter(|region| {
        region.rect.x + region.rect.width <= logical_width
            && region.rect.y + region.rect.height <= logical_height
    });
    let request = HostSurfaceRequest {
        contract_version: HOST_CONTRACT_VERSION,
        revision: next_host_revision(&runtime_state)?,
        surface_id: current.surface_id,
        logical_size: LogicalSize {
            width: logical_width,
            height: logical_height,
        },
        scale_factor: scale,
        user_scale: current.user_scale,
        placement_intent: HostPlacementIntent::Preserve,
        interaction_region,
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
    };
    apply_host_surface_request(&app, request).map(|result| result.desktop_host)
}

fn configure_poster_host(
    app: &tauri::AppHandle,
    prefer_workerw: bool,
) -> Result<DesktopHostDiagnostics, String> {
    let window = desktop_host_window(app)?;
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
            snapshot.display_topology = resolve_display_topology(&window);
            snapshot.host_topology = inspect_host_topology(app);
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
    let surface_ready = app
        .try_state::<PosterSurfaceLifecycle>()
        .map(|lifecycle| lifecycle.0.load(Ordering::Acquire))
        .unwrap_or(false);
    let native_visible = visible && surface_ready;
    desktop_host::set_poster_visibility_intent(native_visible);
    if let Some(window) = optional_desktop_host_window(app) {
        if native_visible {
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

fn reset_poster_surface_initialization(app: &tauri::AppHandle) {
    if let Some(lifecycle) = app.try_state::<PosterSurfaceLifecycle>() {
        lifecycle.0.store(false, Ordering::Release);
    }
    desktop_host::set_poster_visibility_intent(false);
    if let Some(window) = optional_desktop_host_window(app) {
        let _ = window.hide();
    }
    if let Some(state) = app.try_state::<RuntimeState>() {
        if let Ok(mut snapshot) = state.0.lock() {
            snapshot.host_surface_ready = false;
        }
    }
}

#[tauri::command]
fn mark_poster_surface_ready(revision: u64, app: tauri::AppHandle) -> Result<bool, String> {
    if revision == 0 {
        return Err("poster surface readiness requires a synchronized revision".to_string());
    }

    let (should_show, host_surface) = {
        let state = app.state::<RuntimeState>();
        let snapshot = state.0.lock().map_err(|error| error.to_string())?;
        if snapshot.host_surface.revision < revision {
            return Err(format!(
                "poster surface revision {} is not synchronized yet",
                revision
            ));
        }
        (snapshot.poster_visible, snapshot.host_surface.clone())
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
    if let Some(region) = host_surface.interaction_region.as_ref() {
        // SWP_FRAMECHANGED in host configuration must run before the final region write.
        // Reapplying the region last invalidates the stale Windows 11/DWM non-client strip
        // that otherwise survives until the first user-initiated collapse/resize.
        desktop_host::apply_interaction_region(
            &window,
            region.rect.x,
            region.rect.y,
            region.rect.width,
            region.rect.height,
            region.corner_radius,
            host_surface.effective_scale_factor,
        )?;
    }
    lifecycle.0.store(true, Ordering::Release);
    if let Ok(mut snapshot) = app.state::<RuntimeState>().0.lock() {
        snapshot.host_surface_ready = true;
    }
    set_poster_visibility(&app, should_show);
    runtime_monitor::reevaluate_foreground();
    Ok(true)
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
        .try_state::<RuntimeState>()
        .and_then(|state| state.0.lock().ok().map(|snapshot| snapshot.poster_visible))
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
        .manage(HostSurfaceCoordinator::default())
        .manage(PosterSurfaceLifecycle::default())
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

            if let Some(state) = app.try_state::<RuntimeState>() {
                if let Ok(mut snapshot) = state.0.lock() {
                    snapshot.host_topology = inspect_host_topology(app.handle());
                }
            }

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
            if webview.label() != DESKTOP_HOST_WINDOW_LABEL {
                return;
            }

            let app = webview.app_handle();
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
                        if let Err(error) =
                            resync_host_surface_for_display_change(window.app_handle())
                        {
                            eprintln!("failed to rescale desktop host after DPI change: {error}");
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
            sync_host_surface,
            mark_poster_surface_ready,
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
