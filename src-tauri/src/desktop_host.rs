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

#[cfg(target_os = "windows")]
mod platform {
    use super::DesktopHostDiagnostics;
    use crate::placement::PosterPlacement;
    use std::{
        env,
        sync::{
            atomic::{AtomicBool, AtomicIsize, AtomicUsize, Ordering},
            mpsc::{sync_channel, Receiver, SyncSender},
            Mutex, OnceLock,
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
                CreateRoundRectRgn, DeleteObject, EnumDisplayMonitors, GetMonitorInfoW,
                MonitorFromPoint, MonitorFromWindow, ScreenToClient, SetWindowRgn, HDC, HGDIOBJ,
                HMONITOR, MONITORINFO, MONITORINFOEXW, MONITOR_DEFAULTTONEAREST,
            },
            UI::{
                Accessibility::{SetWinEventHook, UnhookWinEvent, HWINEVENTHOOK},
                Shell::{DefSubclassProc, RemoveWindowSubclass, SetWindowSubclass},
                WindowsAndMessaging::{
                    DispatchMessageW, EnumWindows, FindWindowExW, FindWindowW, GetClassNameW,
                    GetMessageW, GetParent, GetWindow, GetWindowLongPtrW, GetWindowRect, IsWindow,
                    IsWindowVisible, SetParent, SetWindowLongPtrW, SetWindowPos, ShowWindow,
                    TranslateMessage, EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW,
                    EVENT_SYSTEM_FOREGROUND, GWL_EXSTYLE, GWL_STYLE, GW_HWNDPREV, HWND_BOTTOM,
                    HWND_TOP, MA_NOACTIVATE, MSG, OBJID_WINDOW, SWP_ASYNCWINDOWPOS,
                    SWP_FRAMECHANGED, SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER,
                    SWP_SHOWWINDOW, SW_SHOWNOACTIVATE, WINEVENT_OUTOFCONTEXT, WM_DESTROY,
                    WM_DISPLAYCHANGE, WM_DPICHANGED, WM_EXITSIZEMOVE, WM_MOUSEACTIVATE, WM_MOVING,
                    WM_SETTINGCHANGE, WS_CAPTION, WS_CHILD, WS_EX_APPWINDOW, WS_EX_NOACTIVATE,
                    WS_EX_TOOLWINDOW, WS_MAXIMIZEBOX, WS_MINIMIZEBOX, WS_POPUP, WS_SYSMENU,
                    WS_THICKFRAME,
                },
            },
        },
    };

    const SHELL_RECOVERY_SETTLE_MS: u64 = 140;
    const POSTER_WINDOW_SUBCLASS_ID: usize = 0x4455_5031;
    const NON_CLIENT_DECORATION_STYLES: u32 =
        WS_CAPTION.0 | WS_THICKFRAME.0 | WS_SYSMENU.0 | WS_MINIMIZEBOX.0 | WS_MAXIMIZEBOX.0;

    static POSTER_HWND: AtomicIsize = AtomicIsize::new(0);
    static POSTER_VISIBLE_INTENT: AtomicBool = AtomicBool::new(true);
    static TOOL_WINDOW_FALLBACK_ACTIVE: AtomicBool = AtomicBool::new(false);
    static PLACEMENT_RECOVERY_ACTIVE: AtomicBool = AtomicBool::new(false);
    static PLACEMENT_RECOVERY_GENERATION: AtomicUsize = AtomicUsize::new(0);
    static SHELL_RECOVERY_SENDER: OnceLock<SyncSender<()>> = OnceLock::new();
    static LAST_POSTER_PLACEMENT: OnceLock<Mutex<Option<PosterPlacement>>> = OnceLock::new();

    #[derive(Clone, Copy)]
    struct ShellWindow {
        hwnd: HWND,
        is_workerw: bool,
        has_shell_view: bool,
        visible: bool,
    }

    #[derive(Clone)]
    struct MonitorDescriptor {
        handle: HMONITOR,
        device: String,
        work_area: RECT,
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

    fn remember_poster_placement(placement: &PosterPlacement) {
        let placement_slot = LAST_POSTER_PLACEMENT.get_or_init(|| Mutex::new(None));
        if let Ok(mut current) = placement_slot.lock() {
            *current = Some(placement.clone());
        }
    }

    fn remembered_poster_placement() -> Option<PosterPlacement> {
        LAST_POSTER_PLACEMENT
            .get()
            .and_then(|placement| placement.lock().ok().and_then(|value| value.clone()))
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
        remember_poster_placement(&placement);
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
        remember_poster_placement(placement);
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

    pub fn restore_poster_window_placement(
        window: &tauri::Window,
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

    fn fallback_z_order_anchor(hwnd: HWND) -> HWND {
        unsafe {
            FindWindowW(w!("Progman"), PCWSTR::null())
                .ok()
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

    fn restore_after_shell_or_display_event() -> Result<(), String> {
        if !POSTER_VISIBLE_INTENT.load(Ordering::Acquire) {
            return Ok(());
        }

        let Some(hwnd) = poster_window() else {
            return Ok(());
        };
        if !unsafe { IsWindow(Some(hwnd)).as_bool() } {
            return Ok(());
        }

        if TOOL_WINDOW_FALLBACK_ACTIVE.load(Ordering::Acquire) {
            let mut screen_rect = RECT::default();
            unsafe { GetWindowRect(hwnd, &mut screen_rect) }.map_err(|error| error.to_string())?;
            let z_order_anchor = fallback_z_order_anchor(hwnd);
            unsafe {
                let _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);
                SetWindowPos(
                    hwnd,
                    Some(z_order_anchor),
                    screen_rect.left,
                    screen_rect.top,
                    0,
                    0,
                    SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS | SWP_SHOWWINDOW,
                )
                .map_err(|error| error.to_string())?;
            }
        }
        if let Some(placement) = remembered_poster_placement() {
            restore_poster_placement_for_hwnd(hwnd, &placement)?;
        }
        Ok(())
    }

    fn run_shell_recovery_worker(receiver: Receiver<()>) {
        while receiver.recv().is_ok() {
            loop {
                PLACEMENT_RECOVERY_ACTIVE.store(true, Ordering::Release);
                let generation = PLACEMENT_RECOVERY_GENERATION.load(Ordering::Acquire);
                thread::sleep(Duration::from_millis(SHELL_RECOVERY_SETTLE_MS));
                while receiver.try_recv().is_ok() {}
                if let Err(error) = restore_after_shell_or_display_event() {
                    eprintln!("failed to restore poster after host recovery event: {error}");
                }
                PLACEMENT_RECOVERY_ACTIVE.store(false, Ordering::Release);
                if PLACEMENT_RECOVERY_GENERATION.load(Ordering::Acquire) == generation {
                    break;
                }
            }
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
        if (!is_shell_foreground && !is_shell_lifecycle) || hwnd.0.is_null() {
            return;
        }
        if !matches!(class_name(hwnd).as_deref(), Some("Progman" | "WorkerW")) {
            return;
        }
        queue_host_recovery();
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

    fn queue_host_recovery() {
        if let Some(sender) = SHELL_RECOVERY_SENDER.get() {
            PLACEMENT_RECOVERY_ACTIVE.store(true, Ordering::Release);
            PLACEMENT_RECOVERY_GENERATION.fetch_add(1, Ordering::AcqRel);
            let _ = sender.try_send(());
        }
    }

    pub fn placement_recovery_in_progress() -> bool {
        PLACEMENT_RECOVERY_ACTIVE.load(Ordering::Acquire)
    }

    unsafe extern "system" fn poster_window_subclass(
        hwnd: HWND,
        message: u32,
        wparam: WPARAM,
        lparam: LPARAM,
        subclass_id: usize,
        _reference_data: usize,
    ) -> LRESULT {
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
        if matches!(message, WM_DISPLAYCHANGE | WM_DPICHANGED | WM_SETTINGCHANGE) {
            queue_host_recovery();
        } else if message == WM_DESTROY {
            let _ = RemoveWindowSubclass(hwnd, Some(poster_window_subclass), subclass_id);
            POSTER_HWND.store(0, Ordering::Release);
        }
        DefSubclassProc(hwnd, message, wparam, lparam)
    }

    pub fn install_shell_foreground_monitor(window: &tauri::WebviewWindow) -> Result<(), String> {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        remember_poster_window(hwnd);
        if SHELL_RECOVERY_SENDER.get().is_some() {
            return Ok(());
        }

        let (sender, receiver) = sync_channel(1);
        SHELL_RECOVERY_SENDER
            .set(sender)
            .map_err(|_| "shell foreground monitor was already initialized".to_string())?;
        thread::Builder::new()
            .name("desktop-up-shell-recovery".to_string())
            .spawn(move || run_shell_recovery_worker(receiver))
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
                (current_style & !(WS_POPUP.0 | NON_CLIENT_DECORATION_STYLES)) | WS_CHILD.0;
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
                (current_style & !(WS_CHILD.0 | NON_CLIENT_DECORATION_STYLES)) | WS_POPUP.0;
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
        }
    }

    pub fn apply_interaction_region(
        window: &tauri::WebviewWindow,
        x: f64,
        y: f64,
        width: f64,
        height: f64,
        corner_radius: f64,
        device_scale_factor: f64,
    ) -> Result<(), String> {
        let hwnd = window.hwnd().map_err(|error| error.to_string())?;
        let scale = device_scale_factor.clamp(0.5, 4.0);
        let left = (x.max(0.0) * scale).round() as i32;
        let top = (y.max(0.0) * scale).round() as i32;
        let right = ((x + width.max(1.0)) * scale).round() as i32;
        let bottom = ((y + height.max(1.0)) * scale).round() as i32;
        let radius = (corner_radius.max(0.0) * scale).round() as i32;
        let region =
            unsafe { CreateRoundRectRgn(left, top, right + 1, bottom + 1, radius * 2, radius * 2) };
        if region.is_invalid() {
            return Err("failed to allocate the poster interaction region".to_string());
        }

        let result = unsafe { SetWindowRgn(hwnd, Some(region), true) };
        if result == 0 {
            unsafe {
                let _ = DeleteObject(HGDIOBJ(region.0));
            }
            return Err("failed to apply the poster interaction region".to_string());
        }
        // After a successful SetWindowRgn call, Windows owns the region handle.
        Ok(())
    }

    #[cfg(test)]
    mod tests {
        use super::{rect_contains, tool_window_extended_style};
        use windows::Win32::Foundation::RECT;
        use windows::Win32::UI::WindowsAndMessaging::{
            WS_EX_APPWINDOW, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
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
    }
}

#[cfg(target_os = "windows")]
pub use platform::{
    apply_interaction_region, capture_poster_window_placement, configure,
    install_shell_foreground_monitor, placement_recovery_in_progress, poster_work_area_size,
    restore_poster_placement, restore_poster_window_placement, set_poster_visibility_intent,
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
pub fn install_shell_foreground_monitor(_window: &tauri::WebviewWindow) -> Result<(), String> {
    Ok(())
}

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
pub fn restore_poster_window_placement(
    window: &tauri::Window,
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
pub fn apply_interaction_region(
    _window: &tauri::WebviewWindow,
    _x: f64,
    _y: f64,
    _width: f64,
    _height: f64,
    _corner_radius: f64,
    _device_scale_factor: f64,
) -> Result<(), String> {
    Ok(())
}
