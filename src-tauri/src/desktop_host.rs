use crate::host_contract::InteractionRegion;
use serde::Serialize;

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DesktopHostDiagnostics {
    pub mode: String,
    pub status: String,
    pub parent_class: Option<String>,
    pub workerw_detected: bool,
    pub fallback_reason: Option<String>,
    pub interaction_region_count: usize,
    pub interaction_region: Option<String>,
    pub surface_registry_version: u32,
    pub surface_registry_revision: u64,
    pub registered_surface_count: usize,
    pub recovery_count: u64,
    pub recovery_generation: usize,
    pub recovery_event_count: usize,
    pub last_recovery_trigger: Option<String>,
    pub last_recovery_action: Option<String>,
    pub last_recovery_error: Option<String>,
}

impl Default for DesktopHostDiagnostics {
    fn default() -> Self {
        Self {
            mode: "pending".to_string(),
            status: "等待桌面宿主初始化".to_string(),
            parent_class: None,
            workerw_detected: false,
            fallback_reason: None,
            interaction_region_count: 0,
            interaction_region: None,
            surface_registry_version: 0,
            surface_registry_revision: 0,
            registered_surface_count: 0,
            recovery_count: 0,
            recovery_generation: 0,
            recovery_event_count: 0,
            last_recovery_trigger: None,
            last_recovery_action: None,
            last_recovery_error: None,
        }
    }
}

impl DesktopHostDiagnostics {
    pub fn tray_label(&self) -> String {
        match self.mode.as_str() {
            "workerW" => "桌面挂载：WorkerW".to_string(),
            "toolWindowFallback" => "桌面挂载：ToolWindow 回退".to_string(),
            _ => "桌面挂载：正在初始化".to_string(),
        }
    }
}
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum HostRecoveryTrigger {
    ShellForeground,
    ShellLifecycle,
    DisplayChange,
    DpiChange,
    WorkAreaChange,
    TauriScaleFactorChanged,
    ManualReconnect,
}

impl HostRecoveryTrigger {
    fn bit(self) -> usize {
        1usize << self as usize
    }

    fn label(self) -> &'static str {
        match self {
            Self::ShellForeground => "shellForeground",
            Self::ShellLifecycle => "shellLifecycle",
            Self::DisplayChange => "displayChange",
            Self::DpiChange => "dpiChange",
            Self::WorkAreaChange => "workAreaChange",
            Self::TauriScaleFactorChanged => "tauriScaleFactorChanged",
            Self::ManualReconnect => "manualReconnect",
        }
    }
}

const HOST_RECOVERY_TRIGGERS: [HostRecoveryTrigger; 7] = [
    HostRecoveryTrigger::ShellForeground,
    HostRecoveryTrigger::ShellLifecycle,
    HostRecoveryTrigger::DisplayChange,
    HostRecoveryTrigger::DpiChange,
    HostRecoveryTrigger::WorkAreaChange,
    HostRecoveryTrigger::TauriScaleFactorChanged,
    HostRecoveryTrigger::ManualReconnect,
];

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct HostRecoveryRequest {
    pub generation: usize,
    pub event_count: usize,
    reason_mask: usize,
}

impl HostRecoveryRequest {
    pub fn single(trigger: HostRecoveryTrigger) -> Self {
        Self::from_mask(0, 1, trigger.bit())
    }

    fn shell_mask() -> usize {
        HostRecoveryTrigger::ShellForeground.bit() | HostRecoveryTrigger::ShellLifecycle.bit()
    }

    pub fn includes_shell(&self) -> bool {
        self.reason_mask & Self::shell_mask() != 0
    }

    pub fn is_shell_only(&self) -> bool {
        self.reason_mask != 0 && self.reason_mask & !Self::shell_mask() == 0
    }

    fn from_mask(generation: usize, event_count: usize, reason_mask: usize) -> Self {
        Self {
            generation,
            event_count: event_count.max(1),
            reason_mask,
        }
    }

    pub fn trigger_summary(&self) -> String {
        let triggers: Vec<&str> = HOST_RECOVERY_TRIGGERS
            .iter()
            .copied()
            .filter(|trigger| self.reason_mask & trigger.bit() != 0)
            .map(HostRecoveryTrigger::label)
            .collect();
        if triggers.is_empty() {
            "unknown".to_string()
        } else {
            triggers.join(",")
        }
    }
}

fn merge_host_recovery_request(
    pending: &mut Option<HostRecoveryRequest>,
    generation: usize,
    trigger: HostRecoveryTrigger,
) {
    if let Some(request) = pending.as_mut() {
        request.generation = generation;
        request.event_count = request.event_count.saturating_add(1);
        request.reason_mask |= trigger.bit();
    } else {
        *pending = Some(HostRecoveryRequest::from_mask(generation, 1, trigger.bit()));
    }
}

#[cfg(test)]
mod recovery_tests {
    use super::{merge_host_recovery_request, HostRecoveryRequest, HostRecoveryTrigger};

    #[test]
    fn recovery_requests_merge_reasons_and_count_burst_events() {
        let mut pending = None;
        merge_host_recovery_request(&mut pending, 6, HostRecoveryTrigger::ShellForeground);
        merge_host_recovery_request(&mut pending, 9, HostRecoveryTrigger::DpiChange);
        let request = pending.expect("merged recovery request");
        assert_eq!(request.generation, 9);
        assert_eq!(request.event_count, 2);
        assert_eq!(request.trigger_summary(), "shellForeground,dpiChange");
        assert!(request.includes_shell());
        assert!(!request.is_shell_only());
        let mut shell_pending = None;
        merge_host_recovery_request(&mut shell_pending, 10, HostRecoveryTrigger::ShellLifecycle);
        assert!(shell_pending
            .expect("shell recovery request")
            .is_shell_only());

        let manual = HostRecoveryRequest::single(HostRecoveryTrigger::ManualReconnect);
        assert_eq!(manual.generation, 0);
        assert_eq!(manual.event_count, 1);
        assert_eq!(manual.trigger_summary(), "manualReconnect");
        assert!(!manual.includes_shell());
    }
}

#[cfg(target_os = "windows")]
mod platform {
    use super::{
        merge_host_recovery_request, DesktopHostDiagnostics, HostRecoveryRequest,
        HostRecoveryTrigger, InteractionRegion,
    };
    use crate::placement::PosterPlacement;
    use std::{
        env,
        sync::{
            atomic::{AtomicBool, AtomicIsize, AtomicUsize, Ordering},
            mpsc::{sync_channel, Receiver, SyncSender},
            Arc, Mutex, OnceLock, RwLock,
        },
        thread,
        time::Duration,
    };
    use windows::{
        core::{w, BOOL, PCWSTR},
        Win32::{
            Foundation::{HWND, LPARAM, LRESULT, POINT, RECT, WPARAM},
            Graphics::Dwm::{
                DwmSetWindowAttribute, DWMNCRP_DISABLED, DWMWA_BORDER_COLOR,
                DWMWA_NCRENDERING_POLICY, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_DONOTROUND,
            },
            Graphics::Gdi::{
                CombineRgn, CreateRectRgn, CreateRoundRectRgn, DeleteObject, EnumDisplayMonitors,
                GetMonitorInfoW, MonitorFromPoint, MonitorFromWindow, ScreenToClient, SetWindowRgn,
                HDC, HGDIOBJ, HMONITOR, HRGN, MONITORINFO, MONITORINFOEXW,
                MONITOR_DEFAULTTONEAREST, RGN_ERROR, RGN_OR,
            },
            System::Threading::GetCurrentProcessId,
            UI::{
                Accessibility::{SetWinEventHook, UnhookWinEvent, HWINEVENTHOOK},
                Shell::{DefSubclassProc, RemoveWindowSubclass, SetWindowSubclass},
                WindowsAndMessaging::{
                    CallNextHookEx, DispatchMessageW, EnumWindows, FindWindowExW, GetClassNameW,
                    GetMessageW, GetParent, GetWindow, GetWindowLongPtrW, GetWindowRect,
                    GetWindowThreadProcessId, IsWindowVisible, SendMessageW, SetParent,
                    SetWindowLongPtrW, SetWindowPos, SetWindowsHookExW, ShowWindow,
                    TranslateMessage, UnhookWindowsHookEx, EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW,
                    EVENT_SYSTEM_FOREGROUND, GWL_EXSTYLE, GWL_STYLE, GW_HWNDPREV, HWND_BOTTOM,
                    HWND_TOP, MA_NOACTIVATE, MSG, MSLLHOOKSTRUCT, OBJID_WINDOW, STYLESTRUCT,
                    SWP_ASYNCWINDOWPOS, SWP_FRAMECHANGED, SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE,
                    SWP_NOZORDER, SW_SHOWNOACTIVATE, WH_MOUSE_LL, WINEVENT_OUTOFCONTEXT, WM_APP,
                    WM_DESTROY, WM_DISPLAYCHANGE, WM_DPICHANGED, WM_EXITSIZEMOVE, WM_LBUTTONDOWN,
                    WM_LBUTTONUP, WM_MBUTTONDOWN, WM_MBUTTONUP, WM_MOUSEACTIVATE, WM_MOUSEHWHEEL,
                    WM_MOUSEMOVE, WM_MOUSEWHEEL, WM_MOVING, WM_RBUTTONDOWN, WM_RBUTTONUP,
                    WM_SETTINGCHANGE, WM_STYLECHANGING, WM_XBUTTONDOWN, WM_XBUTTONUP, WS_CAPTION,
                    WS_CHILD, WS_EX_APPWINDOW, WS_EX_LAYERED, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
                    WS_EX_TRANSPARENT, WS_MAXIMIZEBOX, WS_MINIMIZEBOX, WS_POPUP, WS_SYSMENU,
                    WS_THICKFRAME,
                },
            },
        },
    };

    const SHELL_RECOVERY_SETTLE_MS: u64 = 140;
    const SHELL_DESKTOP_LAYER_REPAIR_DELAYS_MS: [u64; 14] =
        [4, 8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48, 64, 96];
    const POST_SHOW_DESKTOP_LAYER_SETTLE_DELAYS_MS: [u64; 3] = [16, 48, 96];
    const POSTER_WINDOW_SUBCLASS_ID: usize = 0x4455_5031;
    const WM_POSTER_INPUT_PASSTHROUGH: u32 = WM_APP + 0x451;
    const NON_CLIENT_DECORATION_STYLES: u32 =
        WS_CAPTION.0 | WS_THICKFRAME.0 | WS_SYSMENU.0 | WS_MINIMIZEBOX.0 | WS_MAXIMIZEBOX.0;

    fn strip_non_client_decoration_styles(style: u32) -> u32 {
        style & !NON_CLIENT_DECORATION_STYLES
    }

    static POSTER_HWND: AtomicIsize = AtomicIsize::new(0);
    // The poster is deliberately hidden until the WebView has completed the canonical
    // HostSurface handshake. Showing the raw Tauri window during cold start can expose a
    // transient Windows 11/DWM non-client strip before the first size/region invalidation.
    static POSTER_VISIBLE_INTENT: AtomicBool = AtomicBool::new(false);
    static TOOL_WINDOW_FALLBACK_ACTIVE: AtomicBool = AtomicBool::new(false);
    static HOST_RECOVERY_ACTIVE: AtomicBool = AtomicBool::new(false);
    static HOST_RECOVERY_GENERATION: AtomicUsize = AtomicUsize::new(0);
    static HOST_RECOVERY_PENDING: OnceLock<Mutex<Option<HostRecoveryRequest>>> = OnceLock::new();
    static HOST_RECOVERY_SENDER: OnceLock<SyncSender<()>> = OnceLock::new();
    type HostRecoveryHandler = Arc<dyn Fn(HostRecoveryRequest) + Send + Sync>;
    static INPUT_HIT_REGIONS: OnceLock<RwLock<Vec<PhysicalHitRegion>>> = OnceLock::new();
    static INPUT_PASSTHROUGH_ACTIVE: AtomicBool = AtomicBool::new(false);
    static INPUT_MOUSE_BUTTON_ACTIVE: AtomicBool = AtomicBool::new(false);
    static INPUT_MOUSE_HOOK_STARTED: OnceLock<()> = OnceLock::new();

    #[derive(Clone, Copy)]
    struct ShellWindow {
        hwnd: HWND,
        is_workerw: bool,
        is_progman: bool,
        has_shell_view: bool,
        visible: bool,
    }

    #[derive(Clone)]
    struct MonitorDescriptor {
        handle: HMONITOR,
        device: String,
        work_area: RECT,
    }

    #[derive(Clone, Copy, Debug, PartialEq)]
    struct PhysicalHitRegion {
        left: i32,
        top: i32,
        right: i32,
        bottom: i32,
        radius: i32,
    }

    impl PhysicalHitRegion {
        fn contains(self, x: i32, y: i32) -> bool {
            if x < self.left || x > self.right || y < self.top || y > self.bottom {
                return false;
            }
            let radius = self
                .radius
                .max(0)
                .min((self.right - self.left) / 2)
                .min((self.bottom - self.top) / 2);
            if radius == 0
                || (x >= self.left + radius && x <= self.right - radius)
                || (y >= self.top + radius && y <= self.bottom - radius)
            {
                return true;
            }

            let center_x = if x < self.left + radius {
                self.left + radius
            } else {
                self.right - radius
            };
            let center_y = if y < self.top + radius {
                self.top + radius
            } else {
                self.bottom - radius
            };
            let dx = i64::from(x - center_x);
            let dy = i64::from(y - center_y);
            dx * dx + dy * dy <= i64::from(radius) * i64::from(radius)
        }
    }

    unsafe extern "system" fn collect_shell_window(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let windows = &mut *(lparam.0 as *mut Vec<ShellWindow>);
        let mut class_name = [0u16; 64];
        let class_length = GetClassNameW(hwnd, &mut class_name);
        let class = String::from_utf16_lossy(&class_name[..class_length.max(0) as usize]);
        let has_shell_view =
            FindWindowExW(Some(hwnd), None, w!("SHELLDLL_DefView"), PCWSTR::null()).is_ok();

        if class == "WorkerW" || class == "Progman" || has_shell_view {
            windows.push(ShellWindow {
                hwnd,
                is_workerw: class == "WorkerW",
                is_progman: class == "Progman",
                has_shell_view,
                visible: IsWindowVisible(hwnd).as_bool(),
            });
        }

        BOOL(1)
    }

    fn shell_windows() -> Vec<ShellWindow> {
        let mut windows = Vec::new();
        unsafe {
            let _ = EnumWindows(
                Some(collect_shell_window),
                LPARAM((&mut windows as *mut Vec<ShellWindow>) as isize),
            );
        }
        windows
    }

    fn is_desktop_shell_window(hwnd: HWND) -> bool {
        shell_windows()
            .into_iter()
            .any(|window| window.hwnd == hwnd && (window.is_progman || window.has_shell_view))
    }

    fn rect_contains(container: &RECT, target: &RECT) -> bool {
        target.left >= container.left
            && target.top >= container.top
            && target.right <= container.right
            && target.bottom <= container.bottom
    }

    fn workerw_covers_poster_rect(workerw: HWND, poster_rect: &RECT) -> bool {
        let mut workerw_rect = RECT::default();
        if unsafe { GetWindowRect(workerw, &mut workerw_rect) }.is_err()
            || !rect_contains(&workerw_rect, poster_rect)
        {
            return false;
        }

        let poster_center = POINT {
            x: poster_rect.left + (poster_rect.right - poster_rect.left) / 2,
            y: poster_rect.top + (poster_rect.bottom - poster_rect.top) / 2,
        };
        let poster_monitor = unsafe { MonitorFromPoint(poster_center, MONITOR_DEFAULTTONEAREST) };
        let workerw_monitor = unsafe { MonitorFromWindow(workerw, MONITOR_DEFAULTTONEAREST) };
        poster_monitor == workerw_monitor
    }

    fn select_workerw(windows: &[ShellWindow], poster_rect: &RECT) -> Option<HWND> {
        if let Some(candidate) = windows.iter().find(|window| {
            window.is_workerw
                && window.visible
                && window.has_shell_view
                && workerw_covers_poster_rect(window.hwnd, poster_rect)
        }) {
            return Some(candidate.hwnd);
        }

        for icon_host in windows.iter().filter(|window| window.has_shell_view) {
            let next_worker =
                unsafe { FindWindowExW(None, Some(icon_host.hwnd), w!("WorkerW"), PCWSTR::null()) };
            if let Ok(workerw) = next_worker {
                if unsafe { IsWindowVisible(workerw).as_bool() }
                    && workerw_covers_poster_rect(workerw, poster_rect)
                {
                    return Some(workerw);
                }
            }
        }

        windows
            .iter()
            .find(|window| {
                window.is_workerw
                    && window.visible
                    && workerw_covers_poster_rect(window.hwnd, poster_rect)
            })
            .map(|window| window.hwnd)
    }

    fn class_name(hwnd: HWND) -> Option<String> {
        let mut buffer = [0u16; 64];
        let length = unsafe { GetClassNameW(hwnd, &mut buffer) };
        (length > 0).then(|| String::from_utf16_lossy(&buffer[..length as usize]))
    }

    fn monitor_descriptor(handle: HMONITOR) -> Option<MonitorDescriptor> {
        if handle.0.is_null() {
            return None;
        }
        let mut details = MONITORINFOEXW::default();
        details.monitorInfo.cbSize = std::mem::size_of::<MONITORINFOEXW>() as u32;
        let succeeded = unsafe {
            GetMonitorInfoW(
                handle,
                (&mut details as *mut MONITORINFOEXW).cast::<MONITORINFO>(),
            )
        };
        if !succeeded.as_bool() {
            return None;
        }
        let device_length = details
            .szDevice
            .iter()
            .position(|character| *character == 0)
            .unwrap_or(details.szDevice.len());
        Some(MonitorDescriptor {
            handle,
            device: String::from_utf16_lossy(&details.szDevice[..device_length]),
            work_area: details.monitorInfo.rcWork,
        })
    }

    pub fn poster_work_area_size(window: &tauri::WebviewWindow) -> Option<(u32, u32)> {
        let hwnd = window.hwnd().ok()?;
        let monitor = unsafe { MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST) };
        let descriptor = monitor_descriptor(monitor)?;
        Some((
            (descriptor.work_area.right - descriptor.work_area.left).max(1) as u32,
            (descriptor.work_area.bottom - descriptor.work_area.top).max(1) as u32,
        ))
    }

    unsafe extern "system" fn collect_monitor(
        monitor: HMONITOR,
        _device_context: HDC,
        _monitor_rect: *mut RECT,
        data: LPARAM,
    ) -> BOOL {
        let monitors = &mut *(data.0 as *mut Vec<MonitorDescriptor>);
        if let Some(descriptor) = monitor_descriptor(monitor) {
            monitors.push(descriptor);
        }
        BOOL(1)
    }

    fn available_monitors() -> Vec<MonitorDescriptor> {
        let mut monitors = Vec::new();
        unsafe {
            let _ = EnumDisplayMonitors(
                None,
                None,
                Some(collect_monitor),
                LPARAM((&mut monitors as *mut Vec<MonitorDescriptor>) as isize),
            );
        }
        monitors
    }

    fn usable_axis_span(work_start: i32, work_end: i32, window_size: i32) -> i32 {
        (work_end - work_start - window_size).max(0)
    }

    fn normalized_axis_anchor(position: i32, work_start: i32, usable_span: i32) -> f64 {
        if usable_span <= 0 {
            0.0
        } else {
            ((position - work_start) as f64 / usable_span as f64).clamp(0.0, 1.0)
        }
    }

    fn capture_poster_placement_for_hwnd(hwnd: HWND) -> Result<PosterPlacement, String> {
        let mut window_rect = RECT::default();
        unsafe { GetWindowRect(hwnd, &mut window_rect) }.map_err(|error| error.to_string())?;
        let monitor = unsafe { MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST) };
        let details = monitor_descriptor(monitor)
            .ok_or_else(|| "failed to resolve the poster monitor work area".to_string())?;
        let window_width = (window_rect.right - window_rect.left).max(1);
        let window_height = (window_rect.bottom - window_rect.top).max(1);
        let horizontal_span = usable_axis_span(
            details.work_area.left,
            details.work_area.right,
            window_width,
        );
        let vertical_span = usable_axis_span(
            details.work_area.top,
            details.work_area.bottom,
            window_height,
        );
        let placement = PosterPlacement::new(
            Some(details.device),
            normalized_axis_anchor(window_rect.left, details.work_area.left, horizontal_span),
            normalized_axis_anchor(window_rect.top, details.work_area.top, vertical_span),
            window_rect.left,
            window_rect.top,
        );
        Ok(placement)
    }

    pub fn capture_poster_window_placement(
        window: &tauri::Window,
    ) -> Result<PosterPlacement, String> {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        capture_poster_placement_for_hwnd(hwnd)
    }

    fn restore_poster_placement_for_hwnd(
        hwnd: HWND,
        placement: &PosterPlacement,
    ) -> Result<(), String> {
        let mut window_rect = RECT::default();
        unsafe { GetWindowRect(hwnd, &mut window_rect) }.map_err(|error| error.to_string())?;
        let monitors = available_monitors();
        let fallback_monitor = unsafe {
            MonitorFromPoint(
                POINT {
                    x: placement.fallback_x,
                    y: placement.fallback_y,
                },
                MONITOR_DEFAULTTONEAREST,
            )
        };
        let target = placement
            .monitor_device
            .as_deref()
            .and_then(|device| monitors.iter().find(|monitor| monitor.device == device))
            .cloned()
            .or_else(|| {
                monitors
                    .iter()
                    .find(|monitor| monitor.handle == fallback_monitor)
                    .cloned()
            })
            .or_else(|| monitor_descriptor(fallback_monitor))
            .ok_or_else(|| "no monitor work area is available for poster recovery".to_string())?;

        let window_width = (window_rect.right - window_rect.left).max(1);
        let window_height = (window_rect.bottom - window_rect.top).max(1);
        let horizontal_span =
            usable_axis_span(target.work_area.left, target.work_area.right, window_width);
        let vertical_span =
            usable_axis_span(target.work_area.top, target.work_area.bottom, window_height);
        let screen_x = target.work_area.left
            + (placement.anchor_x.clamp(0.0, 1.0) * horizontal_span as f64).round() as i32;
        let screen_y = target.work_area.top
            + (placement.anchor_y.clamp(0.0, 1.0) * vertical_span as f64).round() as i32;

        let mut target_point = POINT {
            x: screen_x,
            y: screen_y,
        };
        if let Ok(parent) = unsafe { GetParent(hwnd) } {
            if !parent.0.is_null()
                && !unsafe { ScreenToClient(parent, &mut target_point).as_bool() }
            {
                return Err("failed to translate restored poster coordinates".to_string());
            }
        }
        unsafe {
            SetWindowPos(
                hwnd,
                None,
                target_point.x,
                target_point.y,
                0,
                0,
                SWP_NOZORDER | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS,
            )
            .map_err(|error| error.to_string())?;
        }
        Ok(())
    }

    pub fn restore_poster_placement(
        window: &tauri::WebviewWindow,
        placement: &PosterPlacement,
    ) -> Result<(), String> {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        restore_poster_placement_for_hwnd(hwnd, placement)
    }

    fn remember_poster_window(hwnd: HWND) {
        POSTER_HWND.store(hwnd.0 as isize, Ordering::Release);
    }

    fn poster_window() -> Option<HWND> {
        let raw = POSTER_HWND.load(Ordering::Acquire);
        (raw != 0).then(|| HWND(raw as *mut core::ffi::c_void))
    }

    unsafe extern "system" fn collect_poster_surface_window(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let windows = &mut *(lparam.0 as *mut Vec<HWND>);
        let mut process_id = 0;
        GetWindowThreadProcessId(hwnd, Some(&mut process_id));
        if process_id != GetCurrentProcessId()
            || !IsWindowVisible(hwnd).as_bool()
            || !is_top_level_tool_window(hwnd)
        {
            return BOOL(1);
        }

        let extended_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
        if extended_style & WS_EX_TOOLWINDOW.0 == 0 || extended_style & WS_EX_NOACTIVATE.0 == 0 {
            return BOOL(1);
        }

        let class = class_name(hwnd);
        if matches!(
            class.as_deref(),
            Some("tray_icon_app" | "Tao Thread Event Target")
        ) {
            return BOOL(1);
        }
        windows.push(hwnd);
        BOOL(1)
    }

    fn poster_surface_windows() -> Vec<HWND> {
        let mut windows = Vec::new();
        unsafe {
            let _ = EnumWindows(
                Some(collect_poster_surface_window),
                LPARAM((&mut windows as *mut Vec<HWND>) as isize),
            );
        }
        windows
    }

    fn fallback_z_order_anchor(hwnd: HWND) -> HWND {
        let progman = shell_windows()
            .into_iter()
            .find(|window| window.is_progman)
            .map(|window| window.hwnd);
        unsafe {
            progman
                .and_then(|progman| GetWindow(progman, GW_HWNDPREV).ok())
                .and_then(|anchor| {
                    if anchor == hwnd {
                        GetWindow(hwnd, GW_HWNDPREV).ok()
                    } else {
                        Some(anchor)
                    }
                })
                .unwrap_or(HWND_BOTTOM)
        }
    }

    fn anchor_tool_window_to_desktop(hwnd: HWND) -> Result<(), String> {
        let z_order_anchor = fallback_z_order_anchor(hwnd);
        unsafe {
            SetWindowPos(
                hwnd,
                Some(z_order_anchor),
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS,
            )
            .map_err(|error| error.to_string())
        }
    }

    fn anchor_tool_window_to_desktop_immediately(hwnd: HWND) -> Result<(), String> {
        let z_order_anchor = fallback_z_order_anchor(hwnd);
        unsafe {
            SetWindowPos(
                hwnd,
                Some(z_order_anchor),
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE,
            )
            .map_err(|error| error.to_string())
        }
    }

    fn anchor_poster_surface_windows_to_desktop() -> Result<bool, String> {
        let windows = poster_surface_windows();
        if windows.is_empty() {
            return Ok(false);
        }
        for hwnd in windows {
            anchor_tool_window_to_desktop_immediately(hwnd)?;
        }
        Ok(true)
    }

    fn repair_tool_window_during_shell_transition() {
        if !POSTER_VISIBLE_INTENT.load(Ordering::Acquire)
            || !TOOL_WINDOW_FALLBACK_ACTIVE.load(Ordering::Acquire)
        {
            return;
        }
        let _ = anchor_poster_surface_windows_to_desktop();
    }

    fn is_top_level_tool_window(hwnd: HWND) -> bool {
        match unsafe { GetParent(hwnd) } {
            Ok(parent) => parent.0.is_null(),
            Err(_) => true,
        }
    }

    fn settle_poster_desktop_layer_after_show() -> Result<(), String> {
        thread::Builder::new()
            .name("desktop-up-post-show-layer-settle".to_string())
            .spawn(|| {
                for delay_ms in POST_SHOW_DESKTOP_LAYER_SETTLE_DELAYS_MS {
                    thread::sleep(Duration::from_millis(delay_ms));
                    match anchor_poster_surface_windows_to_desktop() {
                        Ok(true) => {}
                        Ok(false) | Err(_) => return,
                    }
                }
            })
            .map(|_| ())
            .map_err(|error| error.to_string())
    }

    pub fn show_poster_at_desktop_layer(window: &tauri::WebviewWindow) -> Result<(), String> {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        remember_poster_window(hwnd);
        unsafe {
            let _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        }
        if is_top_level_tool_window(hwnd) {
            TOOL_WINDOW_FALLBACK_ACTIVE.store(true, Ordering::Release);
            anchor_poster_surface_windows_to_desktop()?;
            settle_poster_desktop_layer_after_show()
        } else {
            Ok(())
        }
    }

    pub fn repair_shell_host(window: &tauri::WebviewWindow) -> Result<bool, String> {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        remember_poster_window(hwnd);
        if TOOL_WINDOW_FALLBACK_ACTIVE.load(Ordering::Acquire) || is_top_level_tool_window(hwnd) {
            TOOL_WINDOW_FALLBACK_ACTIVE.store(true, Ordering::Release);
            anchor_poster_surface_windows_to_desktop()?;
            return Ok(true);
        }

        let parent = unsafe { GetParent(hwnd) }.map_err(|error| error.to_string())?;
        Ok(!parent.0.is_null() && matches!(class_name(parent).as_deref(), Some("WorkerW")))
    }

    fn take_pending_host_recovery_request(receiver: &Receiver<()>) -> Option<HostRecoveryRequest> {
        let pending = HOST_RECOVERY_PENDING.get_or_init(|| Mutex::new(None));
        let mut pending = pending.lock().ok()?;
        let request = pending.take();
        while receiver.try_recv().is_ok() {}
        request
    }

    fn clear_host_recovery_if_idle(generation: usize) {
        let pending = HOST_RECOVERY_PENDING.get_or_init(|| Mutex::new(None));
        let Ok(pending) = pending.lock() else {
            return;
        };
        if HOST_RECOVERY_GENERATION.load(Ordering::Acquire) == generation && pending.is_none() {
            HOST_RECOVERY_ACTIVE.store(false, Ordering::Release);
        }
    }

    fn pending_host_recovery_includes_shell(generation: usize) -> bool {
        let pending = HOST_RECOVERY_PENDING.get_or_init(|| Mutex::new(None));
        let Ok(pending) = pending.lock() else {
            return false;
        };
        pending
            .as_ref()
            .is_some_and(|request| request.generation == generation && request.includes_shell())
    }

    fn run_shell_recovery_worker(receiver: Receiver<()>, handler: HostRecoveryHandler) {
        while receiver.recv().is_ok() {
            HOST_RECOVERY_ACTIVE.store(true, Ordering::Release);
            loop {
                let generation = HOST_RECOVERY_GENERATION.load(Ordering::Acquire);
                let mut elapsed_ms = 0;
                let mut superseded = false;
                if pending_host_recovery_includes_shell(generation) {
                    for repair_at_ms in SHELL_DESKTOP_LAYER_REPAIR_DELAYS_MS {
                        thread::sleep(Duration::from_millis(repair_at_ms - elapsed_ms));
                        elapsed_ms = repair_at_ms;
                        while receiver.try_recv().is_ok() {}
                        if HOST_RECOVERY_GENERATION.load(Ordering::Acquire) != generation {
                            superseded = true;
                            break;
                        }
                        repair_tool_window_during_shell_transition();
                    }
                }
                if superseded {
                    continue;
                }
                thread::sleep(Duration::from_millis(SHELL_RECOVERY_SETTLE_MS - elapsed_ms));
                while receiver.try_recv().is_ok() {}
                if HOST_RECOVERY_GENERATION.load(Ordering::Acquire) == generation {
                    break;
                }
            }

            let Some(request) = take_pending_host_recovery_request(&receiver) else {
                HOST_RECOVERY_ACTIVE.store(false, Ordering::Release);
                continue;
            };
            handler(request.clone());
            clear_host_recovery_if_idle(request.generation);
        }
    }

    unsafe extern "system" fn shell_foreground_event(
        _hook: HWINEVENTHOOK,
        event: u32,
        hwnd: HWND,
        object_id: i32,
        _child_id: i32,
        _event_thread: u32,
        _event_time: u32,
    ) {
        let is_shell_foreground = event == EVENT_SYSTEM_FOREGROUND;
        let is_shell_lifecycle = (EVENT_OBJECT_CREATE..=EVENT_OBJECT_SHOW).contains(&event)
            && object_id == OBJID_WINDOW.0;
        if hwnd.0.is_null() {
            return;
        }
        let is_shell_window = matches!(class_name(hwnd).as_deref(), Some("Progman" | "WorkerW"));
        if (!is_shell_foreground && !is_shell_lifecycle)
            || !is_shell_window
            || !is_desktop_shell_window(hwnd)
        {
            return;
        }
        let trigger = if is_shell_foreground {
            HostRecoveryTrigger::ShellForeground
        } else {
            HostRecoveryTrigger::ShellLifecycle
        };
        repair_tool_window_during_shell_transition();
        queue_host_recovery(trigger);
    }

    fn run_shell_foreground_hook() {
        unsafe {
            let foreground_hook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                None,
                Some(shell_foreground_event),
                0,
                0,
                WINEVENT_OUTOFCONTEXT,
            );
            let shell_lifecycle_hook = SetWinEventHook(
                EVENT_OBJECT_CREATE,
                EVENT_OBJECT_SHOW,
                None,
                Some(shell_foreground_event),
                0,
                0,
                WINEVENT_OUTOFCONTEXT,
            );
            if foreground_hook.0.is_null() && shell_lifecycle_hook.0.is_null() {
                eprintln!("failed to install shell foreground recovery hook");
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
            if !shell_lifecycle_hook.0.is_null() {
                let _ = UnhookWinEvent(shell_lifecycle_hook);
            }
        }
    }

    fn queue_host_recovery(trigger: HostRecoveryTrigger) {
        if !POSTER_VISIBLE_INTENT.load(Ordering::Acquire) {
            return;
        }
        let Some(sender) = HOST_RECOVERY_SENDER.get() else {
            return;
        };
        let pending = HOST_RECOVERY_PENDING.get_or_init(|| Mutex::new(None));
        let Ok(mut pending) = pending.lock() else {
            eprintln!("failed to lock the desktop host recovery queue");
            return;
        };

        HOST_RECOVERY_ACTIVE.store(true, Ordering::Release);
        let generation = HOST_RECOVERY_GENERATION
            .fetch_add(1, Ordering::AcqRel)
            .wrapping_add(1);
        merge_host_recovery_request(&mut pending, generation, trigger);
        let _ = sender.try_send(());
    }

    pub fn request_host_recovery(trigger: HostRecoveryTrigger) {
        queue_host_recovery(trigger);
    }

    pub fn placement_recovery_in_progress() -> bool {
        HOST_RECOVERY_ACTIVE.load(Ordering::Acquire)
    }

    fn input_passthrough_extended_style(current_style: u32, passthrough: bool) -> u32 {
        if passthrough {
            current_style | WS_EX_TRANSPARENT.0 | WS_EX_LAYERED.0
        } else {
            current_style & !WS_EX_TRANSPARENT.0
        }
    }
    fn poster_input_extended_style(current_style: u32, passthrough: bool) -> u32 {
        input_passthrough_extended_style(tool_window_extended_style(current_style), passthrough)
    }

    fn apply_input_passthrough(hwnd: HWND, passthrough: bool) {
        INPUT_PASSTHROUGH_ACTIVE.store(passthrough, Ordering::Release);
        let current_style = unsafe { GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32 };
        let next_style = poster_input_extended_style(current_style, passthrough);
        if next_style != current_style {
            unsafe {
                SetWindowLongPtrW(hwnd, GWL_EXSTYLE, next_style as isize);
                let _ = SetWindowPos(
                    hwnd,
                    None,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED,
                );
            }
        }
    }

    fn input_hit_at_screen_point(point: POINT) -> Option<bool> {
        let hwnd = poster_window()?;
        if !unsafe { IsWindowVisible(hwnd).as_bool() } {
            return None;
        }
        let mut window_rect = RECT::default();
        if unsafe { GetWindowRect(hwnd, &mut window_rect) }.is_err()
            || point.x < window_rect.left
            || point.x >= window_rect.right
            || point.y < window_rect.top
            || point.y >= window_rect.bottom
        {
            return None;
        }
        let local_x = point.x - window_rect.left;
        let local_y = point.y - window_rect.top;
        let hit = INPUT_HIT_REGIONS
            .get_or_init(|| RwLock::new(Vec::new()))
            .read()
            .map(|regions| {
                regions
                    .iter()
                    .any(|region| region.contains(local_x, local_y))
            })
            .unwrap_or(true);
        Some(hit)
    }

    fn request_input_passthrough(passthrough: bool) {
        if INPUT_PASSTHROUGH_ACTIVE.load(Ordering::Acquire) == passthrough {
            return;
        }
        if let Some(hwnd) = poster_window() {
            unsafe {
                SendMessageW(
                    hwnd,
                    WM_POSTER_INPUT_PASSTHROUGH,
                    Some(WPARAM(usize::from(passthrough))),
                    None,
                );
            }
        }
    }

    unsafe extern "system" fn input_mouse_hook(
        code: i32,
        wparam: WPARAM,
        lparam: LPARAM,
    ) -> LRESULT {
        if code >= 0 {
            let message = wparam.0 as u32;
            let button_down = matches!(
                message,
                WM_LBUTTONDOWN | WM_RBUTTONDOWN | WM_MBUTTONDOWN | WM_XBUTTONDOWN
            );
            let button_up = matches!(
                message,
                WM_LBUTTONUP | WM_RBUTTONUP | WM_MBUTTONUP | WM_XBUTTONUP
            );
            let should_probe = matches!(
                message,
                WM_MOUSEMOVE
                    | WM_MOUSEWHEEL
                    | WM_MOUSEHWHEEL
                    | WM_LBUTTONDOWN
                    | WM_RBUTTONDOWN
                    | WM_MBUTTONDOWN
                    | WM_XBUTTONDOWN
            );

            if button_up {
                INPUT_MOUSE_BUTTON_ACTIVE.store(false, Ordering::Release);
            } else if should_probe && !INPUT_MOUSE_BUTTON_ACTIVE.load(Ordering::Acquire) {
                if let Some(details) = (lparam.0 as *const MSLLHOOKSTRUCT).as_ref() {
                    if let Some(hit) = input_hit_at_screen_point(details.pt) {
                        request_input_passthrough(!hit);
                    }
                }
                if button_down {
                    INPUT_MOUSE_BUTTON_ACTIVE.store(true, Ordering::Release);
                }
            }
        }
        CallNextHookEx(None, code, wparam, lparam)
    }

    fn run_input_mouse_hook(ready: SyncSender<Result<(), String>>) {
        unsafe {
            let hook = match SetWindowsHookExW(WH_MOUSE_LL, Some(input_mouse_hook), None, 0) {
                Ok(hook) => hook,
                Err(error) => {
                    let _ = ready.send(Err(error.to_string()));
                    return;
                }
            };
            let _ = ready.send(Ok(()));
            let mut message = MSG::default();
            while GetMessageW(&mut message, None, 0, 0).0 > 0 {
                let _ = TranslateMessage(&message);
                DispatchMessageW(&message);
            }
            let _ = UnhookWindowsHookEx(hook);
        }
    }

    fn install_input_mouse_hook() -> Result<(), String> {
        if INPUT_MOUSE_HOOK_STARTED.get().is_some() {
            return Ok(());
        }
        let (ready_sender, ready_receiver) = sync_channel(1);
        thread::Builder::new()
            .name("desktop-up-input-hit-test".to_string())
            .spawn(move || run_input_mouse_hook(ready_sender))
            .map_err(|error| error.to_string())?;
        ready_receiver
            .recv_timeout(Duration::from_secs(2))
            .map_err(|error| format!("poster input mouse hook did not start: {error}"))??;
        let _ = INPUT_MOUSE_HOOK_STARTED.set(());
        Ok(())
    }

    unsafe extern "system" fn poster_window_subclass(
        hwnd: HWND,
        message: u32,
        wparam: WPARAM,
        lparam: LPARAM,
        subclass_id: usize,
        _reference_data: usize,
    ) -> LRESULT {
        if message == WM_POSTER_INPUT_PASSTHROUGH {
            apply_input_passthrough(hwnd, wparam.0 != 0);
            return LRESULT(0);
        }
        if message == WM_STYLECHANGING && wparam.0 as i32 == GWL_EXSTYLE.0 {
            if let Some(style) = (lparam.0 as *mut STYLESTRUCT).as_mut() {
                style.styleNew = poster_input_extended_style(
                    style.styleNew,
                    INPUT_PASSTHROUGH_ACTIVE.load(Ordering::Acquire),
                );
            }
        }
        if message == WM_STYLECHANGING && wparam.0 as i32 == GWL_STYLE.0 {
            // Tauri/tao may reapply its cached top-level style while the initially hidden
            // WebView is shown. Enforce the desktop-host invariant at the Win32 boundary so
            // WS_CAPTION cannot reappear after the one-time configuration path has finished.
            if let Some(style) = (lparam.0 as *mut STYLESTRUCT).as_mut() {
                style.styleNew = strip_non_client_decoration_styles(style.styleNew);
            }
        }
        if message == WM_MOVING {
            // Clamp the drag rectangle to the nearest monitor's work area so the
            // poster can never be dragged off-screen.
            if let Some(rect) = (lparam.0 as *mut RECT).as_mut() {
                let monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if let Some(descriptor) = monitor_descriptor(monitor) {
                    let window_width = rect.right - rect.left;
                    let window_height = rect.bottom - rect.top;
                    let horizontal_span = usable_axis_span(
                        descriptor.work_area.left,
                        descriptor.work_area.right,
                        window_width,
                    );
                    let vertical_span = usable_axis_span(
                        descriptor.work_area.top,
                        descriptor.work_area.bottom,
                        window_height,
                    );
                    let clamp_left = descriptor.work_area.left;
                    let clamp_top = descriptor.work_area.top;
                    let clamp_right = clamp_left + horizontal_span;
                    let clamp_bottom = clamp_top + vertical_span;
                    if rect.left < clamp_left {
                        let offset = clamp_left - rect.left;
                        rect.left += offset;
                        rect.right += offset;
                    } else if rect.left > clamp_right {
                        let offset = rect.left - clamp_right;
                        rect.left -= offset;
                        rect.right -= offset;
                    }
                    if rect.top < clamp_top {
                        let offset = clamp_top - rect.top;
                        rect.top += offset;
                        rect.bottom += offset;
                    } else if rect.top > clamp_bottom {
                        let offset = rect.top - clamp_bottom;
                        rect.top -= offset;
                        rect.bottom -= offset;
                    }
                }
            }
            return LRESULT(1); // handled — write the (clamped) RECT back
        }
        if message == WM_EXITSIZEMOVE {
            // After the user finishes dragging, re-anchor the poster to the
            // desktop Z-order layer so it never stays above application windows.
            if TOOL_WINDOW_FALLBACK_ACTIVE.load(Ordering::Acquire) {
                let _ = anchor_tool_window_to_desktop(hwnd);
            }
        }
        if message == WM_MOUSEACTIVATE {
            // The poster must stay mouse-interactive without becoming a foreground application.
            // Reassert the desktop layer before allowing the click through to the WebView.
            if TOOL_WINDOW_FALLBACK_ACTIVE.load(Ordering::Acquire) {
                let _ = anchor_tool_window_to_desktop(hwnd);
            }
            return LRESULT(MA_NOACTIVATE as isize);
        }
        match message {
            WM_DISPLAYCHANGE => queue_host_recovery(HostRecoveryTrigger::DisplayChange),
            WM_DPICHANGED => queue_host_recovery(HostRecoveryTrigger::DpiChange),
            WM_SETTINGCHANGE => queue_host_recovery(HostRecoveryTrigger::WorkAreaChange),
            WM_DESTROY => {
                let _ = RemoveWindowSubclass(hwnd, Some(poster_window_subclass), subclass_id);
                POSTER_HWND.store(0, Ordering::Release);
            }
            _ => {}
        }
        DefSubclassProc(hwnd, message, wparam, lparam)
    }

    pub fn install_shell_foreground_monitor<F>(
        window: &tauri::WebviewWindow,
        recovery_handler: F,
    ) -> Result<(), String>
    where
        F: Fn(HostRecoveryRequest) + Send + Sync + 'static,
    {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        remember_poster_window(hwnd);
        install_input_mouse_hook()?;
        if HOST_RECOVERY_SENDER.get().is_some() {
            return Ok(());
        }

        let recovery_handler: HostRecoveryHandler = Arc::new(recovery_handler);
        let (sender, receiver) = sync_channel(1);
        HOST_RECOVERY_SENDER
            .set(sender)
            .map_err(|_| "shell foreground monitor was already initialized".to_string())?;
        thread::Builder::new()
            .name("desktop-up-shell-recovery".to_string())
            .spawn(move || run_shell_recovery_worker(receiver, recovery_handler))
            .map_err(|error| error.to_string())?;
        thread::Builder::new()
            .name("desktop-up-shell-events".to_string())
            .spawn(run_shell_foreground_hook)
            .map_err(|error| error.to_string())?;
        let subclass_installed = unsafe {
            SetWindowSubclass(
                hwnd,
                Some(poster_window_subclass),
                POSTER_WINDOW_SUBCLASS_ID,
                0,
            )
        };
        if !subclass_installed.as_bool() {
            return Err("failed to install poster display recovery subclass".to_string());
        }
        Ok(())
    }

    pub fn set_poster_visibility_intent(visible: bool) {
        POSTER_VISIBLE_INTENT.store(visible, Ordering::Release);
    }

    fn set_common_extended_style(hwnd: HWND) {
        unsafe {
            let current_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
            let tool_style = tool_window_extended_style(current_style);
            SetWindowLongPtrW(hwnd, GWL_EXSTYLE, tool_style as isize);
        }
    }

    fn tool_window_extended_style(current_style: u32) -> u32 {
        (current_style & !WS_EX_APPWINDOW.0) | WS_EX_TOOLWINDOW.0 | WS_EX_NOACTIVATE.0
    }

    fn suppress_dwm_non_client_frame(hwnd: HWND) {
        unsafe {
            let policy = DWMNCRP_DISABLED;
            let _ = DwmSetWindowAttribute(
                hwnd,
                DWMWA_NCRENDERING_POLICY,
                std::ptr::from_ref(&policy).cast(),
                std::mem::size_of_val(&policy) as u32,
            );

            // Windows 11 can retain a system-colored frame even after WS_CAPTION is removed.
            // The poster owns its border and corner mask, so suppress DWM's border explicitly.
            let border_color: u32 = 0xFFFF_FFFE;
            let _ = DwmSetWindowAttribute(
                hwnd,
                DWMWA_BORDER_COLOR,
                std::ptr::from_ref(&border_color).cast(),
                std::mem::size_of_val(&border_color) as u32,
            );

            let corner_preference = DWMWCP_DONOTROUND;
            let _ = DwmSetWindowAttribute(
                hwnd,
                DWMWA_WINDOW_CORNER_PREFERENCE,
                std::ptr::from_ref(&corner_preference).cast(),
                std::mem::size_of_val(&corner_preference) as u32,
            );
        }
    }

    fn attach_to_workerw(hwnd: HWND, workerw: HWND) -> Result<(), String> {
        let mut screen_rect = RECT::default();
        unsafe { GetWindowRect(hwnd, &mut screen_rect) }.map_err(|error| error.to_string())?;
        if !workerw_covers_poster_rect(workerw, &screen_rect) {
            return Err(
                "WorkerW candidate does not cover the restored poster screen rectangle".to_string(),
            );
        }

        unsafe {
            set_common_extended_style(hwnd);
            let current_style = GetWindowLongPtrW(hwnd, GWL_STYLE) as u32;
            let child_style =
                (strip_non_client_decoration_styles(current_style) & !WS_POPUP.0) | WS_CHILD.0;
            SetWindowLongPtrW(hwnd, GWL_STYLE, child_style as isize);
            suppress_dwm_non_client_frame(hwnd);

            // SetParent may return NULL on success when the old parent was the desktop. Verify
            // the resulting parent explicitly instead of trusting the generated wrapper result.
            let _ = SetParent(hwnd, Some(workerw));
            if GetParent(hwnd).ok() != Some(workerw) {
                return Err("SetParent did not attach the poster to WorkerW".to_string());
            }

            let mut parent_point = POINT {
                x: screen_rect.left,
                y: screen_rect.top,
            };
            if !ScreenToClient(workerw, &mut parent_point).as_bool() {
                return Err("failed to translate poster coordinates into WorkerW".to_string());
            }

            SetWindowPos(
                hwnd,
                Some(HWND_TOP),
                parent_point.x,
                parent_point.y,
                0,
                0,
                SWP_FRAMECHANGED | SWP_NOSIZE | SWP_NOACTIVATE,
            )
            .map_err(|error| error.to_string())?;
        }
        Ok(())
    }

    fn configure_tool_window_fallback(hwnd: HWND) -> Result<(), String> {
        let mut screen_rect = RECT::default();
        unsafe { GetWindowRect(hwnd, &mut screen_rect) }.map_err(|error| error.to_string())?;

        unsafe {
            let _ = SetParent(hwnd, None);
            let current_style = GetWindowLongPtrW(hwnd, GWL_STYLE) as u32;
            let popup_style =
                (strip_non_client_decoration_styles(current_style) & !WS_CHILD.0) | WS_POPUP.0;
            SetWindowLongPtrW(hwnd, GWL_STYLE, popup_style as isize);
            set_common_extended_style(hwnd);
            suppress_dwm_non_client_frame(hwnd);

            // Keep the ToolWindow immediately above Progman, but below regular application
            // windows. HWND_BOTTOM remains the terminal fallback when the shell anchor is absent.
            let z_order_anchor = fallback_z_order_anchor(hwnd);
            SetWindowPos(
                hwnd,
                Some(z_order_anchor),
                screen_rect.left,
                screen_rect.top,
                0,
                0,
                SWP_FRAMECHANGED | SWP_NOSIZE | SWP_NOACTIVATE,
            )
            .map_err(|error| error.to_string())?;
        }
        Ok(())
    }

    pub fn configure(
        window: &tauri::WebviewWindow,
        prefer_workerw: bool,
    ) -> DesktopHostDiagnostics {
        let hwnd = match window.hwnd() {
            Ok(hwnd) => hwnd,
            Err(error) => {
                return DesktopHostDiagnostics {
                    mode: "toolWindowFallback".to_string(),
                    status: "无法读取原生窗口句柄".to_string(),
                    fallback_reason: Some(error.to_string()),
                    ..DesktopHostDiagnostics::default()
                };
            }
        };
        remember_poster_window(hwnd);

        let disabled_by_environment = env::var_os("DESKTOP_UP_DISABLE_WORKERW").is_some();
        let enabled_by_environment = env::var_os("DESKTOP_UP_ENABLE_WORKERW").is_some();
        let should_try_workerw =
            prefer_workerw && enabled_by_environment && !disabled_by_environment;
        let windows = shell_windows();
        let mut poster_rect = RECT::default();
        let poster_rect_available = match unsafe { GetWindowRect(hwnd, &mut poster_rect) } {
            Ok(()) => true,
            Err(_error) => false,
        };
        let mut request_error = (!poster_rect_available)
            .then(|| "failed to read the restored poster rectangle".to_string());

        // Do not ask Explorer to create a hidden WorkerW. Only attach to an already visible,
        // stable candidate that covers the restored poster rectangle; otherwise use ToolWindow.

        let workerw_detected = windows.iter().any(|window| window.is_workerw);
        if should_try_workerw && poster_rect_available {
            if let Some(workerw) = select_workerw(&windows, &poster_rect) {
                match attach_to_workerw(hwnd, workerw) {
                    Ok(()) => {
                        TOOL_WINDOW_FALLBACK_ACTIVE.store(false, Ordering::Release);
                        return DesktopHostDiagnostics {
                            mode: "workerW".to_string(),
                            status: "已挂载到 Windows WorkerW 桌面宿主".to_string(),
                            parent_class: class_name(workerw),
                            workerw_detected,
                            fallback_reason: None,
                            interaction_region_count: 0,
                            interaction_region: None,
                            ..DesktopHostDiagnostics::default()
                        };
                    }
                    Err(error) => request_error = Some(error),
                }
            }
        }

        let fallback_error = configure_tool_window_fallback(hwnd).err();
        TOOL_WINDOW_FALLBACK_ACTIVE.store(fallback_error.is_none(), Ordering::Release);
        let reason = if disabled_by_environment {
            Some("DESKTOP_UP_DISABLE_WORKERW requested the safe fallback".to_string())
        } else if !enabled_by_environment {
            Some("safe ToolWindow fallback is the default on this host".to_string())
        } else if !prefer_workerw {
            Some("caller requested the safe fallback".to_string())
        } else {
            request_error.or_else(|| {
                Some("no visible WorkerW candidate was available for safe attachment".to_string())
            })
        };

        DesktopHostDiagnostics {
            mode: "toolWindowFallback".to_string(),
            status: if fallback_error.is_none() {
                "WorkerW 不可用，已自动进入 ToolWindow 回退".to_string()
            } else {
                "WorkerW 与 ToolWindow 配置均出现错误".to_string()
            },
            parent_class: None,
            workerw_detected,
            fallback_reason: match (reason, fallback_error) {
                (Some(primary), Some(fallback)) => {
                    Some(format!("{primary}; fallback error: {fallback}"))
                }
                (None, Some(fallback)) => Some(format!("fallback error: {fallback}")),
                (primary, None) => primary,
            },
            interaction_region_count: 0,
            interaction_region: None,
            ..DesktopHostDiagnostics::default()
        }
    }

    fn scaled_interaction_bounds(
        region: &InteractionRegion,
        device_scale_factor: f64,
    ) -> (i32, i32, i32, i32, i32) {
        let scale = device_scale_factor.clamp(0.125, 16.0);
        let left = (region.rect.x.max(0.0) * scale).round() as i32;
        let top = (region.rect.y.max(0.0) * scale).round() as i32;
        let right = ((region.rect.x + region.rect.width.max(1.0)) * scale)
            .round()
            .max((left + 1) as f64) as i32;
        let bottom = ((region.rect.y + region.rect.height.max(1.0)) * scale)
            .round()
            .max((top + 1) as f64) as i32;
        let radius = (region.corner_radius.max(0.0) * scale).round() as i32;
        (left, top, right, bottom, radius)
    }

    fn allocate_visual_region(
        region: &InteractionRegion,
        device_scale_factor: f64,
    ) -> Result<HRGN, String> {
        let (left, top, right, bottom, radius) =
            scaled_interaction_bounds(region, device_scale_factor);
        let native_region = if radius > 0 {
            unsafe { CreateRoundRectRgn(left, top, right + 1, bottom + 1, radius * 2, radius * 2) }
        } else {
            unsafe { CreateRectRgn(left, top, right + 1, bottom + 1) }
        };
        if native_region.is_invalid() {
            return Err(format!(
                "failed to allocate visual region {}",
                region.region_id
            ));
        }
        Ok(native_region)
    }

    fn update_input_hit_regions(
        regions: &[InteractionRegion],
        device_scale_factor: f64,
    ) -> Result<(), String> {
        let physical_regions = regions
            .iter()
            .map(|region| {
                let (left, top, right, bottom, radius) =
                    scaled_interaction_bounds(region, device_scale_factor);
                PhysicalHitRegion {
                    left,
                    top,
                    right,
                    bottom,
                    radius,
                }
            })
            .collect();
        *INPUT_HIT_REGIONS
            .get_or_init(|| RwLock::new(Vec::new()))
            .write()
            .map_err(|error| error.to_string())? = physical_regions;
        Ok(())
    }

    pub fn apply_surface_regions(
        window: &tauri::WebviewWindow,
        visual_regions: &[InteractionRegion],
        input_regions: &[InteractionRegion],
        device_scale_factor: f64,
    ) -> Result<(), String> {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        let combined = unsafe { CreateRectRgn(0, 0, 0, 0) };
        if combined.is_invalid() {
            return Err("failed to allocate the combined visual region".to_string());
        }

        for region in visual_regions {
            let component = match allocate_visual_region(region, device_scale_factor) {
                Ok(component) => component,
                Err(error) => {
                    unsafe {
                        let _ = DeleteObject(HGDIOBJ(combined.0));
                    }
                    return Err(error);
                }
            };
            let combine_result =
                unsafe { CombineRgn(Some(combined), Some(combined), Some(component), RGN_OR) };
            unsafe {
                let _ = DeleteObject(HGDIOBJ(component.0));
            }
            if combine_result == RGN_ERROR {
                unsafe {
                    let _ = DeleteObject(HGDIOBJ(combined.0));
                }
                return Err("failed to merge a visual region".to_string());
            }
        }

        let result = unsafe { SetWindowRgn(hwnd, Some(combined), true) };
        if result == 0 {
            unsafe {
                let _ = DeleteObject(HGDIOBJ(combined.0));
            }
            return Err("failed to apply the combined visual region".to_string());
        }
        // Windows owns the visual region handle. Input hit testing stays independent so
        // semi-transparent poster background can remain visible while input passes through.
        update_input_hit_regions(input_regions, device_scale_factor)
    }

    #[cfg(test)]
    mod tests {
        use super::{
            poster_input_extended_style, rect_contains, scaled_interaction_bounds,
            strip_non_client_decoration_styles, tool_window_extended_style, PhysicalHitRegion,
        };
        use crate::host_contract::{InteractionRegion, LogicalRect};
        use windows::Win32::Foundation::RECT;
        use windows::Win32::UI::WindowsAndMessaging::{
            WS_CAPTION, WS_EX_APPWINDOW, WS_EX_LAYERED, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
            WS_EX_TRANSPARENT, WS_MAXIMIZEBOX, WS_MINIMIZEBOX, WS_SYSMENU, WS_THICKFRAME,
            WS_VISIBLE,
        };

        #[test]
        fn workerw_rectangle_must_fully_contain_the_poster() {
            let primary_workerw = RECT {
                left: 0,
                top: 0,
                right: 2560,
                bottom: 1440,
            };
            let secondary_poster = RECT {
                left: 3000,
                top: 400,
                right: 3490,
                bottom: 792,
            };
            assert!(!rect_contains(&primary_workerw, &secondary_poster));
        }

        #[test]
        fn virtual_desktop_rectangle_can_contain_a_secondary_poster() {
            let virtual_desktop_workerw = RECT {
                left: 0,
                top: 0,
                right: 4480,
                bottom: 1443,
            };
            let secondary_poster = RECT {
                left: 3000,
                top: 400,
                right: 3490,
                bottom: 792,
            };
            assert!(rect_contains(&virtual_desktop_workerw, &secondary_poster));
        }

        #[test]
        fn fallback_window_is_mouse_interactive_without_becoming_an_application() {
            let style = tool_window_extended_style(WS_EX_APPWINDOW.0);
            assert_eq!(style & WS_EX_APPWINDOW.0, 0);
            assert_ne!(style & WS_EX_TOOLWINDOW.0, 0);
            assert_ne!(style & WS_EX_NOACTIVATE.0, 0);
        }

        #[test]
        fn interaction_bounds_scale_once_across_dpi_matrix() {
            let region = InteractionRegion {
                region_id: "poster-primary".to_string(),
                rect: LogicalRect {
                    x: 10.0,
                    y: 12.0,
                    width: 100.0,
                    height: 80.0,
                },
                corner_radius: 16.0,
            };

            assert_eq!(
                scaled_interaction_bounds(&region, 1.0),
                (10, 12, 110, 92, 16)
            );
            assert_eq!(
                scaled_interaction_bounds(&region, 1.25),
                (13, 15, 138, 115, 20)
            );
            assert_eq!(
                scaled_interaction_bounds(&region, 2.0),
                (20, 24, 220, 184, 32)
            );
        }
        #[test]
        fn rounded_hit_regions_exclude_clipped_corners_and_outside_points() {
            let region = PhysicalHitRegion {
                left: 0,
                top: 0,
                right: 100,
                bottom: 80,
                radius: 12,
            };

            assert!(!region.contains(0, 0));
            assert!(!region.contains(1, 1));
            assert!(region.contains(12, 0));
            assert!(region.contains(0, 12));
            assert!(region.contains(50, 40));
            assert!(!region.contains(-1, 40));
            assert!(!region.contains(101, 40));
        }

        #[test]
        fn input_passthrough_style_preserves_layering_and_host_window_flags() {
            let base = WS_EX_APPWINDOW.0;
            let passthrough = poster_input_extended_style(base, true);
            assert_ne!(passthrough & WS_EX_TRANSPARENT.0, 0);
            assert_ne!(passthrough & WS_EX_LAYERED.0, 0);
            assert_ne!(passthrough & WS_EX_TOOLWINDOW.0, 0);
            assert_ne!(passthrough & WS_EX_NOACTIVATE.0, 0);
            assert_eq!(passthrough & WS_EX_APPWINDOW.0, 0);

            let interactive = poster_input_extended_style(passthrough, false);
            assert_eq!(interactive & WS_EX_TRANSPARENT.0, 0);
            assert_ne!(interactive & WS_EX_LAYERED.0, 0);
            assert_ne!(interactive & WS_EX_TOOLWINDOW.0, 0);
            assert_ne!(interactive & WS_EX_NOACTIVATE.0, 0);
            assert_eq!(interactive & WS_EX_APPWINDOW.0, 0);
        }

        #[test]
        fn later_style_changes_cannot_restore_native_decorations() {
            let proposed = WS_VISIBLE.0
                | WS_CAPTION.0
                | WS_THICKFRAME.0
                | WS_SYSMENU.0
                | WS_MINIMIZEBOX.0
                | WS_MAXIMIZEBOX.0;
            let sanitized = strip_non_client_decoration_styles(proposed);
            assert_ne!(sanitized & WS_VISIBLE.0, 0);
            assert_eq!(sanitized & WS_CAPTION.0, 0);
            assert_eq!(sanitized & WS_THICKFRAME.0, 0);
            assert_eq!(sanitized & WS_SYSMENU.0, 0);
            assert_eq!(sanitized & WS_MINIMIZEBOX.0, 0);
            assert_eq!(sanitized & WS_MAXIMIZEBOX.0, 0);
        }
    }
}

#[cfg(target_os = "windows")]
pub use platform::{
    apply_surface_regions, capture_poster_window_placement, configure,
    install_shell_foreground_monitor, placement_recovery_in_progress, poster_work_area_size,
    repair_shell_host, request_host_recovery, restore_poster_placement,
    set_poster_visibility_intent, show_poster_at_desktop_layer,
};

#[cfg(not(target_os = "windows"))]
pub fn configure(_window: &tauri::WebviewWindow, _prefer_workerw: bool) -> DesktopHostDiagnostics {
    DesktopHostDiagnostics {
        mode: "toolWindowFallback".to_string(),
        status: "当前平台仅使用普通窗口回退".to_string(),
        fallback_reason: Some("WorkerW is only available on Windows".to_string()),
        ..DesktopHostDiagnostics::default()
    }
}

#[cfg(not(target_os = "windows"))]
pub fn install_shell_foreground_monitor<F>(
    _window: &tauri::WebviewWindow,
    _recovery_handler: F,
) -> Result<(), String>
where
    F: Fn(HostRecoveryRequest) + Send + Sync + 'static,
{
    Ok(())
}

#[cfg(not(target_os = "windows"))]
pub fn show_poster_at_desktop_layer(window: &tauri::WebviewWindow) -> Result<(), String> {
    window.show().map_err(|error| error.to_string())
}

#[cfg(not(target_os = "windows"))]
pub fn repair_shell_host(_window: &tauri::WebviewWindow) -> Result<bool, String> {
    Ok(false)
}

#[cfg(not(target_os = "windows"))]
pub fn request_host_recovery(_trigger: HostRecoveryTrigger) {}

#[cfg(not(target_os = "windows"))]
pub fn set_poster_visibility_intent(_visible: bool) {}

#[cfg(not(target_os = "windows"))]
pub fn placement_recovery_in_progress() -> bool {
    false
}

#[cfg(not(target_os = "windows"))]
pub fn poster_work_area_size(_window: &tauri::WebviewWindow) -> Option<(u32, u32)> {
    None
}

#[cfg(not(target_os = "windows"))]
pub fn restore_poster_placement(
    window: &tauri::WebviewWindow,
    placement: &crate::placement::PosterPlacement,
) -> Result<(), String> {
    window
        .set_position(tauri::PhysicalPosition::new(
            placement.fallback_x,
            placement.fallback_y,
        ))
        .map_err(|error| error.to_string())
}

#[cfg(not(target_os = "windows"))]
pub fn capture_poster_window_placement(
    window: &tauri::Window,
) -> Result<crate::placement::PosterPlacement, String> {
    let position = window.outer_position().map_err(|error| error.to_string())?;
    Ok(crate::placement::PosterPlacement::new(
        None, 0.0, 0.0, position.x, position.y,
    ))
}

#[cfg(not(target_os = "windows"))]
pub fn apply_surface_regions(
    _window: &tauri::WebviewWindow,
    _visual_regions: &[InteractionRegion],
    _input_regions: &[InteractionRegion],
    _device_scale_factor: f64,
) -> Result<(), String> {
    Ok(())
}
