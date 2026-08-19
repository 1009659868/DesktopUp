// DesktopUp V2 核心真身(C-V2P1-02 骨架)
// 单实例 + 状态权威 + 结构化诊断 + 渲染侧车生命周期 + 穿透钩子(原型机制移植)
// 控制通道:命名管道(apply 命令带 base_rev,防回弹)
mod diag;
mod pipe;
mod proto;
mod singleton;
mod state;
mod tray;

use std::path::PathBuf;
use std::process::{Child, Command as ProcCommand, Stdio};
use std::sync::atomic::{AtomicBool, AtomicI32, AtomicU32, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::Duration;

use serde_json::json;
use windows::core::w;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::{
    CallNextHookEx, DispatchMessageW, GetMessageW, GetWindowLongPtrW, RegisterWindowMessageW,
    SetWindowLongPtrW, SetWindowsHookExW, TranslateMessage, UnhookWindowsHookEx, GWL_EXSTYLE,
    MSG, MSLLHOOKSTRUCT, WH_MOUSE_LL, WM_MOUSEMOVE, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
    WS_EX_TRANSPARENT,
};

use diag::Diag;
use singleton::SingleInstance;
use state::{Command, SurfaceState};
use windows::Win32::UI::HiDpi::GetDpiForWindow;

static DIAG: OnceLock<Diag> = OnceLock::new();

fn log(level: &str, event: &str, fields: serde_json::Value) {
    if let Some(d) = DIAG.get() {
        d.log(level, event, fields);
    }
}

// ---- 渲染表面(命中区域来自渲染端声明)----
// 坐标约定:hello/health 中的 rect 与 regions 是 Qt 逻辑坐标;
// WH_MOUSE_LL 的 pt 是物理屏幕像素。scale = GetDpiForWindow/96 用于换算。
struct Region {
    id: String,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
}
struct Surface {
    hwnd: isize,
    scale: f64,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
    regions: Vec<Region>, // 逻辑窗口坐标
}
static SURFACE: OnceLock<Mutex<Option<Surface>>> = OnceLock::new();
static PASSTHROUGH_ON: AtomicBool = AtomicBool::new(false);
static HOOK_STARTED: OnceLock<()> = OnceLock::new();
static HOOK_EVENTS: AtomicU32 = AtomicU32::new(0);

fn phys(s: &Surface, v: i32) -> i32 {
    (v as f64 * s.scale).round() as i32
}

fn apply_passthrough(hwnd: isize, passthrough: bool) {
    if PASSTHROUGH_ON.swap(passthrough, Ordering::SeqCst) == passthrough {
        return;
    }
    static TOGGLES: AtomicU32 = AtomicU32::new(0);
    let n = TOGGLES.fetch_add(1, Ordering::Relaxed) + 1;
    // 前 6 次全记 + 之后每 10 次记一次(两个方向都要有证据;n%20==1 会数学上漏掉交替方向)
    if n <= 6 || n % 10 == 0 {
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
                    // 物理 pt 与物理窗口矩形比较(scale 换算)
                    let inside = point.x >= phys(s, s.x)
                        && point.x < phys(s, s.x + s.w)
                        && point.y >= phys(s, s.y)
                        && point.y < phys(s, s.y + s.h);
                    static LAST_INSIDE: AtomicBool = AtomicBool::new(false);
                    if LAST_INSIDE.swap(inside, Ordering::Relaxed) != inside {
                        log(
                            "info",
                            "hover.inside_changed",
                            json!({ "inside": inside, "pt": [point.x, point.y], "surface_logical": [s.x, s.y, s.w, s.h], "scale": s.scale }),
                        );
                    }
                    if inside {
                        // 区域级命中:命中返回区域 id;空白 → None
                        let sx = (point.x as f64 / s.scale).round() as i32;
                        let sy = (point.y as f64 / s.scale).round() as i32;
                        let lx = sx - s.x;
                        let ly = sy - s.y;
                        let hit = s.regions.iter().find(|r| {
                            lx >= r.x && lx < r.x + r.w && ly >= r.y && ly < r.y + r.h
                        });
                        let region_id = hit.map(|r| r.id.clone());
                        static LAST_HIT: OnceLock<Mutex<Option<String>>> = OnceLock::new();
                        static HIT_LOGS: AtomicU32 = AtomicU32::new(0);
                        let prev = LAST_HIT
                            .get_or_init(|| Mutex::new(None))
                            .lock()
                            .map(|mut g| std::mem::replace(&mut *g, region_id.clone()))
                            .unwrap_or(None);
                        if prev != region_id {
                            let n = HIT_LOGS.fetch_add(1, Ordering::Relaxed) + 1;
                            if n <= 3 || n % 20 == 1 {
                                log(
                                    "info",
                                    "hover.region_hit",
                                    json!({ "region": region_id, "pt": [point.x, point.y], "n": n }),
                                );
                            }
                        }
                        apply_passthrough(s.hwnd, hit.is_none());
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
        // 托盘:由本线程消息泵驱动(消息队列归安装线程)
        match tray::init() {
            Ok(()) => log("info", "tray.added", json!({})),
            Err(e) => log("error", "tray.add_failed", json!({ "error": e })),
        }
        // Explorer 重启广播:托盘图标需自行重注册
        let taskbar_created = unsafe { RegisterWindowMessageW(w!("TaskbarCreated")) };
        let mut message = MSG::default();
        while GetMessageW(&mut message, None, 0, 0).as_bool() {
            if tray::pump(&message) {
                continue;
            }
            if message.message == taskbar_created {
                match tray::reinit() {
                    Ok(()) => log("info", "tray.re_added", json!({})),
                    Err(e) => log("error", "tray.re_add_failed", json!({ "error": e })),
                }
                continue;
            }
            let _ = TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        let _ = UnhookWindowsHookEx(hook);
    }
}

fn register_surface(value: &serde_json::Value, state: &Mutex<SurfaceState>) {
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
    let mut regions = Vec::new();
    if let Some(list) = value.get("regions").and_then(|v| v.as_array()) {
        for (i, r) in list.iter().enumerate() {
            regions.push(Region {
                id: r.get("id")
                    .and_then(|v| v.as_str())
                    .map(String::from)
                    .unwrap_or_else(|| format!("region:{i}")),
                x: num(r, "x"),
                y: num(r, "y"),
                w: num(r, "w"),
                h: num(r, "h"),
            });
        }
    }
    // 状态权威在核心:hello 只作句柄/能力声明;几何以核心状态为准(hello_ack 下发)
    let (revision, geometry) = {
        let mut s = state.lock().expect("state lock");
        s.revision = s.revision.max(1);
        (s.revision, s.geometry)
    };
    let region_count = regions.len();
    // DPI:窗口所在显示器的真实缩放(物理光标坐标与 Qt 逻辑坐标的换算因子)
    let dpi = unsafe { GetDpiForWindow(HWND(hwnd as *mut core::ffi::c_void)) };
    let scale = dpi as f64 / 96.0;
    if let Ok(mut guard) = SURFACE.get_or_init(|| Mutex::new(None)).lock() {
        *guard = Some(Surface {
            hwnd: hwnd as isize,
            scale,
            x: geometry.0,
            y: geometry.1,
            w: geometry.2,
            h: geometry.3,
            regions,
        });
    }
    log(
        "info",
        "surface.registered",
        json!({ "hwnd": hwnd, "geometry": geometry, "regions": region_count, "revision": revision, "dpi": dpi, "scale": scale }),
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

    let state = Arc::new(Mutex::new(SurfaceState::default()));
    // 当前会话的渲染端 stdin 槽(每次重建会话更新;管道命令经此转发)
    let current_stdin: Arc<Mutex<Option<Arc<Mutex<std::process::ChildStdin>>>>> =
        Arc::new(Mutex::new(None));

    // 控制通道:命名管道服务器(命令带 base_rev,状态权威在核心)
    {
        let state = state.clone();
        let cs = current_stdin.clone();
        pipe::start(
            r"\\.\pipe\desktopup-core-v1",
            Arc::new(move |req| {
                // 只读状态查询(控制中心订阅用,协议 get_state)
                if req.get("cmd").and_then(|v| v.as_str()) == Some("get_state") {
                    let s = state.lock().expect("state lock");
                    return json!({
                        "ok": true,
                        "revision": s.revision,
                        "geometry": s.geometry,
                        "visible": s.visible,
                        "params": s.params,
                    });
                }
                let base_rev = req
                    .get("base_rev")
                    .and_then(|v| v.as_u64())
                    .unwrap_or(u64::MAX);
                let cmd: Command = match serde_json::from_value(req.clone()) {
                    Ok(c) => c,
                    Err(e) => {
                        log("warn", "pipe.bad_command", json!({ "error": e.to_string() }));
                        return json!({ "ok": false, "error": "bad_frame" });
                    }
                };
                let mut s = state.lock().expect("state lock");
                match s.apply(&cmd, base_rev) {
                    Ok(()) => {
                        let rev = s.revision;
                        drop(s);
                        let fwd = match &cmd {
                            Command::SetGeometry { x, y, w, h } => json!({
                                "cmd": "set_geometry", "id": 0, "x": x, "y": y, "w": w, "h": h
                            }),
                            Command::SetVisible { visible } => json!({
                                "cmd": "set_visible", "id": 0, "visible": visible
                            }),
                            Command::SetParams { accent, opacity } => json!({
                                "cmd": "set_params", "id": 0, "accent": accent, "opacity": opacity
                            }),
                        };
                        if let Some(w) = cs.lock().expect("stdin slot").as_ref() {
                            let mut w = w.lock().expect("stdin lock");
                            let _ = proto::send_frame(&mut *w, &fwd);
                        }
                        log(
                            "info",
                            "state.applied",
                            json!({ "cmd": serde_json::to_value(&cmd).unwrap_or(json!(null)), "revision": rev }),
                        );
                        json!({ "ok": true, "revision": rev })
                    }
                    Err(c) => {
                        log(
                            "warn",
                            "state.rev_conflict",
                            json!({ "base_rev": base_rev, "current": c.current }),
                        );
                        json!({ "ok": false, "error": "rev_conflict", "current": c.current })
                    }
                }
            }),
        );
    }

    let mut restarts = 0u32;
    loop {
        let Some(mut child) = spawn_renderer(&renderer) else {
            log("error", "renderer.spawn_failed", json!({ "path": renderer }));
            return;
        };
        let stdin = Arc::new(Mutex::new(child.stdin.take().expect("stdin")));
        let stdout = child.stdout.take().expect("stdout");
        *current_stdin.lock().expect("stdin slot") = Some(stdin.clone());
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

        let mut next_id: u64 = 1;
        let mut ticks: u64 = 0;
        let mut exited = false;
        while !exited {
            ticks += 1;
            std::thread::sleep(Duration::from_millis(30));
            // 托盘动作:重启海报 = 发 shutdown,看门狗随后重建;退出由外层循环处理
            if tray::RESTART_REQUESTED.swap(false, Ordering::SeqCst) {
                log("info", "tray.restart_requested", json!({}));
                if let Ok(mut w) = stdin.lock() {
                    let _ = proto::send_frame(&mut *w, &json!({"cmd": "shutdown", "id": 0}));
                }
            }
            if tray::EXIT_REQUESTED.load(Ordering::SeqCst) {
                log("info", "tray.exit_requested", json!({}));
                if let Ok(mut w) = stdin.lock() {
                    let _ = proto::send_frame(&mut *w, &json!({"cmd": "shutdown", "id": 0}));
                }
                exited = true;
            }
            if ticks % 100 == 0 {
                let id = next_id;
                next_id += 1;
                if let Ok(mut w) = stdin.lock() {
                    let _ = proto::send_frame(&mut *w, &json!({"cmd": "get_health", "id": id}));
                }
            }
            if ticks % 300 == 150 {
                let id = next_id;
                next_id += 1;
                if let Ok(mut w) = stdin.lock() {
                    let _ = proto::send_frame(&mut *w, &json!({"cmd": "capture", "id": id}));
                }
            }
            loop {
                match rx.try_recv() {
                    Ok(Some(value)) => {
                        if let Some(id) = value.get("id").and_then(|v| v.as_u64()) {
                            if id == 0 {
                                continue; // 管道转发命令的回执,静默
                            }
                            if value.get("frames").is_some() {
                                // 几何同步:用户拖动后以渲染端报告为准(逻辑坐标),并重查 DPI
                                if let Some(geo) = value.get("geometry").and_then(|v| v.as_str()) {
                                    let parts: Vec<i32> = geo
                                        .split(',')
                                        .filter_map(|p| p.parse().ok())
                                        .collect();
                                    if parts.len() == 4 {
                                        if let Ok(mut guard) =
                                            SURFACE.get_or_init(|| Mutex::new(None)).lock()
                                        {
                                            if let Some(s) = guard.as_mut() {
                                                s.x = parts[0];
                                                s.y = parts[1];
                                                s.w = parts[2];
                                                s.h = parts[3];
                                                let dpi = unsafe {
                                                    GetDpiForWindow(HWND(
                                                        s.hwnd as *mut core::ffi::c_void,
                                                    ))
                                                };
                                                s.scale = dpi as f64 / 96.0;
                                            }
                                        }
                                    }
                                }
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
                            Some("hello") => {
                                register_surface(&value, &state);
                                // 配置注入:渲染端未收 ack 前不显示;几何/参数以核心状态为准
                                let (geometry, params) = {
                                    let s = state.lock().expect("state lock");
                                    (s.geometry, s.params.clone())
                                };
                                let ack = json!({
                                    "cmd": "hello_ack",
                                    "id": 0,
                                    "geometry": { "x": geometry.0, "y": geometry.1, "w": geometry.2, "h": geometry.3 },
                                    "params": { "accent": params.accent, "opacity": params.opacity },
                                });
                                if let Ok(mut w) = stdin.lock() {
                                    let _ = proto::send_frame(&mut *w, &ack);
                                }
                                log("info", "renderer.acked", json!({ "geometry": geometry }));
                            }
                            Some("geometry_changed") => {
                                // 用户拖动结束:事件入状态权威,修订递增
                                let num = |o: &serde_json::Value, k: &str| -> i32 {
                                    o.get(k)
                                        .and_then(|v| v.as_f64())
                                        .map(|f| f as i32)
                                        .unwrap_or(0)
                                };
                                if let Some(rect) = value.get("rect") {
                                    let g = (num(rect, "x"), num(rect, "y"), num(rect, "w"), num(rect, "h"));
                                    let mut s = state.lock().expect("state lock");
                                    s.geometry = g;
                                    s.revision += 1;
                                    log("info", "state.geometry_changed", json!({ "geometry": g, "revision": s.revision }));
                                }
                            }
                            Some("bye") => exited = true,
                            other => log("warn", "frame.unhandled", json!({ "cmd": other })),
                        }
                    }
                    Ok(None) | Err(std::sync::mpsc::TryRecvError::Disconnected) => {
                        exited = true;
                        break; // 必须跳出内层接收循环,否则 Disconnected 反复命中导致 100% CPU 空转
                    }
                    Err(std::sync::mpsc::TryRecvError::Empty) => break,
                }
            }
        }

        let code = child.try_wait().ok().flatten();
        log("info", "renderer.exited", json!({ "code": code.map(|c| c.to_string()), "restarts": restarts }));
        if tray::EXIT_REQUESTED.load(Ordering::SeqCst) {
            log("info", "lifecycle.exit", json!({}));
            return;
        }
        if restarts >= 3 {
            log("error", "lifecycle.restart_limit", json!({ "max": 3 }));
            return;
        }
        restarts += 1;
        log("info", "lifecycle.restarting", json!({ "attempt": restarts, "max": 3 }));
    }
}
