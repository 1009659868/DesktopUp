use serde::Serialize;
use std::sync::{Arc, Mutex};

pub const TASKBAR_RECOVERY_CONTRACT_VERSION: u32 = 1;
pub const TASKBAR_RECOVERY_MAX_ATTEMPTS: u32 = 3;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum TaskbarRecoveryPhase {
    Initializing,
    Ready,
    Recovering,
    Degraded,
    Removed,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TaskbarRecoverySnapshot {
    pub contract_version: u32,
    pub phase: TaskbarRecoveryPhase,
    pub listener_installed: bool,
    pub generation: u64,
    pub event_count: u64,
    pub coalesced_event_count: u64,
    pub recovery_count: u64,
    pub attempt_count: u64,
    pub consecutive_failures: u32,
    pub max_attempts: u32,
    pub pending_event_count: u64,
    pub ignored_after_exit_count: u64,
    pub last_trigger: String,
    pub last_outcome: String,
    pub last_error: Option<String>,
}

impl Default for TaskbarRecoverySnapshot {
    fn default() -> Self {
        Self {
            contract_version: TASKBAR_RECOVERY_CONTRACT_VERSION,
            phase: TaskbarRecoveryPhase::Initializing,
            listener_installed: false,
            generation: 0,
            event_count: 0,
            coalesced_event_count: 0,
            recovery_count: 0,
            attempt_count: 0,
            consecutive_failures: 0,
            max_attempts: TASKBAR_RECOVERY_MAX_ATTEMPTS,
            pending_event_count: 0,
            ignored_after_exit_count: 0,
            last_trigger: "initialState".to_string(),
            last_outcome: "pending".to_string(),
            last_error: None,
        }
    }
}

#[derive(Default)]
struct TaskbarRecoveryState {
    snapshot: TaskbarRecoverySnapshot,
    worker_active: bool,
}

#[derive(Clone, Default)]
pub struct TaskbarRecoveryLifecycle {
    inner: Arc<Mutex<TaskbarRecoveryState>>,
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct TaskbarRecoveryBatch {
    pub generation: u64,
    pub event_count: u64,
}

impl TaskbarRecoveryLifecycle {
    pub fn snapshot(&self) -> Result<TaskbarRecoverySnapshot, String> {
        self.inner
            .lock()
            .map(|state| state.snapshot.clone())
            .map_err(|error| error.to_string())
    }

    pub fn mark_listener_installed(&self) -> Result<TaskbarRecoverySnapshot, String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        if state.snapshot.phase != TaskbarRecoveryPhase::Removed {
            state.snapshot.phase = TaskbarRecoveryPhase::Ready;
            state.snapshot.listener_installed = true;
            state.snapshot.last_outcome = "listenerReady".to_string();
            state.snapshot.last_error = None;
        }
        Ok(state.snapshot.clone())
    }

    pub fn mark_listener_failed(
        &self,
        error: impl Into<String>,
    ) -> Result<TaskbarRecoverySnapshot, String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        if state.snapshot.phase != TaskbarRecoveryPhase::Removed {
            state.snapshot.phase = TaskbarRecoveryPhase::Degraded;
            state.snapshot.listener_installed = false;
            state.snapshot.last_trigger = "listenerInstall".to_string();
            state.snapshot.last_outcome = "listenerFailed".to_string();
            state.snapshot.last_error = Some(error.into());
        }
        Ok(state.snapshot.clone())
    }

    pub fn record_taskbar_created(&self) -> Result<bool, String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        state.snapshot.event_count = state.snapshot.event_count.saturating_add(1);
        state.snapshot.last_trigger = "taskbarCreated".to_string();
        if state.snapshot.phase == TaskbarRecoveryPhase::Removed {
            state.snapshot.ignored_after_exit_count =
                state.snapshot.ignored_after_exit_count.saturating_add(1);
            state.snapshot.last_outcome = "ignoredAfterExit".to_string();
            return Ok(false);
        }

        state.snapshot.pending_event_count = state.snapshot.pending_event_count.saturating_add(1);
        if state.worker_active {
            state.snapshot.coalesced_event_count =
                state.snapshot.coalesced_event_count.saturating_add(1);
            state.snapshot.last_outcome = "coalesced".to_string();
            return Ok(true);
        }

        state.worker_active = true;
        state.snapshot.last_outcome = "queued".to_string();
        Ok(true)
    }

    pub fn take_pending_batch(&self) -> Result<Option<TaskbarRecoveryBatch>, String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        if state.snapshot.phase == TaskbarRecoveryPhase::Removed
            || state.snapshot.pending_event_count == 0
        {
            return Ok(None);
        }
        state.snapshot.generation = state.snapshot.generation.saturating_add(1);
        state.snapshot.recovery_count = state.snapshot.recovery_count.saturating_add(1);
        state.snapshot.phase = TaskbarRecoveryPhase::Recovering;
        state.snapshot.last_outcome = "recovering".to_string();
        state.snapshot.last_error = None;
        let batch = TaskbarRecoveryBatch {
            generation: state.snapshot.generation,
            event_count: state.snapshot.pending_event_count,
        };
        state.snapshot.pending_event_count = 0;
        Ok(Some(batch))
    }

    pub fn record_attempt(&self) -> Result<(), String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        state.snapshot.attempt_count = state.snapshot.attempt_count.saturating_add(1);
        Ok(())
    }

    pub fn finish_batch(&self, result: &Result<(), String>) -> Result<(), String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        if state.snapshot.phase == TaskbarRecoveryPhase::Removed {
            return Ok(());
        }
        match result {
            Ok(()) => {
                state.snapshot.phase = TaskbarRecoveryPhase::Ready;
                state.snapshot.consecutive_failures = 0;
                state.snapshot.last_outcome = "recovered".to_string();
                state.snapshot.last_error = None;
            }
            Err(error) => {
                state.snapshot.phase = TaskbarRecoveryPhase::Degraded;
                state.snapshot.consecutive_failures =
                    state.snapshot.consecutive_failures.saturating_add(1);
                state.snapshot.last_outcome = "degraded".to_string();
                state.snapshot.last_error = Some(error.clone());
            }
        }
        Ok(())
    }

    pub fn finish_worker_if_idle(&self) -> Result<bool, String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        if state.snapshot.phase == TaskbarRecoveryPhase::Removed
            || state.snapshot.pending_event_count == 0
        {
            state.worker_active = false;
            return Ok(true);
        }
        Ok(false)
    }

    pub fn record_manual_reconnect(&self, result: &Result<(), String>) -> Result<(), String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        if state.snapshot.phase == TaskbarRecoveryPhase::Removed {
            return Ok(());
        }
        state.snapshot.last_trigger = "manualReconnect".to_string();
        match result {
            Ok(()) => {
                state.snapshot.phase = TaskbarRecoveryPhase::Ready;
                state.snapshot.consecutive_failures = 0;
                state.snapshot.last_outcome = "manualRecovered".to_string();
                state.snapshot.last_error = None;
            }
            Err(error) => {
                state.snapshot.phase = TaskbarRecoveryPhase::Degraded;
                state.snapshot.last_outcome = "manualRecoveryFailed".to_string();
                state.snapshot.last_error = Some(error.clone());
            }
        }
        Ok(())
    }

    pub fn begin_exit(&self) -> Result<TaskbarRecoverySnapshot, String> {
        let mut state = self.inner.lock().map_err(|error| error.to_string())?;
        state.snapshot.phase = TaskbarRecoveryPhase::Removed;
        state.snapshot.listener_installed = false;
        state.snapshot.pending_event_count = 0;
        state.snapshot.last_trigger = "runtimeExit".to_string();
        state.snapshot.last_outcome = "removed".to_string();
        state.worker_active = false;
        Ok(state.snapshot.clone())
    }

    pub fn is_exiting(&self) -> bool {
        self.inner
            .lock()
            .map(|state| state.snapshot.phase == TaskbarRecoveryPhase::Removed)
            .unwrap_or(true)
    }
}

#[cfg(target_os = "windows")]
mod platform {
    use super::{TaskbarRecoveryBatch, TaskbarRecoveryLifecycle, TASKBAR_RECOVERY_MAX_ATTEMPTS};
    use std::{
        sync::{
            atomic::{AtomicIsize, Ordering},
            mpsc::{sync_channel, Receiver, SyncSender},
            Arc, OnceLock,
        },
        thread,
        time::Duration,
    };
    use windows::{
        core::{w, BOOL},
        Win32::{
            Foundation::{HWND, LPARAM, LRESULT, WPARAM},
            System::Threading::GetCurrentProcessId,
            UI::{
                Shell::{DefSubclassProc, RemoveWindowSubclass, SetWindowSubclass},
                WindowsAndMessaging::{
                    EnumWindows, GetClassNameW, GetWindowThreadProcessId, RegisterWindowMessageW,
                    WM_NCDESTROY,
                },
            },
        },
    };

    const TRAY_WINDOW_CLASS: &str = "tray_icon_app";
    const TASKBAR_SUBCLASS_ID: usize = 0x4455_5035;
    const TASKBAR_DEBOUNCE_MS: u64 = 180;
    const TASKBAR_RETRY_DELAYS_MS: [u64; 3] = [0, 250, 750];

    static TASKBAR_MESSAGE: OnceLock<u32> = OnceLock::new();
    static TASKBAR_SENDER: OnceLock<SyncSender<()>> = OnceLock::new();
    static TASKBAR_LIFECYCLE: OnceLock<TaskbarRecoveryLifecycle> = OnceLock::new();
    static TASKBAR_HWND: AtomicIsize = AtomicIsize::new(0);

    type RecoveryHandler =
        Arc<dyn Fn(TaskbarRecoveryBatch) -> Result<(), String> + Send + Sync + 'static>;
    type StatusHandler = Arc<dyn Fn() + Send + Sync + 'static>;

    unsafe extern "system" fn enumerate_tray_window(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let mut process_id = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut process_id));
        if process_id != GetCurrentProcessId() {
            return BOOL(1);
        }
        let mut class_name = [0u16; 128];
        let length = GetClassNameW(hwnd, &mut class_name);
        let class_name = String::from_utf16_lossy(&class_name[..length.max(0) as usize]);
        if class_name == TRAY_WINDOW_CLASS {
            let found = &mut *(lparam.0 as *mut isize);
            *found = hwnd.0 as isize;
            return BOOL(0);
        }
        BOOL(1)
    }

    fn find_tray_window() -> Result<HWND, String> {
        let mut found = 0isize;
        unsafe {
            let _ = EnumWindows(
                Some(enumerate_tray_window),
                LPARAM((&mut found as *mut isize) as isize),
            );
        }
        if found == 0 {
            Err("the Tauri tray message window is unavailable".to_string())
        } else {
            Ok(HWND(found as *mut core::ffi::c_void))
        }
    }

    fn attach_listener_to_tray_window(hwnd: HWND) -> Result<(), String> {
        let installed = unsafe {
            SetWindowSubclass(hwnd, Some(taskbar_window_subclass), TASKBAR_SUBCLASS_ID, 0)
        };
        if !installed.as_bool() {
            return Err("failed to subclass the Tauri tray message window".to_string());
        }
        TASKBAR_HWND.store(hwnd.0 as isize, Ordering::Release);
        Ok(())
    }
    unsafe extern "system" fn taskbar_window_subclass(
        hwnd: HWND,
        message: u32,
        wparam: WPARAM,
        lparam: LPARAM,
        subclass_id: usize,
        _data: usize,
    ) -> LRESULT {
        if TASKBAR_MESSAGE
            .get()
            .is_some_and(|registered| *registered == message)
        {
            if let Some(lifecycle) = TASKBAR_LIFECYCLE.get() {
                if lifecycle.record_taskbar_created().unwrap_or(false) {
                    if let Some(sender) = TASKBAR_SENDER.get() {
                        let _ = sender.try_send(());
                    }
                }
            }
        }
        if message == WM_NCDESTROY {
            let _ = RemoveWindowSubclass(hwnd, Some(taskbar_window_subclass), subclass_id);
            TASKBAR_HWND.store(0, Ordering::Release);
        }
        DefSubclassProc(hwnd, message, wparam, lparam)
    }

    fn run_recovery_worker(
        receiver: Receiver<()>,
        lifecycle: TaskbarRecoveryLifecycle,
        handler: RecoveryHandler,
        status_handler: StatusHandler,
    ) {
        while receiver.recv().is_ok() {
            thread::sleep(Duration::from_millis(TASKBAR_DEBOUNCE_MS));
            while receiver.try_recv().is_ok() {}
            loop {
                if lifecycle.is_exiting() {
                    return;
                }
                let Ok(Some(batch)) = lifecycle.take_pending_batch() else {
                    let _ = lifecycle.finish_worker_if_idle();
                    break;
                };
                let mut result = Err("TaskbarCreated recovery did not run".to_string());
                for (attempt, delay_ms) in TASKBAR_RETRY_DELAYS_MS.iter().copied().enumerate() {
                    if lifecycle.is_exiting() {
                        return;
                    }
                    if delay_ms > 0 {
                        thread::sleep(Duration::from_millis(delay_ms));
                    }
                    let _ = lifecycle.record_attempt();
                    result = handler(batch.clone());
                    if result.is_ok() || attempt + 1 >= TASKBAR_RECOVERY_MAX_ATTEMPTS as usize {
                        break;
                    }
                }
                let _ = lifecycle.finish_batch(&result);
                status_handler();
                if lifecycle.finish_worker_if_idle().unwrap_or(true) {
                    break;
                }
                thread::sleep(Duration::from_millis(TASKBAR_DEBOUNCE_MS));
                while receiver.try_recv().is_ok() {}
            }
        }
    }

    pub fn install<F, G>(
        lifecycle: TaskbarRecoveryLifecycle,
        recovery_handler: F,
        status_handler: G,
    ) -> Result<(), String>
    where
        F: Fn(TaskbarRecoveryBatch) -> Result<(), String> + Send + Sync + 'static,
        G: Fn() + Send + Sync + 'static,
    {
        let hwnd = find_tray_window()?;
        let taskbar_message = unsafe { RegisterWindowMessageW(w!("TaskbarCreated")) };
        if taskbar_message == 0 {
            return Err("failed to register the TaskbarCreated message".to_string());
        }
        TASKBAR_MESSAGE
            .set(taskbar_message)
            .map_err(|_| "TaskbarCreated message was already registered".to_string())?;
        TASKBAR_LIFECYCLE
            .set(lifecycle.clone())
            .map_err(|_| "TaskbarCreated lifecycle was already installed".to_string())?;
        let (sender, receiver) = sync_channel(1);
        TASKBAR_SENDER
            .set(sender)
            .map_err(|_| "TaskbarCreated listener was already installed".to_string())?;
        let handler: RecoveryHandler = Arc::new(recovery_handler);
        let status_handler: StatusHandler = Arc::new(status_handler);
        thread::Builder::new()
            .name("desktop-up-taskbar-recovery".to_string())
            .spawn({
                let lifecycle = lifecycle.clone();
                move || run_recovery_worker(receiver, lifecycle, handler, status_handler)
            })
            .map_err(|error| error.to_string())?;
        attach_listener_to_tray_window(hwnd)?;
        lifecycle.mark_listener_installed()?;
        Ok(())
    }

    pub fn reattach_listener() -> Result<(), String> {
        let lifecycle = TASKBAR_LIFECYCLE
            .get()
            .ok_or_else(|| "TaskbarCreated lifecycle is unavailable".to_string())?;
        let previous = TASKBAR_HWND.swap(0, Ordering::AcqRel);
        if previous != 0 {
            let hwnd = HWND(previous as *mut core::ffi::c_void);
            unsafe {
                let _ =
                    RemoveWindowSubclass(hwnd, Some(taskbar_window_subclass), TASKBAR_SUBCLASS_ID);
            }
        }
        attach_listener_to_tray_window(find_tray_window()?)?;
        lifecycle.mark_listener_installed()?;
        Ok(())
    }
    pub fn begin_exit(lifecycle: &TaskbarRecoveryLifecycle) {
        let _ = lifecycle.begin_exit();
        let raw = TASKBAR_HWND.swap(0, Ordering::AcqRel);
        if raw != 0 {
            let hwnd = HWND(raw as *mut core::ffi::c_void);
            unsafe {
                let _ =
                    RemoveWindowSubclass(hwnd, Some(taskbar_window_subclass), TASKBAR_SUBCLASS_ID);
            }
        }
        if let Some(sender) = TASKBAR_SENDER.get() {
            let _ = sender.try_send(());
        }
    }
}

#[cfg(target_os = "windows")]
pub use platform::{begin_exit, install, reattach_listener};

#[cfg(not(target_os = "windows"))]
pub fn install<F, G>(
    lifecycle: TaskbarRecoveryLifecycle,
    _recovery_handler: F,
    _status_handler: G,
) -> Result<(), String>
where
    F: Fn(TaskbarRecoveryBatch) -> Result<(), String> + Send + Sync + 'static,
    G: Fn() + Send + Sync + 'static,
{
    lifecycle.mark_listener_installed()?;
    Ok(())
}

#[cfg(not(target_os = "windows"))]
pub fn begin_exit(lifecycle: &TaskbarRecoveryLifecycle) {
    let _ = lifecycle.begin_exit();
}

#[cfg(not(target_os = "windows"))]
pub fn reattach_listener() -> Result<(), String> {
    Ok(())
}
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn listener_failure_is_diagnostic_and_recoverable() {
        let lifecycle = TaskbarRecoveryLifecycle::default();
        let failed = lifecycle
            .mark_listener_failed("tray message window unavailable")
            .unwrap();
        assert_eq!(failed.phase, TaskbarRecoveryPhase::Degraded);
        assert!(!failed.listener_installed);
        assert_eq!(failed.last_outcome, "listenerFailed");
        assert_eq!(
            failed.last_error.as_deref(),
            Some("tray message window unavailable")
        );

        let ready = lifecycle.mark_listener_installed().unwrap();
        assert_eq!(ready.phase, TaskbarRecoveryPhase::Ready);
        assert!(ready.listener_installed);
        assert!(ready.last_error.is_none());
    }

    #[test]
    fn duplicate_taskbar_events_are_coalesced_into_one_batch() {
        let lifecycle = TaskbarRecoveryLifecycle::default();
        lifecycle.mark_listener_installed().unwrap();
        assert!(lifecycle.record_taskbar_created().unwrap());
        assert!(lifecycle.record_taskbar_created().unwrap());
        let batch = lifecycle.take_pending_batch().unwrap().unwrap();
        assert_eq!(batch.generation, 1);
        assert_eq!(batch.event_count, 2);
        let snapshot = lifecycle.snapshot().unwrap();
        assert_eq!(snapshot.coalesced_event_count, 1);
        assert_eq!(snapshot.phase, TaskbarRecoveryPhase::Recovering);
    }

    #[test]
    fn successful_recovery_returns_to_ready() {
        let lifecycle = TaskbarRecoveryLifecycle::default();
        lifecycle.mark_listener_installed().unwrap();
        lifecycle.record_taskbar_created().unwrap();
        lifecycle.take_pending_batch().unwrap();
        lifecycle.record_attempt().unwrap();
        lifecycle.finish_batch(&Ok(())).unwrap();
        assert_eq!(
            lifecycle.snapshot().unwrap().phase,
            TaskbarRecoveryPhase::Ready
        );
        assert_eq!(lifecycle.snapshot().unwrap().last_outcome, "recovered");
    }

    #[test]
    fn bounded_failure_is_degraded_and_manual_reconnect_can_clear_it() {
        let lifecycle = TaskbarRecoveryLifecycle::default();
        lifecycle.mark_listener_installed().unwrap();
        lifecycle.record_taskbar_created().unwrap();
        lifecycle.take_pending_batch().unwrap();
        lifecycle
            .finish_batch(&Err("Explorer is unavailable".to_string()))
            .unwrap();
        assert_eq!(
            lifecycle.snapshot().unwrap().phase,
            TaskbarRecoveryPhase::Degraded
        );
        lifecycle.record_manual_reconnect(&Ok(())).unwrap();
        assert_eq!(
            lifecycle.snapshot().unwrap().phase,
            TaskbarRecoveryPhase::Ready
        );
        assert_eq!(
            lifecycle.snapshot().unwrap().last_outcome,
            "manualRecovered"
        );
    }

    #[test]
    fn exit_is_sticky_and_late_taskbar_events_are_ignored() {
        let lifecycle = TaskbarRecoveryLifecycle::default();
        lifecycle.mark_listener_installed().unwrap();
        lifecycle.begin_exit().unwrap();
        assert!(!lifecycle.record_taskbar_created().unwrap());
        let snapshot = lifecycle.snapshot().unwrap();
        assert_eq!(snapshot.phase, TaskbarRecoveryPhase::Removed);
        assert_eq!(snapshot.ignored_after_exit_count, 1);
        assert_eq!(snapshot.pending_event_count, 0);
    }
}
