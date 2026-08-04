mod desktop_host;
mod placement;
mod runtime_monitor;

use std::sync::{
    atomic::{AtomicBool, Ordering},
    Mutex,
};

use desktop_host::DesktopHostDiagnostics;
use placement::PlacementStore;
use runtime_monitor::{RuntimeMonitorCapabilities, RuntimeVisibilitySnapshot};
use serde::Serialize;
use tauri::{
    menu::{
        CheckMenuItem, CheckMenuItemBuilder, MenuBuilder, MenuItem, MenuItemBuilder,
        PredefinedMenuItem, SubmenuBuilder,
    },
    tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent},
    webview::PageLoadEvent,
    Emitter, Manager, State, WindowEvent,
};

static POSTER_PLACEMENT_CAPTURE_ENABLED: AtomicBool = AtomicBool::new(false);

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
            all_locked: true,
            poster_visible: true,
            desktop_host: DesktopHostDiagnostics::default(),
            runtime_visibility: RuntimeVisibilitySnapshot::default(),
            runtime_monitor: RuntimeMonitorCapabilities::default(),
        }
    }
}

impl RuntimeSnapshot {
    fn refresh_pause_state(&mut self) {
        self.automatically_paused = self.runtime_visibility.automatically_paused;
        self.effective_paused = self.manually_paused || self.automatically_paused;
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
    app: tauri::AppHandle,
    state: State<'_, RuntimeState>,
) -> Result<RuntimeSnapshot, String> {
    let mut snapshot = state.0.lock().map_err(|error| error.to_string())?;
    snapshot.edit_mode = edit_mode;
    snapshot.manually_paused = manually_paused;
    snapshot.all_locked = all_locked;
    snapshot.refresh_pause_state();
    runtime_monitor::publish_diagnostics(&snapshot.runtime_visibility, snapshot.manually_paused);
    let updated = snapshot.clone();
    drop(snapshot);
    schedule_runtime_outputs(&app, updated.clone(), true);
    Ok(updated)
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
    let logical_width = width.clamp(330.0, 640.0) + gutter * 2.0;
    let content_height = if collapsed {
        116.0
    } else {
        height.clamp(240.0, 560.0)
    };
    let logical_height = content_height + gutter * 2.0;
    let window = app
        .get_webview_window("poster")
        .ok_or_else(|| "poster window is unavailable".to_string())?;
    window
        .set_size(tauri::PhysicalSize::new(
            (logical_width * scale).round() as u32,
            (logical_height * scale).round() as u32,
        ))
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
        width.clamp(1.0, 640.0),
        height.clamp(1.0, 560.0),
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

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            show_main_window(app);
        }))
        .manage(RuntimeState::default())
        .setup(|app| {
            let placement_path = app
                .path()
                .app_config_dir()?
                .join("poster-placement-v1.json");
            let placement_store = PlacementStore::open(placement_path);
            let startup_placement = placement_store.snapshot();
            app.manage(placement_store);

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
                .checked(true)
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
                    "edit" => show_and_emit(app, "toggle-edit"),
                    "lock-all" => show_and_emit(app, "toggle-lock-all"),
                    "pause" => toggle_manual_pause(app),
                    "theme-nocturne" => show_and_emit(app, "theme:nocturne"),
                    "theme-ember" => show_and_emit(app, "theme:ember"),
                    "theme-glass" => show_and_emit(app, "theme:glass"),
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
                    TrayIconEvent::Click {
                        button: MouseButton::Left,
                        button_state: MouseButtonState::Up,
                        ..
                    } => toggle_poster_window(tray.app_handle()),
                    TrayIconEvent::DoubleClick {
                        button: MouseButton::Left,
                        ..
                    } => show_main_window(tray.app_handle()),
                    _ => {}
                })
                .build(app)?;

            if let Some(poster) = app.get_webview_window("poster") {
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
            desktop_host_diagnostics,
            update_runtime_state,
            start_poster_drag,
            resize_poster_surface,
            set_poster_interaction_region,
            reconnect_desktop_host
        ])
        .run(tauri::generate_context!())
        .expect("failed to run Desktop-Up");
}
