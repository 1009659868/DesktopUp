// DesktopUp V2 核心真身(C-V2P1-02 骨架)
// 单实例 + 状态权威 + 结构化诊断 + 渲染侧车生命周期 + 穿透钩子(原型机制移植)
mod diag;
mod proto;
mod singleton;
mod state;

use std::io::Read;
use std::path::PathBuf;
use std::process::{Child, Command as ProcCommand, Stdio};
use std::sync::atomic::{AtomicBool, AtomicU32, Ordering};
use std::sync::{Mutex, OnceLock};
use std::time::Duration;

use serde_json::json;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::{
    CallNextHookEx, DispatchMessageW, GetMessageW, GetWindowLongPtrW, SetWindowLongPtrW,
    SetWindowsHookExW, TranslateMessage, UnhookWindowsHookEx, GWL_EXSTYLE, MSG,
    MSLLHOOKSTRUCT, WH_MOUSE_LL, WM_MOUSEMOVE, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
    WS_EX_TRANSPARENT,
};

use diag::Diag;
use singleton::SingleInstance;
use state::SurfaceState;

static DIAG: OnceLock<Diag> = OnceLock::new();

fn log(level: &str, event: &str, fields: serde_json::Value) {
    if let Some(d) = DIAG.get() {
        d.log(level, event, fields);
    }
}

// ---- 渲染表面(命中区域来自渲染端声明)----
struct Surface {
    hwnd: isize,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
    regions: Vec<(i32, i32, i32, i32)>,
}
static SURFACE: OnceLock<Mutex<Option<Surface>>> = OnceLock::new();
static PASSTHROUGH_ON: AtomicBool = AtomicBool::new(false);
static HOOK_STARTED: OnceLock<()> = OnceLock::new();
static HOOK_EVENTS: AtomicU32 = AtomicU32::new(0);

fn hit_any_region(s: &Surface, px: i32, py: i32) -> bool {
    let lx = px - s.x;
    let ly = py - s.y;
    s.regions
        .iter()
        .any(|(rx, ry, rw, rh)| lx >= *rx && lx < rx + rw && ly >= *ry && ly < ry + rh)
}

fn apply_passthrough(hwnd: isize, passthrough: bool) {
    if PASSTHROUGH_ON.swap(passthrough, Ordering::SeqCst) == passthrough {
        return;
    }
    static TOGGLES: AtomicU32 = AtomicU32::new(0);
    let n = TOGGLES.fetch_add(1, Ordering::Relaxed) + 1;
    if n % 20 == 1 {
        log("info", "passthrough.toggled", json!({ "passthrough": passthrough, "toggle": n }));
    }
    unsafe {
        let hwnd = HWND(hwnd as *mut core::ffi::c_void);
        let style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        let next = if passthrough {
            style | WS_EX_TRANSPARENT.0 as isize
        } else {
            style & !(WS_EX_TRANSPARENT.0 as isize)
        };
        let next = next | WS_EX_TOOLWINDOW.0 as isize | WS_EX_NOACTIVATE.0 as isize;
        let _ = SetWindowLongPtrW(hwnd, GWL_EXSTYLE, next);
    }
}

unsafe extern "system" fn mouse_hook(code: i32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    let n = HOOK_EVENTS.fetch_add(1, Ordering::Relaxed) + 1;
    if n <= 8 {
        let pt = (lparam.0 as *const MSLLHOOKSTRUCT)
            .as_ref()
            .map(|d| (d.pt.x, d.pt.y))
            .unwrap_or((-1, -1));
        log(
            "info",
            "hook.event",
            json!({ "n": n, "code": code, "wparam": wparam.0, "pt": [pt.0, pt.1] }),
        );
    }
    if code >= 0 && wparam.0 as u32 == WM_MOUSEMOVE {
        if let Some(details) = (lparam.0 as *const MSLLHOOKSTRUCT).as_ref() {
            let point = details.pt;
            if let Ok(guard) = SURFACE.get_or_init(|| Mutex::new(None)).lock() {
                if let Some(s) = guard.as_ref() {
                    let inside = point.x >= s.x
                        && point.x < s.x + s.w
                        && point.y >= s.y
                        && point.y < s.y + s.h;
                    if inside {
                        apply_passthrough(s.hwnd, !hit_any_region(s, point.x, point.y));
                    } else {
                        apply_passthrough(s.hwnd, true);
                    }
                }
            }
        }
    }
    CallNextHookEx(None, code, wparam, lparam)
}

fn run_hook_thread() {
    unsafe {
        let Ok(hook) = SetWindowsHookExW(WH_MOUSE_LL, Some(mouse_hook), None, 0) else {
            log("error", "hook.install_failed", json!({}));
            return;
        };
        log("info", "hook.installed", json!({ "kind": "WH_MOUSE_LL" }));
        let _ = HOOK_STARTED.set(());
        let mut message = MSG::default();
        while GetMessageW(&mut message, None, 0, 0).as_bool() {
            let _ = TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        let _ = UnhookWindowsHookEx(hook);
    }
}

fn register_surface(value: &serde_json::Value, state: &mut SurfaceState) {
    let Some(hwnd) = value.get("hwnd").and_then(|v| v.as_f64()).map(|f| f as u64) else {
        log("error", "hello.missing_hwnd", json!({}));
        return;
    };
    let num = |obj: &serde_json::Value, key: &str| -> i32 {
        obj.get(key)
            .and_then(|v| v.as_f64())
            .map(|f| f as i32)
            .unwrap_or(0)
    };
    let rect = value.get("rect");
    let x = rect.map(|r| num(r, "x")).unwrap_or(0);
    let y = rect.map(|r| num(r, "y")).unwrap_or(0);
    let w = rect.map(|r| num(r, "w")).unwrap_or(0);
    let h = rect.map(|r| num(r, "h")).unwrap_or(0);
    let mut regions = Vec::new();
    if let Some(list) = value.get("regions").and_then(|v| v.as_array()) {
        for r in list {
            regions.push((num(r, "x"), num(r, "y"), num(r, "w"), num(r, "h")));
        }
    }
    // 渲染端声明即状态:hello 视为修订 1 的初始投影(状态权威在核心)
    state.geometry = (x, y, w, h);
    state.revision = state.revision.max(1);
    let region_count = regions.len();
    if let Ok(mut guard) = SURFACE.get_or_init(|| Mutex::new(None)).lock() {
        *guard = Some(Surface {
            hwnd: hwnd as isize,
            x,
            y,
            w,
            h,
            regions,
        });
    }
    log(
        "info",
        "surface.registered",
        json!({ "hwnd": hwnd, "rect": [x, y, w, h], "regions": region_count, "revision": state.revision }),
    );
    HOOK_STARTED.get_or_init(|| {
        std::thread::spawn(run_hook_thread);
    });
}

fn spawn_renderer(exe: &str) -> Option<Child> {
    ProcCommand::new(exe)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .spawn()
        .ok()
}

struct Session {
    stdin: std::process::ChildStdin,
    stdout: std::process::ChildStdout,
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let renderer = args
        .iter()
        .skip(1)
        .find(|a| !a.starts_with("--"))
        .cloned()
        .unwrap_or_else(|| {
            r"G:\A_\Tools\DesktopUp_V2\prototype\qt-quick\renderer\build\poster-renderer.exe"
                .to_string()
        });
    let log_dir: PathBuf = args
        .iter()
        .position(|a| a == "--log-dir")
        .and_then(|i| args.get(i + 1))
        .map(PathBuf::from)
        .unwrap_or_else(|| {
            let mut p = std::env::var_os("LOCALAPPDATA")
                .map(PathBuf::from)
                .unwrap_or_else(|| PathBuf::from("."));
            p.push("DesktopUp");
            p.push("logs");
            p
        });

    let _ = DIAG.set(Diag::open(&log_dir));

    // 单实例:二次启动归并退出
    let Some(_singleton) = SingleInstance::acquire("Local\\DesktopUp.V2.Core.Singleton") else {
        log("info", "lifecycle.second_instance_exit", json!({}));
        return;
    };
    log("info", "lifecycle.started", json!({ "renderer": renderer, "log_dir": log_dir.to_string_lossy() }));

    let mut state = SurfaceState::default();
    let mut restarts = 0u32;
    loop {
        let Some(mut child) = spawn_renderer(&renderer) else {
            log("error", "renderer.spawn_failed", json!({ "path": renderer }));
            return;
        };
        let stdin = child.stdin.take().expect("stdin");
        let stdout = child.stdout.take().expect("stdout");
        log("info", "session.started", json!({ "pid": child.id() }));

        // 上行读线程(帧解析)
        let (tx, rx) = std::sync::mpsc::channel::<Option<serde_json::Value>>();
        std::thread::spawn(move || {
            let mut reader = proto::FrameReader::new(stdout);
            loop {
                let Some(value) = reader.next() else {
                    let _ = tx.send(None);
                    return;
                };
                if tx.send(Some(value)).is_err() {
                    return;
                }
            }
        });

        let mut stdin = stdin;
        let mut next_id: u64 = 1;
        let mut ticks: u64 = 0;
        let mut exited = false;
        while !exited {
            ticks += 1;
            std::thread::sleep(Duration::from_millis(30));
            if ticks % 100 == 0 {
                let id = next_id;
                next_id += 1;
                let _ = proto::send_frame(&mut stdin, &json!({"cmd": "get_health", "id": id}));
            }
            if ticks % 300 == 150 {
                let id = next_id;
                next_id += 1;
                let _ = proto::send_frame(&mut stdin, &json!({"cmd": "capture", "id": id}));
            }
            loop {
                match rx.try_recv() {
                    Ok(Some(value)) => {
                        if let Some(id) = value.get("id").and_then(|v| v.as_u64()) {
                            if value.get("frames").is_some() {
                                log(
                                    "info",
                                    "health",
                                    json!({ "id": id, "frames": value.get("frames"), "hook_events": HOOK_EVENTS.load(Ordering::Relaxed), "geometry": value.get("geometry") }),
                                );
                            } else if let Some(m) = value.get("magenta").and_then(|v| v.as_f64()) {
                                let verdict = if m > 0.0 { "pass" } else { "fail" };
                                log(
                                    if m > 0.0 { "info" } else { "warn" },
                                    "selfcheck.capture",
                                    json!({ "id": id, "magenta": m, "verdict": verdict }),
                                );
                            }
                            continue;
                        }
                        match value.get("cmd").and_then(|v| v.as_str()) {
                            Some("hello") => register_surface(&value, &mut state),
                            Some("bye") => exited = true,
                            other => log("warn", "frame.unhandled", json!({ "cmd": other })),
                        }
                    }
                    Ok(None) | Err(std::sync::mpsc::TryRecvError::Disconnected) => {
                        exited = true;
                    }
                    Err(std::sync::mpsc::TryRecvError::Empty) => break,
                }
            }
        }

        let code = child.try_wait().ok().flatten();
        log("info", "renderer.exited", json!({ "code": code.map(|c| c.to_string()), "restarts": restarts }));
        if restarts >= 3 {
            log("error", "lifecycle.restart_limit", json!({ "max": 3 }));
            return;
        }
        restarts += 1;
        log("info", "lifecycle.restarting", json!({ "attempt": restarts, "max": 3 }));
    }
}
