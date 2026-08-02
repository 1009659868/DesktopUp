use serde::Serialize;

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ForegroundCoverage {
    #[default]
    None,
    Maximized,
    Fullscreen,
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeVisibilitySnapshot {
    pub foreground_coverage: ForegroundCoverage,
    pub foreground_class: Option<String>,
    pub foreground_probe: u32,
    pub session_locked: bool,
    pub display_off: bool,
    pub automatically_paused: bool,
    pub pause_reasons: Vec<String>,
}

impl RuntimeVisibilitySnapshot {
    fn from_inputs(
        foreground_coverage: ForegroundCoverage,
        foreground_class: Option<String>,
        session_locked: bool,
        display_off: bool,
    ) -> Self {
        let pause_reasons = pause_reasons(foreground_coverage, session_locked, display_off);
        Self {
            foreground_coverage,
            foreground_class,
            foreground_probe: 0,
            session_locked,
            display_off,
            automatically_paused: !pause_reasons.is_empty(),
            pause_reasons,
        }
    }

    pub fn tray_label(&self, manually_paused: bool) -> String {
        if manually_paused {
            return "运行状态：手动暂停".to_string();
        }
        if self.display_off {
            return "运行状态：显示器关闭，自动暂停".to_string();
        }
        if self.session_locked {
            return "运行状态：系统锁定，自动暂停".to_string();
        }
        match self.foreground_coverage {
            ForegroundCoverage::Fullscreen => "运行状态：全屏覆盖，自动暂停".to_string(),
            ForegroundCoverage::Maximized => "运行状态：最大化覆盖，自动暂停".to_string(),
            ForegroundCoverage::None => "运行状态：静态待机".to_string(),
        }
    }
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeMonitorCapabilities {
    pub foreground_events: bool,
    pub session_events: bool,
    pub display_events: bool,
}

fn pause_reasons(
    foreground_coverage: ForegroundCoverage,
    session_locked: bool,
    display_off: bool,
) -> Vec<String> {
    let mut reasons = Vec::new();
    if display_off {
        reasons.push("displayOff".to_string());
    }
    if session_locked {
        reasons.push("sessionLocked".to_string());
    }
    match foreground_coverage {
        ForegroundCoverage::Fullscreen => reasons.push("fullscreenForeground".to_string()),
        ForegroundCoverage::Maximized => reasons.push("maximizedForeground".to_string()),
        ForegroundCoverage::None => {}
    }
    reasons
}

#[cfg(target_os = "windows")]
mod platform {
    use super::{ForegroundCoverage, RuntimeMonitorCapabilities, RuntimeVisibilitySnapshot};
    use std::{
        sync::{
            atomic::{AtomicBool, AtomicIsize, Ordering},
            mpsc::{sync_channel, Receiver, SyncSender},
            OnceLock,
        },
        thread,
        time::Duration,
    };
    use windows::{
        core::w,
        Win32::{
            Foundation::{HANDLE, HWND, LPARAM, LRESULT, RECT, WPARAM},
            Graphics::Gdi::{
                GetMonitorInfoW, MonitorFromWindow, HMONITOR, MONITORINFO, MONITOR_DEFAULTTONEAREST,
            },
            System::{
                Power::{
                    RegisterPowerSettingNotification, UnregisterPowerSettingNotification,
                    HPOWERNOTIFY, POWERBROADCAST_SETTING,
                },
                RemoteDesktop::{
                    WTSRegisterSessionNotification, WTSUnRegisterSessionNotification,
                    NOTIFY_FOR_THIS_SESSION,
                },
                SystemServices::GUID_CONSOLE_DISPLAY_STATE,
            },
            UI::{
                Accessibility::{SetWinEventHook, UnhookWinEvent, HWINEVENTHOOK},
                Shell::{DefSubclassProc, RemoveWindowSubclass, SetWindowSubclass},
                WindowsAndMessaging::{
                    DispatchMessageW, GetClassNameW, GetForegroundWindow, GetMessageW,
                    GetWindowRect, GetWindowThreadProcessId, IsWindowVisible, IsZoomed,
                    RemovePropW, SetPropW, TranslateMessage, DEVICE_NOTIFY_WINDOW_HANDLE,
                    EVENT_OBJECT_LOCATIONCHANGE, EVENT_SYSTEM_FOREGROUND, MSG, OBJID_WINDOW,
                    PBT_POWERSETTINGCHANGE, WINEVENT_OUTOFCONTEXT, WINEVENT_SKIPOWNPROCESS,
                    WM_DESTROY, WM_POWERBROADCAST, WM_WTSSESSION_CHANGE, WTS_SESSION_LOCK,
                    WTS_SESSION_UNLOCK,
                },
            },
        },
    };

    const RUNTIME_WINDOW_SUBCLASS_ID: usize = 0x4455_5032;
    const EVENT_SETTLE_MS: u64 = 80;
    const STARTUP_RECHECK_MS: u64 = 1_500;
    const FOREGROUND_PROBE_PRESERVED: u32 = 1 << 11;
    const RUNTIME_PAUSE_PROPERTY: windows::core::PCWSTR = w!("DesktopUp.RuntimePauseFlags");
    const RUNTIME_FOREGROUND_PROPERTY: windows::core::PCWSTR = w!("DesktopUp.ForegroundProbeFlags");
    const RUNTIME_CAPABILITIES_PROPERTY: windows::core::PCWSTR =
        w!("DesktopUp.RuntimeMonitorCapabilities");

    static RUNTIME_EVENT_SENDER: OnceLock<SyncSender<RuntimeSignal>> = OnceLock::new();
    static RUNTIME_POSTER_HWND: AtomicIsize = AtomicIsize::new(0);
    static POWER_NOTIFICATION: AtomicIsize = AtomicIsize::new(0);
    static SESSION_NOTIFICATION_REGISTERED: AtomicBool = AtomicBool::new(false);

    #[derive(Clone, Copy)]
    enum RuntimeSignal {
        ForegroundChanged(Option<isize>),
        SessionLocked(bool),
        DisplayOff(bool),
    }

    #[derive(Default)]
    struct RuntimeInputs {
        session_locked: bool,
        display_off: bool,
        foreground_hint: Option<isize>,
    }

    struct ForegroundMeasurement {
        coverage: ForegroundCoverage,
        class_name: Option<String>,
        probe: u32,
        preserve_previous: bool,
    }

    fn poster_window() -> Option<HWND> {
        let raw = RUNTIME_POSTER_HWND.load(Ordering::Acquire);
        (raw != 0).then(|| HWND(raw as *mut core::ffi::c_void))
    }

    fn send_signal(signal: RuntimeSignal) {
        if let Some(sender) = RUNTIME_EVENT_SENDER.get() {
            let _ = sender.try_send(signal);
        }
    }

    pub fn reevaluate_foreground() {
        send_signal(RuntimeSignal::ForegroundChanged(None));
    }

    pub fn publish_diagnostics(snapshot: &RuntimeVisibilitySnapshot, manually_paused: bool) {
        let Some(hwnd) = poster_window() else {
            return;
        };
        let mut flags = 0usize;
        if snapshot.automatically_paused {
            flags |= 1;
        }
        match snapshot.foreground_coverage {
            ForegroundCoverage::Fullscreen => flags |= 2,
            ForegroundCoverage::Maximized => flags |= 4,
            ForegroundCoverage::None => {}
        }
        if snapshot.session_locked {
            flags |= 8;
        }
        if snapshot.display_off {
            flags |= 16;
        }
        if manually_paused {
            flags |= 32;
        }
        // Window properties use NULL to mean “missing”, so store flags + 1.
        let encoded = HANDLE((flags + 1) as *mut core::ffi::c_void);
        unsafe {
            let _ = SetPropW(hwnd, RUNTIME_PAUSE_PROPERTY, Some(encoded));
            let foreground_probe =
                HANDLE((snapshot.foreground_probe as usize + 1) as *mut core::ffi::c_void);
            let _ = SetPropW(hwnd, RUNTIME_FOREGROUND_PROPERTY, Some(foreground_probe));
        }
    }

    pub fn publish_capabilities(capabilities: &RuntimeMonitorCapabilities) {
        let Some(hwnd) = poster_window() else {
            return;
        };
        let mut flags = 0usize;
        if capabilities.foreground_events {
            flags |= 1;
        }
        if capabilities.session_events {
            flags |= 2;
        }
        if capabilities.display_events {
            flags |= 4;
        }
        let encoded = HANDLE((flags + 1) as *mut core::ffi::c_void);
        unsafe {
            let _ = SetPropW(hwnd, RUNTIME_CAPABILITIES_PROPERTY, Some(encoded));
        }
    }

    fn apply_signal(inputs: &mut RuntimeInputs, signal: RuntimeSignal) {
        match signal {
            RuntimeSignal::ForegroundChanged(hwnd) => inputs.foreground_hint = hwnd,
            RuntimeSignal::SessionLocked(locked) => inputs.session_locked = locked,
            RuntimeSignal::DisplayOff(off) => inputs.display_off = off,
        }
    }

    fn class_name(hwnd: HWND) -> Option<String> {
        let mut buffer = [0u16; 128];
        let length = unsafe { GetClassNameW(hwnd, &mut buffer) };
        (length > 0).then(|| String::from_utf16_lossy(&buffer[..length as usize]))
    }

    fn same_monitor(left: HMONITOR, right: HMONITOR) -> bool {
        left.0 == right.0
    }

    fn covers(rect: &RECT, target: &RECT, tolerance: i32) -> bool {
        rect.left <= target.left + tolerance
            && rect.top <= target.top + tolerance
            && rect.right >= target.right - tolerance
            && rect.bottom >= target.bottom - tolerance
    }

    fn is_desktop_surface(class_name: Option<&str>) -> bool {
        matches!(class_name, Some("Progman" | "WorkerW"))
    }

    fn is_transient_shell_surface(class_name: Option<&str>) -> bool {
        matches!(
            class_name,
            Some(
                "Shell_TrayWnd"
                    | "Shell_SecondaryTrayWnd"
                    | "NotifyIconOverflowWindow"
                    | "TopLevelWindowForOverflowXamlIsland"
                    | "Windows.UI.Composition.DesktopWindowContentBridge"
                    | "#32768"
            )
        )
    }

    fn resolve_foreground_measurement(
        measurement: ForegroundMeasurement,
        previous: Option<&RuntimeVisibilitySnapshot>,
    ) -> (ForegroundCoverage, Option<String>, u32) {
        if measurement.preserve_previous {
            let probe = measurement.probe | FOREGROUND_PROBE_PRESERVED;
            if let Some(previous) = previous {
                return (
                    previous.foreground_coverage,
                    previous.foreground_class.clone(),
                    probe,
                );
            }
            return (ForegroundCoverage::None, measurement.class_name, probe);
        }
        (
            measurement.coverage,
            measurement.class_name,
            measurement.probe,
        )
    }

    fn foreground_coverage(foreground_hint: Option<isize>) -> ForegroundMeasurement {
        let mut probe = 0u32;
        let Some(poster) = poster_window() else {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: None,
                probe,
                preserve_previous: false,
            };
        };
        probe |= 1;
        let foreground = foreground_hint
            .map(|raw| HWND(raw as *mut core::ffi::c_void))
            .unwrap_or_else(|| unsafe { GetForegroundWindow() });
        if foreground.0.is_null() || foreground == poster {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: None,
                probe,
                preserve_previous: true,
            };
        }
        probe |= 2;
        if !unsafe { IsWindowVisible(foreground).as_bool() } {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: class_name(foreground),
                probe,
                preserve_previous: true,
            };
        }
        probe |= 4;

        let mut poster_process = 0u32;
        let mut foreground_process = 0u32;
        unsafe {
            GetWindowThreadProcessId(poster, Some(&mut poster_process));
            GetWindowThreadProcessId(foreground, Some(&mut foreground_process));
        }
        if poster_process != 0 && poster_process == foreground_process {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: class_name(foreground),
                probe,
                preserve_previous: true,
            };
        }
        probe |= 8;

        let foreground_class = class_name(foreground);
        if is_desktop_surface(foreground_class.as_deref()) {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: foreground_class,
                probe,
                preserve_previous: false,
            };
        }
        if is_transient_shell_surface(foreground_class.as_deref()) {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: foreground_class,
                probe,
                preserve_previous: true,
            };
        }
        probe |= 16;

        let foreground_monitor = unsafe { MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST) };
        let poster_monitor = unsafe { MonitorFromWindow(poster, MONITOR_DEFAULTTONEAREST) };
        if !same_monitor(foreground_monitor, poster_monitor) {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: foreground_class,
                probe,
                preserve_previous: false,
            };
        }
        probe |= 32;

        let mut rect = RECT::default();
        if unsafe { GetWindowRect(foreground, &mut rect) }.is_err() {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: foreground_class,
                probe,
                preserve_previous: true,
            };
        }
        probe |= 64;
        let mut monitor = MONITORINFO {
            cbSize: std::mem::size_of::<MONITORINFO>() as u32,
            ..MONITORINFO::default()
        };
        if !unsafe { GetMonitorInfoW(foreground_monitor, &mut monitor).as_bool() } {
            return ForegroundMeasurement {
                coverage: ForegroundCoverage::None,
                class_name: foreground_class,
                probe,
                preserve_previous: true,
            };
        }
        probe |= 128;

        let coverage = if covers(&rect, &monitor.rcMonitor, 2) {
            probe |= 256;
            ForegroundCoverage::Fullscreen
        } else if unsafe { IsZoomed(foreground).as_bool() } {
            probe |= 512;
            ForegroundCoverage::Maximized
        } else if covers(&rect, &monitor.rcWork, 2) {
            probe |= 1024;
            ForegroundCoverage::Maximized
        } else {
            ForegroundCoverage::None
        };
        ForegroundMeasurement {
            coverage,
            class_name: foreground_class,
            probe,
            preserve_previous: false,
        }
    }

    fn run_runtime_worker<F>(receiver: Receiver<RuntimeSignal>, on_change: F)
    where
        F: Fn(RuntimeVisibilitySnapshot) + Send + 'static,
    {
        let mut inputs = RuntimeInputs::default();
        let mut previous = None;
        while let Ok(signal) = receiver.recv() {
            apply_signal(&mut inputs, signal);
            thread::sleep(Duration::from_millis(EVENT_SETTLE_MS));
            while let Ok(pending) = receiver.try_recv() {
                apply_signal(&mut inputs, pending);
            }

            let (coverage, foreground_class, foreground_probe) = resolve_foreground_measurement(
                foreground_coverage(inputs.foreground_hint),
                previous.as_ref(),
            );
            let mut snapshot = RuntimeVisibilitySnapshot::from_inputs(
                coverage,
                foreground_class,
                inputs.session_locked,
                inputs.display_off,
            );
            snapshot.foreground_probe = foreground_probe;
            if previous.as_ref() != Some(&snapshot) {
                previous = Some(snapshot.clone());
                on_change(snapshot);
            }
        }
    }

    unsafe extern "system" fn runtime_win_event(
        _hook: HWINEVENTHOOK,
        event: u32,
        hwnd: HWND,
        object_id: i32,
        _child_id: i32,
        _event_thread: u32,
        _event_time: u32,
    ) {
        let is_foreground = event == EVENT_SYSTEM_FOREGROUND;
        let is_foreground_geometry = event == EVENT_OBJECT_LOCATIONCHANGE
            && object_id == OBJID_WINDOW.0
            && GetForegroundWindow() == hwnd;
        if is_foreground || is_foreground_geometry {
            send_signal(RuntimeSignal::ForegroundChanged(Some(hwnd.0 as isize)));
        }
    }

    fn run_runtime_event_hook(started: SyncSender<bool>) {
        unsafe {
            let flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
            let foreground_hook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                None,
                Some(runtime_win_event),
                0,
                0,
                flags,
            );
            let geometry_hook = SetWinEventHook(
                EVENT_OBJECT_LOCATIONCHANGE,
                EVENT_OBJECT_LOCATIONCHANGE,
                None,
                Some(runtime_win_event),
                0,
                0,
                flags,
            );
            let any_hook = !foreground_hook.0.is_null() || !geometry_hook.0.is_null();
            let _ = started.send(any_hook);
            if !any_hook {
                return;
            }

            let mut message = MSG::default();
            while GetMessageW(&mut message, None, 0, 0).0 > 0 {
                let _ = TranslateMessage(&message);
                DispatchMessageW(&message);
            }
            if !foreground_hook.0.is_null() {
                let _ = UnhookWinEvent(foreground_hook);
            }
            if !geometry_hook.0.is_null() {
                let _ = UnhookWinEvent(geometry_hook);
            }
        }
    }

    unsafe fn read_display_off(lparam: LPARAM) -> Option<bool> {
        if lparam.0 == 0 {
            return None;
        }
        let setting = &*(lparam.0 as *const POWERBROADCAST_SETTING);
        if setting.PowerSetting != GUID_CONSOLE_DISPLAY_STATE || setting.DataLength < 4 {
            return None;
        }
        let state = std::ptr::read_unaligned(setting.Data.as_ptr().cast::<u32>());
        Some(state == 0)
    }

    unsafe extern "system" fn runtime_window_subclass(
        hwnd: HWND,
        message: u32,
        wparam: WPARAM,
        lparam: LPARAM,
        subclass_id: usize,
        _reference_data: usize,
    ) -> LRESULT {
        match message {
            WM_WTSSESSION_CHANGE if wparam.0 as u32 == WTS_SESSION_LOCK => {
                send_signal(RuntimeSignal::SessionLocked(true));
            }
            WM_WTSSESSION_CHANGE if wparam.0 as u32 == WTS_SESSION_UNLOCK => {
                send_signal(RuntimeSignal::SessionLocked(false));
                send_signal(RuntimeSignal::ForegroundChanged(None));
            }
            WM_POWERBROADCAST if wparam.0 as u32 == PBT_POWERSETTINGCHANGE => {
                if let Some(display_off) = read_display_off(lparam) {
                    send_signal(RuntimeSignal::DisplayOff(display_off));
                    if !display_off {
                        send_signal(RuntimeSignal::ForegroundChanged(None));
                    }
                }
            }
            WM_DESTROY => {
                let _ = RemovePropW(hwnd, RUNTIME_PAUSE_PROPERTY);
                let _ = RemovePropW(hwnd, RUNTIME_FOREGROUND_PROPERTY);
                let _ = RemovePropW(hwnd, RUNTIME_CAPABILITIES_PROPERTY);
                if SESSION_NOTIFICATION_REGISTERED.swap(false, Ordering::AcqRel) {
                    let _ = WTSUnRegisterSessionNotification(hwnd);
                }
                let power_handle = POWER_NOTIFICATION.swap(0, Ordering::AcqRel);
                if power_handle != 0 {
                    let _ = UnregisterPowerSettingNotification(HPOWERNOTIFY(power_handle));
                }
                let _ = RemoveWindowSubclass(hwnd, Some(runtime_window_subclass), subclass_id);
                RUNTIME_POSTER_HWND.store(0, Ordering::Release);
            }
            _ => {}
        }
        DefSubclassProc(hwnd, message, wparam, lparam)
    }

    pub fn install<F>(
        window: &tauri::WebviewWindow,
        on_change: F,
    ) -> Result<RuntimeMonitorCapabilities, String>
    where
        F: Fn(RuntimeVisibilitySnapshot) + Send + 'static,
    {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        RUNTIME_POSTER_HWND.store(hwnd.0 as isize, Ordering::Release);
        if RUNTIME_EVENT_SENDER.get().is_some() {
            return Err("runtime visibility monitor was already initialized".to_string());
        }

        let (sender, receiver) = sync_channel(8);
        RUNTIME_EVENT_SENDER
            .set(sender)
            .map_err(|_| "runtime visibility monitor was already initialized".to_string())?;
        thread::Builder::new()
            .name("desktop-up-runtime-policy".to_string())
            .spawn(move || run_runtime_worker(receiver, on_change))
            .map_err(|error| error.to_string())?;

        let (started_sender, started_receiver) = sync_channel(1);
        thread::Builder::new()
            .name("desktop-up-runtime-events".to_string())
            .spawn(move || run_runtime_event_hook(started_sender))
            .map_err(|error| error.to_string())?;
        let foreground_events = started_receiver
            .recv_timeout(Duration::from_secs(1))
            .unwrap_or(false);

        let subclass_installed = unsafe {
            SetWindowSubclass(
                hwnd,
                Some(runtime_window_subclass),
                RUNTIME_WINDOW_SUBCLASS_ID,
                0,
            )
        };
        if !subclass_installed.as_bool() {
            return Err("failed to install runtime visibility window subclass".to_string());
        }

        let session_events =
            unsafe { WTSRegisterSessionNotification(hwnd, NOTIFY_FOR_THIS_SESSION).is_ok() };
        SESSION_NOTIFICATION_REGISTERED.store(session_events, Ordering::Release);

        let display_events = unsafe {
            RegisterPowerSettingNotification(
                HANDLE(hwnd.0),
                &GUID_CONSOLE_DISPLAY_STATE,
                DEVICE_NOTIFY_WINDOW_HANDLE,
            )
        }
        .map(|handle| {
            POWER_NOTIFICATION.store(handle.0, Ordering::Release);
            true
        })
        .unwrap_or(false);

        send_signal(RuntimeSignal::ForegroundChanged(None));
        thread::Builder::new()
            .name("desktop-up-runtime-startup-recheck".to_string())
            .spawn(|| {
                thread::sleep(Duration::from_millis(STARTUP_RECHECK_MS));
                send_signal(RuntimeSignal::ForegroundChanged(None));
            })
            .map_err(|error| error.to_string())?;
        Ok(RuntimeMonitorCapabilities {
            foreground_events,
            session_events,
            display_events,
        })
    }

    #[cfg(test)]
    mod tests {
        use super::{
            covers, is_desktop_surface, is_transient_shell_surface, resolve_foreground_measurement,
            ForegroundMeasurement, RECT,
        };
        use crate::runtime_monitor::{ForegroundCoverage, RuntimeVisibilitySnapshot};

        #[test]
        fn monitor_coverage_allows_small_native_border_tolerance() {
            let monitor = RECT {
                left: 0,
                top: 0,
                right: 2560,
                bottom: 1440,
            };
            let borderless_game = RECT {
                left: 0,
                top: 0,
                right: 2562,
                bottom: 1440,
            };
            assert!(covers(&borderless_game, &monitor, 2));
        }

        #[test]
        fn small_foreground_window_does_not_cover_monitor() {
            let monitor = RECT {
                left: 0,
                top: 0,
                right: 2560,
                bottom: 1440,
            };
            let small_window = RECT {
                left: 240,
                top: 180,
                right: 1240,
                bottom: 900,
            };
            assert!(!covers(&small_window, &monitor, 2));
        }

        #[test]
        fn tray_and_shell_overlays_preserve_the_covered_window_pause() {
            assert!(is_transient_shell_surface(Some("Shell_TrayWnd")));
            assert!(is_transient_shell_surface(Some("NotifyIconOverflowWindow")));
            assert!(!is_desktop_surface(Some("Shell_TrayWnd")));

            let previous = RuntimeVisibilitySnapshot::from_inputs(
                ForegroundCoverage::Maximized,
                Some("Chrome_WidgetWin_1".to_string()),
                false,
                false,
            );
            let (coverage, class_name, probe) = resolve_foreground_measurement(
                ForegroundMeasurement {
                    coverage: ForegroundCoverage::None,
                    class_name: Some("Shell_TrayWnd".to_string()),
                    probe: 8,
                    preserve_previous: true,
                },
                Some(&previous),
            );

            assert_eq!(coverage, ForegroundCoverage::Maximized);
            assert_eq!(class_name.as_deref(), Some("Chrome_WidgetWin_1"));
            assert_ne!(probe & super::FOREGROUND_PROBE_PRESERVED, 0);
        }

        #[test]
        fn actual_desktop_foreground_clears_previous_coverage() {
            assert!(is_desktop_surface(Some("Progman")));
            assert!(is_desktop_surface(Some("WorkerW")));

            let previous = RuntimeVisibilitySnapshot::from_inputs(
                ForegroundCoverage::Fullscreen,
                Some("GameWindow".to_string()),
                false,
                false,
            );
            let (coverage, class_name, _) = resolve_foreground_measurement(
                ForegroundMeasurement {
                    coverage: ForegroundCoverage::None,
                    class_name: Some("Progman".to_string()),
                    probe: 8,
                    preserve_previous: false,
                },
                Some(&previous),
            );

            assert_eq!(coverage, ForegroundCoverage::None);
            assert_eq!(class_name.as_deref(), Some("Progman"));
        }
    }
}

#[cfg(target_os = "windows")]
pub use platform::{install, publish_capabilities, publish_diagnostics, reevaluate_foreground};

#[cfg(not(target_os = "windows"))]
pub fn install<F>(
    _window: &tauri::WebviewWindow,
    on_change: F,
) -> Result<RuntimeMonitorCapabilities, String>
where
    F: Fn(RuntimeVisibilitySnapshot) + Send + 'static,
{
    on_change(RuntimeVisibilitySnapshot::default());
    Ok(RuntimeMonitorCapabilities::default())
}

#[cfg(not(target_os = "windows"))]
pub fn publish_diagnostics(_snapshot: &RuntimeVisibilitySnapshot, _manually_paused: bool) {}

#[cfg(not(target_os = "windows"))]
pub fn publish_capabilities(_capabilities: &RuntimeMonitorCapabilities) {}

#[cfg(not(target_os = "windows"))]
pub fn reevaluate_foreground() {}

#[cfg(test)]
mod tests {
    use super::{ForegroundCoverage, RuntimeVisibilitySnapshot};

    #[test]
    fn fullscreen_maximized_lock_and_display_states_pause_deterministically() {
        let fullscreen = RuntimeVisibilitySnapshot::from_inputs(
            ForegroundCoverage::Fullscreen,
            Some("GameWindow".to_string()),
            false,
            false,
        );
        assert!(fullscreen.automatically_paused);
        assert_eq!(fullscreen.pause_reasons, ["fullscreenForeground"]);

        let maximized = RuntimeVisibilitySnapshot::from_inputs(
            ForegroundCoverage::Maximized,
            Some("ApplicationFrameWindow".to_string()),
            false,
            false,
        );
        assert_eq!(maximized.pause_reasons, ["maximizedForeground"]);

        let locked_and_off =
            RuntimeVisibilitySnapshot::from_inputs(ForegroundCoverage::None, None, true, true);
        assert_eq!(
            locked_and_off.pause_reasons,
            ["displayOff", "sessionLocked"]
        );
    }

    #[test]
    fn small_window_and_desktop_foreground_stay_active() {
        let active = RuntimeVisibilitySnapshot::from_inputs(
            ForegroundCoverage::None,
            Some("Notepad".to_string()),
            false,
            false,
        );
        assert!(!active.automatically_paused);
        assert!(active.pause_reasons.is_empty());
        assert_eq!(active.tray_label(false), "运行状态：静态待机");
    }
}
